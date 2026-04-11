using System.Text.Json;
using TaiwanAgri.Core.Constants;
using TaiwanAgri.Modules.Weather.Data;
using TaiwanAgri.Modules.Weather.Dtos;
using Microsoft.EntityFrameworkCore;
using TaiwanAgri.Modules.Weather.Entities;

namespace TaiwanAgri.Worker
{
	public class WeatherSyncWorker : BackgroundService
	{
		private readonly ILogger<WeatherSyncWorker> _logger;
		private readonly HttpClient _httpClient;
		private readonly IServiceScopeFactory _scopeFactory;
		public WeatherSyncWorker(ILogger<WeatherSyncWorker> logger, IHttpClientFactory httpClientFactory, IServiceScopeFactory scopeFactory)
		{
			_logger = logger;
			_httpClient = httpClientFactory.CreateClient("MoaApi");
			_scopeFactory = scopeFactory;
		}
		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			while (!stoppingToken.IsCancellationRequested)
			{
				//try
				//{
				//	var json = await _httpClient.GetStringAsync(
				//		MoaApiEndpoints.AutoWeatherStation,   // 只寫路徑，BaseAddress 自動補前綴
				//		stoppingToken);
				//	// 新 API 回傳 {"RS":"OK","Data":[...]}，外層是 {
				//	if (!json.TrimStart().StartsWith("{"))
				//	{
				//		_logger.LogWarning("[WeatherSync] 回傳不是 JSON。前 200 字: {Preview}",
				//			json[..Math.Min(200, json.Length)]);
				//		await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
				//		continue;
				//	}
				//	_logger.LogInformation("[Weather] 回傳長度 : {Length} 字元", json.Length);
				//	_logger.LogInformation("[Weather] 前800字 : {Preview}", json[..Math.Min(800, json.Length)]);
				//}
				//catch (TaskCanceledException)
				//{
				//	_logger.LogWarning("[WeatherSync] 請求超時");
				//}
				//catch (Exception ex)
				//{
				//	_logger.LogError(ex, "[WeatherSync] 呼叫失敗");
				//}
				//await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);// 測試用
				try
				{
					await SyncWeatherAsync(stoppingToken);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "[WeatherSync] 同步失敗");
				}

				await Task.Delay(TimeSpan.FromHours(1), stoppingToken); // 正式排程每小時一次
			}
		}

		private async Task SyncWeatherAsync(CancellationToken stoppingToken)
		{
			// IServiceScopeFactory：每次寫入建立一個獨立的 Scope
			// 原因：DbContext 是 Scoped，不能直接注入進 Singleton 的 BackgroundService
			using var scope = _scopeFactory.CreateScope();
			var db = scope.ServiceProvider.GetRequiredService<WeatherDbContext>();
			// ── 步驟 1：清除 30 天前的舊資料 ──────────────────────────────────
			var cutoff = DateTime.UtcNow.AddDays(-30);
			var deleted = await db.WeatherObservations
				.Where(o => o.ObservedAt < cutoff)
				.ExecuteDeleteAsync(stoppingToken);
			if (deleted > 0)
				_logger.LogInformation("[WeatherSync] 已刪除 {Count} 筆 30 天前的舊資料", deleted);

			// ── 步驟 2：分頁抓取所有資料 ─────────────────────────────────────
			var allDtos = new List<WeatherStationDto>();
			int Page = 1;
			while (true) 
			{
				var url = (Page == 1) ? MoaApiEndpoints.AutoWeatherStation : $"{MoaApiEndpoints.AutoWeatherStation}?page={Page}";

				var json = await _httpClient.GetStringAsync(url, stoppingToken);
				var response = JsonSerializer.Deserialize<WeatherApiResponse>(json);

				if (response?.RS != "OK" || response.Data.Count == 0)
				{
					if (Page == 1)
						_logger.LogWarning("[WeatherSync] API 回傳異常或無資料");
					else
						_logger.LogInformation("[WeatherSync] 第 {Page} 頁無資料或無分頁權限，停止抓取", Page);
					break;
				}
				_logger.LogInformation("[WeatherSync] 第 {Page} 頁，回傳 {Count} 筆", Page, response.Data.Count);
				allDtos.AddRange(response.Data);
				if (!response.Next)
					break; // 沒有下一頁了
				Page++;

				// 安全保護：最多抓 20 頁（約 20000 筆），避免無限迴圈
				if (Page > 20)
				{
					_logger.LogWarning("[WeatherSync] 已達分頁上限（20頁），停止繼續抓取");
					break;
				}
			}
			_logger.LogInformation("[WeatherSync] 合計取得 {Count} 筆原始資料", allDtos.Count);

			// ── 步驟 3：轉換 Entity ────────────────────────────────────────────
			var incoming = allDtos
				.Select(MapToEntity)
				.Where(o => o != null)
				.Cast<WeatherObservation>()
				.ToList();

			if (incoming.Count == 0)
			{
				_logger.LogWarning("[WeatherSync] 全部資料轉換失敗（MapToEntity 皆回 null）");
				return;
			}

			// ── 步驟 4：查詢已存在的組合，只寫入新的 ────────────────────────
			var targetTimes = incoming
				.Select(o => o.ObservedAt)
				.Distinct()
				.ToList();

			var existingKeys = await db.WeatherObservations
				.Where(o => targetTimes.Contains(o.ObservedAt))
				.Select(o => new { o.StationId, o.ObservedAt })
				.ToListAsync(stoppingToken);

			var existingSet = existingKeys
				.Select(k => (k.StationId, k.ObservedAt))
				.ToHashSet();

			var newObservations = incoming
				.Where(o => !existingSet.Contains((o.StationId, o.ObservedAt)))
				.ToList();

			// ── 步驟 5：寫入 ──────────────────────────────────────────────────
			if (newObservations.Count == 0)
			{
				_logger.LogInformation("[WeatherSync] 無新資料需寫入（全部已存在）");
				return;
			}

			await db.WeatherObservations.AddRangeAsync(newObservations, stoppingToken);
			await db.SaveChangesAsync(stoppingToken);

			_logger.LogInformation("[WeatherSync] 成功寫入 {Count} 筆，略過 {Skipped} 筆重複",
				newObservations.Count,
				incoming.Count - newObservations.Count);
		}

		private WeatherObservation? MapToEntity(WeatherStationDto dto)
		{
			// TIME 格式是 "2026/04/02 11:00"，需要轉成 DateTime
			if (!DateTime.TryParseExact(dto.Time, "yyyy/MM/dd HH:mm", null, System.Globalization.DateTimeStyles.None, out var observedAt))
			{
				_logger.LogWarning("[WeatherSync] 時間格式錯誤，略過站 {StationId}: {Time}",
					dto.StationId, dto.Time);
				return null;
			}

				return new WeatherObservation
				{
					StationId = dto.StationId,
					StationName = dto.StationName,
					ObservedAt = observedAt,
					Latitude = ParseDecimal(dto.Latitude),
					Longitude = ParseDecimal(dto.Longitude),
					Elevation = ParseInt(dto.Elevation),
					WindDirection = NullIfEmpty(dto.WindDirection),
					WindSpeed = ParseDecimal(dto.WindSpeed),
					MaxGust = ParseDecimal(dto.MaxGust),       // "儀器校驗中" → null
					MaxGustDirection = NullIfEmpty(dto.MaxGustDirection),
					Temperature = ParseDecimal(dto.Temperature),
					Humidity = ParseDecimal(dto.Humidity),
					Pressure = ParseDecimal(dto.Pressure),
					SunshineHours = ParseDecimal(dto.Sunshine),
					Rainfall24h = ParseDecimal(dto.Rainfall24h),
					DailyMaxTemp = ParseDecimal(dto.DailyMaxTemp),
					DailyMinTemp = ParseDecimal(dto.DailyMinTemp),
					CityCode = dto.CityCode,
					CityName = dto.CityName,
					TownCode = NullIfEmpty(dto.TownCode),
					TownName = NullIfEmpty(dto.TownName),
					SyncedAt = DateTime.UtcNow
				};
			}
			// ── 防呆輔助方法 ─────────────────────────────────────────────────────
			// 字串 → decimal?：非數字（含"儀器校驗中"）一律回 null
			 private static decimal? ParseDecimal(string? s) =>
				decimal.TryParse(s, System.Globalization.NumberStyles.Any,
					System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : null;
			// 字串 → int?
			private static int? ParseInt(string? s) =>
				int.TryParse(s, out var v) ? v : null;

			// 空字串視為 null
			private static string? NullIfEmpty(string? s) =>
				string.IsNullOrWhiteSpace(s) ? null : s;
	}
}
