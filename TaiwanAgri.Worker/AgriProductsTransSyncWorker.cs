using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TaiwanAgri.Core.Constants;
using TaiwanAgri.Core.Helpers;
using TaiwanAgri.Core.Infrastructure.Data;
using TaiwanAgri.Core.Infrastructure.Entities;
using TaiwanAgri.Modules.Market.Data;
using TaiwanAgri.Modules.Market.Dtos;
using TaiwanAgri.Modules.Market.Entities;

namespace TaiwanAgri.Worker
{
	public class AgriProductsTransSyncWorker : BackgroundService
	{
		private readonly ILogger<AgriProductsTransSyncWorker> _logger;
		private readonly HttpClient _httpClient;
		private readonly IServiceScopeFactory _serviceScopeFactory;
		public AgriProductsTransSyncWorker(ILogger<AgriProductsTransSyncWorker> logger, IHttpClientFactory httpClientFactory, IServiceScopeFactory serviceScopeFactory)
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
					await SyncAgriProductsTransAsync(stoppingToken);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, " [AgriProductsTransSyncWorker] 同步失敗 ");
				}
				await Task.Delay(TimeSpan.FromDays(1), stoppingToken); // 每天執行一次
			}
		}

		private async Task SyncAgriProductsTransAsync(CancellationToken stoppingToken)
		{
			using var scope = _serviceScopeFactory.CreateScope();
			var dbMarket = scope.ServiceProvider.GetRequiredService<MarketDbContext>();
			var dbCore = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
			// 取得同步狀態，若無則初始化
			var lastSyncState = await dbCore.SyncStates
				.FirstOrDefaultAsync(s => s.SyncKey == "Market_AgriProductsTrans", cancellationToken: stoppingToken);
			// 從上次同步的下一天開始同步
			if (lastSyncState == null)
			{
				// 情況一：資料不存在，建立新的
				lastSyncState = new SyncState
				{
					SyncKey = "Market_AgriProductsTrans",
					LastSyncedDate = new DateOnly(2018, 06, 30),
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

			//從 dbMarket 撈所有 MarketInfo
			var marketInfos = await dbMarket.MarketInfos.ToListAsync(stoppingToken);
			// 快取全量作物代碼，避免迴圈內重複查詢資料庫
			//先把 CropInfo 的 CropCode 全撈出來，存在 existingCropCodes 裡（HashSet<string>），方便後面比對哪些是新的 CropCode
			var existingCropCodeSet = await dbMarket.CropInfos
				.Select(x => x.CropCode)
				.ToHashSetAsync(stoppingToken);
			for (DateOnly currentDate = startDate; currentDate <= yesterdayDate; currentDate = currentDate.AddDays(1))
			{
				_logger.LogInformation("--- 開始同步日期: {Date} ---", currentDate);
				// 併發抓取所有市場 API 請求，減少網路等待時間
				var rawResults = await Task.WhenAll(marketInfos.Select(async market =>
				{
					var url = $"{MoaApiEndpoints.AgriProductsTrans}?Start_time={DateHelper.FormatRocDate(currentDate)}&End_time={DateHelper.FormatRocDate(currentDate)}&MarketName={market.MarketName}";
					try
					{
						var json = await _httpClient.GetStringAsync(url, stoppingToken);
						return (Market: market, Json: json, Success: true);
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "市場 {Market} 抓取失敗", market.MarketName);
						return (Market: market, Json: string.Empty, Success: false);
					}
				}));
				// 【效能優化比對：當日已存在資料】
				// 1. AsNoTracking & Select: 僅抓取必要欄位且不追蹤實體，極大化查詢效能。
				// 2. 批次處理: 進入市場迴圈前先抓出該日所有資料，避免在巢狀迴圈中反覆查詢 DB。
				// 3. HashSet (O(1)): 將 (日期, 作物, 市場, 類型) 封裝為複合鍵 (Composite Key) 存入雜湊集，確保比對速度。
				var existingKeys = (await dbMarket.AgriProductsTrans
					.AsNoTracking()
					.Where(x => x.TransDate == currentDate)
					.Select(x => new { x.TransDate, x.CropCode, x.MarketCode, x.TcType })
					.ToListAsync(stoppingToken))
					.Select(x => (x.TransDate, x.CropCode, x.MarketCode, x.TcType))
					.ToHashSet();
				//在這裡（每天重置）
				foreach (var (market, json, success) in rawResults)
				{
					if (!success || string.IsNullOrEmpty(json)) continue;
					var response = JsonSerializer.Deserialize<AgriProductsTransTypeApiResponse>(json);

					if (response?.RS != "OK")
					{
						_logger.LogWarning("[SyncAgriProductsTransAsync] 市場 {Market} API回應異常: {RS}", market.MarketName, response?.RS);
						continue;
					}

					if (response.Data == null || response.Data.Count == 0)
					{
						_logger.LogInformation("[SyncAgriProductsTransAsync] 市場 {Market} 無資料，跳過", market.MarketName);
						continue;
					}
					// 1. 過濾休市與重複項，抽出唯一的資料（根據日期、作物代碼、市場代碼、交易類型去重複），避免同一天同一個市場同一個作物有多筆資料的情況（雖然理論上不應該有，但以防萬一）
					var incoming = response.Data
						.Where(x => x.CropCode != "-")
						.DistinctBy(x => new { x.TransDate, x.CropCode, x.MarketCode, x.TcType })
						.ToList();

					// 2. 處理新發現的作物代碼 (CropInfo)
					var newCrops = incoming
						.Where(x => !existingCropCodeSet.Contains(x.CropCode))
						.DistinctBy(x => x.CropCode)
						.Select(x => new CropInfo
						{
							CropCode = x.CropCode,
							CropName = x.CropName,
							CreatedAt = DateTime.UtcNow
						}).ToList();

					//如果有新的 CropCode，先新增到 CropInfo 資料表（避免外鍵問題）
					if (newCrops.Any())
					{
						//把新的 CropCode 加到 existingCropCodeSet 裡，這樣同一天如果有多筆資料是同一個新的 CropCode，就不會重複新增 CropInfo
						foreach (var c in newCrops) existingCropCodeSet.Add(c.CropCode);					
						//新增 CropInfo 資料
						dbMarket.CropInfos.AddRange(newCrops);
						//記錄日誌，顯示發現了多少筆新的 CropCode
						_logger.LogInformation("[SyncAgriProductsTransAsync] 發現 {Count} 筆新作物代碼，正在新增 CropInfo 資料", newCrops.Count);
						//注意：這裡不需要立刻呼叫 SaveChangesAsync，因為後面還有 AgriProductsTrans 的新增，等全部資料都準備好後再一起寫入資料庫，這樣效率更好，也能確保資料一致性。
					}
					//將從資料庫查回來的現有資料，轉換成一個 HashSet（雜湊集）。
					//最後過濾掉已存在的資料
					var saveData = incoming
						.Where(x => !existingKeys.Contains((DateHelper.ParseRocDate(x.TransDate), x.CropCode, x.MarketCode, x.TcType)))
						.Select(MapToEntity)
						.ToList();

					if (saveData.Any())
					{
						dbMarket.AgriProductsTrans.AddRange(saveData);
					}
					_logger.LogInformation("[SyncAgriProductsTransAsync] 成功抓取 共 {Count} 筆資料", saveData.Count);
					//注意：這裡不需要立刻呼叫 SaveChangesAsync，因為同一天的資料還有其他市場的，等全部市場的資料都準備好後再一起寫入資料庫，這樣效率更好，也能確保資料一致性。
				}
				// 當日所有市場處理完畢，一次性提交資料庫更改 (原子性操作)
				lastSyncState.LastSyncedDate = currentDate;
				lastSyncState.UpdatedAt = DateTime.UtcNow;
				await dbMarket.SaveChangesAsync(stoppingToken); // 先把 AgriProductsTrans 的新增寫入資料庫，確保資料已經存在了
				await dbCore.SaveChangesAsync(stoppingToken);
				_logger.LogInformation("{Date} 同步完成", currentDate);
			}
		}

		private AgriProductsTrans MapToEntity(AgriProductsTransTypeDto dto)
		{
			return new AgriProductsTrans
			{
				TransDate = DateHelper.ParseRocDate(dto.TransDate),
				TcType = dto.TcType,
				CropCode = dto.CropCode,
				MarketCode = dto.MarketCode,
				UpperPrice = dto.UpperPrice,
				MiddlePrice = dto.MiddlePrice,
				LowerPrice = dto.LowerPrice,
				AvgPrice = dto.AvgPrice,
				TransQuantity = dto.TransQty,
				CreatedAt = DateTime.UtcNow
			};
		}
	}
}
