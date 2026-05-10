using TaiwanAgri.Modules.Market.Data;
using Microsoft.EntityFrameworkCore;
using TaiwanAgri.Core.Constants;
using System.Text.Json;
using TaiwanAgri.Modules.Market.Entities;
using TaiwanAgri.Modules.Market.Dtos.WorkerResponses;


namespace TaiwanAgri.Worker
{
	public class DebrisAlertRecordSyncWorker : BackgroundService
	{
		private readonly ILogger _logger;
		private readonly HttpClient _httpClient;
		private readonly IServiceScopeFactory _serviceScopeFactory;
		public DebrisAlertRecordSyncWorker(ILogger<DebrisAlertRecordSyncWorker> logger, IHttpClientFactory httpClientFactory, IServiceScopeFactory serviceScopeFactory)
		{
			_logger = logger;
			_httpClient = httpClientFactory.CreateClient("MoaApi");
			_serviceScopeFactory = serviceScopeFactory;
		}
		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			while(!stoppingToken.IsCancellationRequested)
			{
				try
				{
					await SyncDebrisAlertRecordAsync(stoppingToken);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, " [DebrisAlertRecordSyncWorker] 同步失敗 ");
				}
				await Task.Delay(TimeSpan.FromHours(6), stoppingToken); // 每6小時執行一次
			}
		}

		private async Task SyncDebrisAlertRecordAsync(CancellationToken stoppingToken)
		{
			using var scope = _serviceScopeFactory.CreateScope();
			var db = scope.ServiceProvider.GetRequiredService<MarketDbContext>();

			var existingRecords = await db.DebrisAlertRecords
				.Select(r => new {r.ReportID, r.DebrisNo, r.LandslideID})
				.ToHashSetAsync(stoppingToken);

			var url = MoaApiEndpoints.DebrisAlert;
			var json = await _httpClient.GetStringAsync(url, stoppingToken);
			var response = JsonSerializer.Deserialize<List<DebrisAlertRecordDto>>(json);
			//var response = JsonSerializer.Deserialize<List<DebrisAlertRecordDto>>(json) ?? new List<DebrisAlertRecordDto>();
			if (response == null)
			{
				_logger.LogWarning("[DebrisAlertRecordSyncWorker] API 回傳資料反序列化失敗");
				return;
			}

			var incoming = response
				.Select(MapToEntity)
				.DistinctBy(e => new { e.ReportID, e.DebrisNo, e.LandslideID })
				.ToList();

			var newRecords = incoming
				.Where(e => !existingRecords.Contains(new { e.ReportID, e.DebrisNo, e.LandslideID }))
				.ToList();
			if (newRecords.Count == 0)
			{
				_logger.LogInformation(" [DebrisAlertRecordSyncWorker] 無新資料需寫入（全部已存在） ");
				return;
			}
			db.DebrisAlertRecords.AddRange(newRecords);
			await db.SaveChangesAsync(stoppingToken);
			_logger.LogInformation(" [DebrisAlertRecordSyncWorker] 同步完成，新增 {Count} 筆資料, 略過 {Skipped} 筆重複", 
				newRecords.Count, incoming.Count - newRecords.Count);

		}

		private DebrisAlertRecord MapToEntity(DebrisAlertRecordDto dto)
		{
			return new DebrisAlertRecord
			{
				DisasterID = dto.DisasterID,
				DisasterName = dto.DisasterName,
				AlertType = dto.AlertType,
				DebrisNo = dto.DebrisNo == "-" ? null : dto.DebrisNo,
				LandslideID = dto.LandslideID == "-" ? null : dto.LandslideID,
				LandslideName = dto.LandslideName == "-" ? null : dto.LandslideName,
				County = dto.County,
				Town = dto.Town,
				Vill = dto.Vill == "-" ? null : dto.Vill,
				AlertLevel = dto.AlertLevel,
				//確定解析失敗時不需要報錯，只是要過編譯
				LastUpdateDate = DateTime.Parse(dto.LastUpdateDate, System.Globalization.CultureInfo.InvariantCulture),
				ReportID = dto.ReportID,
				CountyCode = dto.CountyCode,
				AreaCode = dto.AreaCode,
				CreatedAt = DateTime.UtcNow
			};
		}
	}
}
