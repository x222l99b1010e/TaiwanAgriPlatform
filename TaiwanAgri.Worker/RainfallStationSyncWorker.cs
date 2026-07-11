using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TaiwanAgri.Core.Constants;
using TaiwanAgri.Modules.Weather.Data;
using TaiwanAgri.Modules.Weather.Dtos.WorkerResponses;
using TaiwanAgri.Modules.Weather.Entities;

namespace TaiwanAgri.Worker
{
	public class RainfallStationSyncWorker : ScheduledSyncWorkerBase
	{
		private readonly ILogger<RainfallStationSyncWorker> _logger;
		private readonly HttpClient _httpClient;
		private readonly IServiceScopeFactory _scopeFactory;
		public RainfallStationSyncWorker(ILogger<RainfallStationSyncWorker> logger, IHttpClientFactory httpClientFactory, IServiceScopeFactory scopeFactory)
			: base(logger)
		{
			_logger = logger;
			_httpClient = httpClientFactory.CreateClient("MoaApi");
			_scopeFactory = scopeFactory;
		}
		protected override TimeSpan Interval => TimeSpan.FromDays(7); // 正式排程7天一次
		protected override string LogPrefix => "[RainfallStationSync]";

		protected override async Task SyncAsync(CancellationToken stoppingToken)
		{
			//每次建立一個Scope
			using var scope = _scopeFactory.CreateScope();
			//從Scope中取得DbContext實例
			var db = scope.ServiceProvider.GetRequiredService<WeatherDbContext>();
			//從API取得資料
			var allDtos = new List<RainfallStationDto>();
			int page = 1;
			while (true)
			{
				// Moa API的分頁機制：第一頁不帶page參數，第二頁開始帶?page=2
				var url = (page == 1) ? MoaApiEndpoints.RainfallStation : $"{MoaApiEndpoints.RainfallStation}?page={page}";
				// 取得API回應
				var json = await _httpClient.GetStringAsync(url, stoppingToken);
				// 反序列化API回應
				var response = JsonSerializer.Deserialize<RainfallStationApiResponse>(json);

				if(response?.RS != "OK" || response.Data.Count == 0)
				{
					if (page == 1)
						_logger.LogWarning("[RainfallStationSync] API回應異常或無資料，停止同步");
					else
						_logger.LogInformation("[RainfallStationSync] 第 {Page} 頁無資料或無分頁權限，停止抓取", page);
					break;
				}
				_logger.LogInformation("[RainfallStationSync] 成功取得第 {Page} 頁資料，共 {Count} 筆", page, response.Data.Count);
				//將Data加入DTO列表
				allDtos.AddRange(response.Data);
				if (!response.Next)
					break;
				page ++;
				//
				if (page > 20) //安全機制，避免無限迴圈
				{
					_logger.LogWarning("[RainfallStationSync] 已達分頁上限（20頁），停止繼續抓取");
					break;
				}
			}
			_logger.LogInformation("[RainfallStationSync] 合計取得 {Count} 筆原始資料", allDtos.Count);

			//將DTO轉換為Entity
			var incoming = allDtos
				.Select(MapToEntity)
				.ToList();
			if (incoming.Count == 0)
			{
				_logger.LogWarning("[RainfallStationSync] 全部資料轉換失敗（MapToEntity 皆回 null）");
				return;
			}
			// 把 API 回傳的 StationId 存成 HashSet，之後軟刪除要用
			var apiStationIds = incoming
				.Select(s => s.StationId)
				.ToHashSet();
			// 把 DB 裡所有站台撈出來，用字典存（key = StationId，方便查找）
			var existingStations = await db.RainfallStations
				.ToDictionaryAsync(rs => rs.StationId,stoppingToken);

			foreach (var station in incoming)
			{
				if (existingStations.TryGetValue(station.StationId, out var existing))
				{
					//把incoming裡面的station一一跟dictionary裡面的existing比對，如果有找到就更新欄位，沒有找到就新增
					// 站台已存在 → 更新欄位，保留 CreatedAt
					existing.StationName = station.StationName;
					existing.CityName = station.CityName;
					existing.CityCode = station.CityCode;
					existing.TownName = station.TownName;
					existing.TownCode = station.TownCode;
					existing.IsActive = true; // API有的站台都設為啟用
					existing.UpdatedAt = DateTime.UtcNow;
				}
				else
				{
					// 新站台 → Insert，CreatedAt 在這裡設
					station.CreatedAt = DateTime.UtcNow;
					db.RainfallStations.Add(station);
				}
			}
			// 處理軟刪除：DB裡有的站台，但API沒有回傳 → 軟刪除（IsActive = false）
			//這行是抓db裡面抓出來的existingStations，然後一筆一筆比對，
			//如果發現有一筆existing的StationId不在apiStationIds裡面，
			//就把這筆existing的IsActive設為false，並更新UpdatedAt
			foreach (var existing in existingStations.Values)
			{
				if (!apiStationIds.Contains(existing.StationId))
				{
					existing.IsActive = false;
					existing.UpdatedAt = DateTime.UtcNow;
				}
			}
			await db.SaveChangesAsync(stoppingToken);
			_logger.LogInformation("[RainfallStationSync] Upsert 完成");
		}

		private RainfallStation MapToEntity(RainfallStationDto dto)
		{
			return new RainfallStation
			{
				StationId = dto.StationId,
				StationName = dto.StationName,
				CityName = dto.CityName,
				CityCode = dto.CityCode,
				TownName = dto.TownName,
				TownCode = dto.TownCode,
				IsActive = true, //預設為啟用
				UpdatedAt = DateTime.UtcNow
				// CreatedAt 不在這裡設，由 Upsert 邏輯決定
			};
		}
	}
}
