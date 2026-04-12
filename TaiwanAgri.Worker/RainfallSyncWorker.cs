using TaiwanAgri.Modules.Weather.Data;
using Microsoft.EntityFrameworkCore;
using TaiwanAgri.Modules.Weather.Dtos;
using TaiwanAgri.Core.Constants;
using System.Text.Json;
using TaiwanAgri.Modules.Weather.Entities;

namespace TaiwanAgri.Worker
{
	public class RainfallSyncWorker : BackgroundService
	{
		private readonly ILogger<RainfallSyncWorker> _logger;
		private readonly HttpClient _httpClient;
		private readonly IServiceScopeFactory _scopeFactory;
		public RainfallSyncWorker(ILogger<RainfallSyncWorker> logger, IHttpClientFactory httpClientFactory, IServiceScopeFactory scopeFactory)
		{
			_logger = logger;
			_httpClient = httpClientFactory.CreateClient("MoaApi");
			_scopeFactory = scopeFactory;

		}
		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			// Polling：等待站台資料就緒
			while (!stoppingToken.IsCancellationRequested)
			{
				using var pollScope = _scopeFactory.CreateScope();
				var pollDb = pollScope.ServiceProvider.GetRequiredService<WeatherDbContext>();

				var count = await pollDb.RainfallStations.CountAsync(stoppingToken);
				if (count > 0) break;

				_logger.LogInformation("[RainfallSync] 等待站台資料，30 秒後重試...");
				await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
			}

			// 主同步迴圈
			while (!stoppingToken.IsCancellationRequested)
			{
				try 
				{
					await SyncRainfallAsync(stoppingToken);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "[RainfallSync] 同步失敗");
				}
				await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken); // 正式排程10分鐘一次
			}
		}

		private async Task SyncRainfallAsync(CancellationToken stoppingToken)
		{
			//每次建立一個Scope
			using var scope = _scopeFactory.CreateScope();
			//從Scope中取得DbContext實例
			var db = scope.ServiceProvider.GetRequiredService<WeatherDbContext>();
			//從API取得資料
			var allDtos = new List<RainfallObservationDto>();
			int Page = 1;
			while (true) 
			{
				//Moa API的分頁機制：第一頁不帶page參數，第二頁開始帶?page=2
				var url = (Page == 1) ? MoaApiEndpoints.AutoRainfall :$"{MoaApiEndpoints.AutoRainfall}?page={Page}";
				//發送HTTP請求
				var json = await _httpClient.GetStringAsync(url, stoppingToken);
				//反序列化JSON為DTO列表
				var response = JsonSerializer.Deserialize<RainfallObservationApiResponse>(json);

				if (response?.RS != "OK" || response.Data.Count == 0)
				{
					if (Page == 1)  _logger.LogWarning("[RainfallSync] API回應異常或無資料");

					else _logger.LogInformation("[RainfallSync] 已無更多資料，完成同步");

					break;
				}
				_logger.LogInformation("[RainfallSync] 取得第 {Page} 頁資料，共 {Count} 筆", Page, response.Data.Count);
				allDtos.AddRange(response.Data);
				if (!response.Next)
					break;
				Page ++;
				//
				if (Page > 20) //安全機制，避免無限迴圈
				{
					_logger.LogWarning("[RainfallSync] 已達分頁上限（20頁），停止繼續抓取");
					break;
				}
			}
			_logger.LogInformation("[RainfallSync] 本次共取得 {TotalCount} 筆資料，開始更新資料庫", allDtos.Count);

			//將DTO轉換為Entity
			var incoming = allDtos
				.Select(MapToEntity)
				.Where(e => e != null)
				.Cast<RainfallObservation>()
				.ToList();

			if (incoming.Count == 0)
			{
				_logger.LogWarning("[RainfallSync] 全部資料轉換失敗（MapToEntity 皆回 null）");
				return;
			}

			// 用這次拿到的時間點去 DB 查哪些已存在
			var targetTimes = incoming
				.Select(o => o.ObservedAt)
				.Distinct()
				.ToList();

			// 查詢已存在的觀測資料（StationId + ObservedAt 為唯一鍵）
			var existingKeys = await db.RainfallObservations
				.Where(o => targetTimes.Contains(o.ObservedAt))
				.Select(o => new { o.StationId, o.ObservedAt })
				.ToListAsync(stoppingToken);

			var existingSet = existingKeys
				.Select(k => (k.StationId, k.ObservedAt))
				.ToHashSet();

			var newObservations = incoming
				.Where(k => !existingSet.Contains((k.StationId, k.ObservedAt)))
				.ToList();

			// 從這批資料順帶更新站台的地理座標
			var stationUpdates = allDtos
				.GroupBy(d => d.StationId)
				.Select(g => g.First()); // 每個站取一筆就夠，座標不會變


			//這裡每個站台都打一次 DB 查詢。如果有 500 個站台，就是 500 次查詢。現在先這樣跑沒問題，
			//之後如果發現這段很慢，可以改成一次把所有站台撈出來再比對——但這是優化，不是現在的優先項。
			foreach (var dto in stationUpdates)
			{
				var station = await db.RainfallStations
					.FirstOrDefaultAsync(s => s.StationId == dto.StationId, stoppingToken);
				if (station != null)
				{
					station.Latitude = ParseDecimal(dto.Latitude);
					station.Longitude = ParseDecimal(dto.Longitude);
					station.Elevation = ParseInt(dto.Elevation);
					station.UpdatedAt = DateTime.UtcNow;
				}
			}

			// 寫入
			// 先寫觀測資料（如果有的話）
			if (newObservations.Count > 0)
				await db.RainfallObservations.AddRangeAsync(newObservations, stoppingToken);

			await db.SaveChangesAsync(stoppingToken);

			_logger.LogInformation("[RainfallSync] 成功寫入 {Count} 筆，略過 {Skipped} 筆重複",
				newObservations.Count,
				incoming.Count - newObservations.Count);

		}

		private RainfallObservation? MapToEntity(RainfallObservationDto dto)
		{
			//Time欄位格式為 "2026/04/10 15:10"，需要解析成DateTime
			if (!DateTime.TryParseExact(dto.Time, "yyyy/MM/dd HH:mm", null, System.Globalization.DateTimeStyles.None, out var parsedTime))
			{
				// 解析失敗，記錄警告並返回 null
				_logger.LogWarning("[RainfallSync] 時間格式錯誤，略過站 {StationName}: {Time}", dto.StationName, dto.Time);
				return null;
			}

			return new RainfallObservation
			{
				StationId = dto.StationId,
				ObservedAt = parsedTime,
				Rain = ParseDecimal(dto.Rain),
				Min10 = ParseDecimal(dto.Min10),
				Hour3 = ParseDecimal(dto.Hour3),
				Hour6 = ParseDecimal(dto.Hour6),
				Hour12 = ParseDecimal(dto.Hour12),
				Hour24 = ParseDecimal(dto.Hour24),
				Now = ParseDecimal(dto.NowTotal),
				Attribute = dto.Attribute,
				SyncedAt = DateTime.UtcNow
			};
		}
		// ── 防呆輔助方法 ─────────────────────────────────────────────────────
		// 字串 → decimal?：非數字（含"儀器校驗中"）一律回 null
		private static decimal? ParseDecimal(string? s) =>
				decimal.TryParse(s, System.Globalization.NumberStyles.Any,
					System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : null;

		// 字串 → int?：非數字一律回 null
		private static int? ParseInt(string? s) =>
			int.TryParse(s, out var v) ? v : null;
	}
}
