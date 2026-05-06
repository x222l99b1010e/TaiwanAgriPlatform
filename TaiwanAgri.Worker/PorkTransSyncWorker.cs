using TaiwanAgri.Modules.Market.Data;
using Microsoft.EntityFrameworkCore;
using TaiwanAgri.Core.Constants;
using System.Text.Json;
using TaiwanAgri.Modules.Market.Dtos;
using TaiwanAgri.Core.Infrastructure.Data;
using TaiwanAgri.Core.Helpers;
using TaiwanAgri.Core.Infrastructure.Entities;
using TaiwanAgri.Modules.Market.Entities;

namespace TaiwanAgri.Worker
{
	public class PorkTransSyncWorker : BackgroundService
	{
		private readonly ILogger<PorkTransSyncWorker> _logger;
		private readonly HttpClient _httpClient;
		private readonly IServiceScopeFactory _serviceScopeFactory;
		public PorkTransSyncWorker(ILogger<PorkTransSyncWorker> logger, IHttpClientFactory httpClientFactory, IServiceScopeFactory serviceScopeFactory)
		{
			_logger = logger;
			_httpClient = httpClientFactory.CreateClient("MoaApi");
			_serviceScopeFactory = serviceScopeFactory;
		}
		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			while (!stoppingToken.IsCancellationRequested)
			{
				try
				{
					await SyncPorkTransAsync(stoppingToken);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, " [PorkTransSyncWorker] 同步失敗 ");
				}
				await Task.Delay(TimeSpan.FromHours(12), stoppingToken); // 每12小時執行一次
			}
		}

		private async Task SyncPorkTransAsync(CancellationToken stoppingToken)
		{
			using var scope = _serviceScopeFactory.CreateScope();
			var dbMarket = scope.ServiceProvider.GetRequiredService<MarketDbContext>();
			var dbCore = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

			// 取得同步狀態，若無則初始化
			var lastSyncState = await dbCore.SyncStates.FirstOrDefaultAsync(s => s.SyncKey == "Market_PorkTrans", cancellationToken: stoppingToken);
			// 若無同步狀態，則初始化為0981126
			if (lastSyncState == null)
			{
				// 初始化同步狀態
				lastSyncState = new SyncState
				{
					SyncKey = "Market_PorkTrans",
					LastSyncedDate = new DateOnly(2009, 11, 26),
					UpdatedAt = DateTime.UtcNow
				};
				dbCore.SyncStates.Add(lastSyncState);
				await dbCore.SaveChangesAsync(stoppingToken);
			}
			DateOnly startDate = lastSyncState.LastSyncedDate.AddDays(1);
			// 1. 取得台灣時區 (台北標準時間)
			// 注意：在 Windows 上是 "Taipei Standard Time"，在 Linux/macOS 上通常是 "Asia/Taipei"
			// 2. 取得目前的台灣時間 (先抓 UTC 再轉換，這最準確)
			// 3. 取得「昨天」
			// 4. 轉換成 DateOnly (如果你資料庫存的是 DateOnly)
			DateOnly yesterdayDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
			TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "Taipei Standard Time" : "Asia/Taipei")).AddDays(-1));
			
			var allDtos = new List<PorkTransTypeDto>();
			DateOnly lastSuccessfulDate = lastSyncState.LastSyncedDate;
			for (DateOnly currentDate = startDate; currentDate <= yesterdayDate; currentDate = currentDate.AddDays(1))
			{
				try
				{
					string transDateParam = currentDate.ToRocNumericDate();
					var url = $"{MoaApiEndpoints.PorkTrans}?TransDate={transDateParam}";
					var json = await _httpClient.GetStringAsync(url, stoppingToken);
					var res = JsonSerializer.Deserialize<PorkTransTypeApiResponse>(json);

					// 最佳化 3: 區分「無資料」與「API 異常」
					if (res?.RS != "OK")
					{
						_logger.LogError(" [PorkTransSyncWorker] API 回傳異常 ({RS})，日期: {Date}", res?.RS, currentDate);
						break; // 發生異常就中斷，不更新同步狀態到最後
					}

					if (res.Data != null && res.Data.Count > 0)
					{
						allDtos.AddRange(res.Data);
					}

					// 只要 API 回傳 OK (包含無資料的休市日)，就紀錄最後處理成功的日期
					lastSuccessfulDate = currentDate;
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, " [PorkTransSyncWorker] 請求失敗，日期: {Date}", currentDate);
					break; // 網路噴錯也中斷
				}
			}
			_logger.LogInformation(" [PorkTransSyncWorker] 同步完成，總共回傳 {Count} 筆，日期: {Date}", allDtos.Count, yesterdayDate);
			// 1. 在查詢資料庫時，改用「匿名類別」而不是 Tuple
			// 在這裡才抓現有的 Keys，確保比對到的是最新狀態
			var existingHashSet = (await dbMarket.PorkTrans
				.Where(p => p.TransDate >= startDate)
				.Select(p => new { p.TransDate, p.MarketName })
				.ToListAsync(stoppingToken))
				.ToHashSet();

			var incoming = allDtos
				.Select(MapToEntity)
				.DistinctBy(p => new { p.TransDate, p.MarketName }) // 預防 API 本身給重複資料
				.ToList();

			var newTrans = incoming
				.Where(p => !existingHashSet.Contains(new { p.TransDate, p.MarketName }))
				.ToList();

			if (newTrans.Any())
			{
				dbMarket.PorkTrans.AddRange(newTrans);
				await dbMarket.SaveChangesAsync(stoppingToken);
			}

			// 最後根據真正「走完」的日期來更新狀態
			if (lastSuccessfulDate > lastSyncState.LastSyncedDate)
			{
				lastSyncState.LastSyncedDate = lastSuccessfulDate;
				lastSyncState.UpdatedAt = DateTime.UtcNow;
				await dbCore.SaveChangesAsync(stoppingToken);
			}
			_logger.LogInformation(" [PorkTransSyncWorker] 同步寫入完成，新增 {Count} 筆資料，略過 {Skipped} 筆重複", 
				newTrans.Count, incoming.Count - newTrans.Count);
		}

		private PorkTrans MapToEntity(PorkTransTypeDto dto)
		{
			return new PorkTrans
			{
				// 使用 DateHelper 將民國年字串 (YYY.MM.DD) 轉換為 DateOnly
				TransDate = DateHelper.ParseRocNumericDate(dto.TransDate),
				MarketName = dto.MarketName,

				// --- 成交頭數總數系列 ---
				TotalTransCount = dto.TotalTransCount,
				TotalTransAvgWeight = dto.TotalTransAvgWeight,
				TotalTransAvgPrice = dto.TotalTransAvgPrice,

				// --- 規格豬系列 ---
				SpecPigCount = dto.SpecPigCount,
				SpecPigAvgWeight = dto.SpecPigAvgWeight,
				SpecPigAvgPrice = dto.SpecPigAvgPrice,

				// --- 95(含)-115(含) 系列 ---
				Count95To115kg = dto.Count95To115kg,
				AvgWeight95To115kg = dto.AvgWeight95To115kg,
				AvgPrice95To115kg = dto.AvgPrice95To115kg,

				// --- 75(含)-95(不含) 系列 ---
				Count75To95kg = dto.Count75To95kg,
				AvgWeight75To95kg = dto.AvgWeight75To95kg,
				AvgPrice75To95kg = dto.AvgPrice75To95kg,

				// --- 115(含)-135(不含) 系列 ---
				Count115To135kg = dto.Count115To135kg,
				AvgWeight115To135kg = dto.AvgWeight115To135kg,
				AvgPrice115To135kg = dto.AvgPrice115To135kg,

				// --- 75公斤以下 系列 ---
				CountUnder75kg = dto.CountUnder75kg,
				AvgWeightUnder75kg = dto.AvgWeightUnder75kg,
				AvgPriceUnder75kg = dto.AvgPriceUnder75kg,

				// --- 淘汰種豬 系列 ---
				OutPigsCount = dto.OutPigsCount,
				OutPigsAvgWeight = dto.OutPigsAvgWeight,
				OutPigsAvgPrice = dto.OutPigsAvgPrice,

				// --- 其他豬頭數 系列 ---
				OtherPigsCount = dto.OtherPigsCount,
				OtherPigsAvgWeight = dto.OtherPigsAvgWeight,
				OtherPigsAvgPrice = dto.OtherPigsAvgPrice,

				// --- 冷凍廠 系列 ---
				FreezerPigsCount = dto.FreezerPigsCount,
				FreezerPigsAvgWeight = dto.FreezerPigsAvgWeight,
				FreezerPigsAvgPrice = dto.FreezerPigsAvgPrice,

				// --- 成交總數(不含冷凍廠) 系列 ---
				ExcludeFreezerCount = dto.ExcludeFreezerCount,
				ExcludeFreezerAvgWeight = dto.ExcludeFreezerAvgWeight,
				ExcludeFreezerAvgPrice = dto.ExcludeFreezerAvgPrice,

				// --- 135(含)-155(不含) 系列 ---
				Count135To155kg = dto.Count135To155kg,
				AvgWeight135To155kg = dto.AvgWeight135To155kg,
				AvgPrice135To155kg = dto.AvgPrice135To155kg,

				// --- 155公斤以上 系列 ---
				CountAbove155kg = dto.CountAbove155kg,
				AvgWeightAbove155kg = dto.AvgWeightAbove155kg,
				AvgPriceAbove155kg = dto.AvgPriceAbove155kg,

				// 系統時間會自動在實體中初始化為 DateTime.UtcNow
				CreatedAt = DateTime.UtcNow
			};
		}
	}
}
