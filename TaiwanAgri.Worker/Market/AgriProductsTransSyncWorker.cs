using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;
using TaiwanAgri.Core.Constants;
using TaiwanAgri.Core.Helpers;
using TaiwanAgri.Core.Infrastructure.Data;
using TaiwanAgri.Core.Infrastructure.Entities;
using TaiwanAgri.Modules.Market.Data;
using TaiwanAgri.Modules.Market.Dtos.WorkerResponses;
using TaiwanAgri.Modules.Market.Entities;
using TaiwanAgri.Worker;

namespace TaiwanAgri.Worker.Market
{
	public class AgriProductsTransSyncWorker : ScheduledSyncWorkerBase
	{
		private readonly ILogger<AgriProductsTransSyncWorker> _logger;
		private readonly HttpClient _httpClient;
		private readonly IServiceScopeFactory _serviceScopeFactory;
		private readonly IConfiguration _configuration;
		public AgriProductsTransSyncWorker(ILogger<AgriProductsTransSyncWorker> logger, IHttpClientFactory httpClientFactory, IServiceScopeFactory serviceScopeFactory, IConfiguration configuration)
			: base(logger)
		{
			_logger = logger;
			_httpClient = httpClientFactory.CreateClient("MoaApi");
			_serviceScopeFactory = serviceScopeFactory;
			_configuration = configuration;
		}
		protected override TimeSpan Interval => TimeSpan.FromDays(1); // 每天執行一次
		protected override string LogPrefix => "[AgriProductsTransSyncWorker]";

		protected override async Task SyncAsync(CancellationToken stoppingToken)
		{
			var success = await SyncAgriProductsTransAsync(stoppingToken);
			if (success)
				await PublishPriceUpdatedEventAsync();
		}

		private async Task PublishPriceUpdatedEventAsync()
		{
			var factory = new ConnectionFactory
			{
				HostName = _configuration["RabbitMQ:HostName"] ?? "localhost"
			};
			await using var connection = await factory.CreateConnectionAsync();
			await using var channel = await connection.CreateChannelAsync();

			// 宣告 topic exchange（不存在會自動建立，已存在則確認設定一致）
			await channel.ExchangeDeclareAsync(
				exchange: "agri.events",
				type: ExchangeType.Topic,
				durable: true);

			// 發布訊息
			var body = Encoding.UTF8.GetBytes("{}"); // 骨架階段，payload 暫時空 JSON
			await channel.BasicPublishAsync(
				exchange: "agri.events",
				routingKey: "agri.market.priceUpdated",
				body: body);

			_logger.LogInformation("[AgriProductsTransSyncWorker] 已發布 agri.market.priceUpdated 事件");
		}

		private async Task<bool> SyncAgriProductsTransAsync(CancellationToken stoppingToken)
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
			var semaphore = new SemaphoreSlim(3); // 控制併發數量，避免過度壓垮 API 或資料庫
			for (DateOnly currentDate = startDate; currentDate <= yesterdayDate; currentDate = currentDate.AddDays(1))
			{
				_logger.LogInformation("--- 開始同步日期: {Date} ---", currentDate);
				// 併發抓取所有市場 API 請求，減少網路等待時間
				var rawResults = await Task.WhenAll(marketInfos.Select(async market =>
				{
					// 1. 控制併發數量，避免過度壓垮 API 或資料庫
					// 改後：semaphore 等待用 default，不要跟 stoppingToken 綁
					await semaphore.WaitAsync(CancellationToken.None);
					// 2. 抓取 API 資料，並捕捉可能的例外（例如網路問題、API 異常等），確保即使某個市場失敗也不會影響整體流程
					try
					{
						var url = $"{MoaApiEndpoints.AgriProductsTrans}?Start_time={DateHelper.FormatRocDate(currentDate)}&End_time={DateHelper.FormatRocDate(currentDate)}&MarketName={market.MarketName}";
						// 改後：HTTP 請求用獨立的 timeout token，不跟 stoppingToken 綁
						//using var httpTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
						var httpTimeoutSeconds = _configuration.GetValue<int>("AgriProductsSyncWorker:HttpTimeoutSeconds", 90);
						using var httpTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(httpTimeoutSeconds));
						var json = await _httpClient.GetStringAsync(url, httpTimeoutCts.Token);
						return (Market: market, Json: json, Success: true);
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "市場 {Market} 抓取失敗", market.MarketName);
						return (Market: market, Json: string.Empty, Success: false);
					}
					finally
					{
						semaphore.Release();
					}
				}));
				var failedMarkets = rawResults.Where(r => !r.Success).Select(r => r.Market.MarketName).ToList();
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
				// foreach 之前
				var allIncoming = new List<AgriProductsTransTypeDto>();
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
					// 只收集，不直接 AddRange
					var incoming = response.Data
						.Where(x => x.CropCode != "-")
						.ToList();
					allIncoming.AddRange(incoming);
				}

				// 2. 處理新發現的作物代碼 (CropInfo)
				var newCrops = allIncoming
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

				// foreach 結束後，對所有市場的資料合併去重
				var targetData = allIncoming
					.DistinctBy(x => new { x.TransDate, x.CropCode, x.MarketCode, x.TcType })
					.ToList();
				//將從資料庫查回來的現有資料，轉換成一個 HashSet（雜湊集）。
				//最後過濾掉已存在的資料
				var saveData = targetData
					.Where(x => !existingKeys.Contains((DateHelper.ParseRocDate(x.TransDate), x.CropCode, x.MarketCode, x.TcType)))
					.Select(MapToEntity)
					.ToList();

				if (saveData.Any())
				{
					dbMarket.AgriProductsTrans.AddRange(saveData);
				}
				_logger.LogInformation("[SyncAgriProductsTransAsync] 成功抓取 共 {Count} 筆資料", saveData.Count);
				//注意：這裡不需要立刻呼叫 SaveChangesAsync，因為同一天的資料還有其他市場的，等全部市場的資料都準備好後再一起寫入資料庫，這樣效率更好，也能確保資料一致性。

				// ★ 改動：只有全部市場成功才推進 LastSyncedDate
				
				if (failedMarkets.Any())
				{
					// 注意：dbCore 不 SaveChanges，LastSyncedDate 不推進 => 下次還是會從同一天開始嘗試，直到成功為止
					_logger.LogWarning("{Date} 有 {Count} 個市場失敗：{Markets}，LastSyncedDate 維持不更新，下次將重新嘗試此日",
						currentDate, failedMarkets.Count, string.Join(", ", failedMarkets));
					// 成功的那幾筆還是要存
					await dbMarket.SaveChangesAsync(stoppingToken);

					//ToDo: 先不強制推進，等未來十座發出警告通知再處理
					// ★ 安全閥：如果這天已經超過 5 天還是有失敗，強制推進並記錄缺口
					//var daysBehind = yesterdayDate.DayNumber - currentDate.DayNumber;
					//if (daysBehind >= 5)
					//{
					//	_logger.LogWarning("{Date} 已落後 {Days} 天仍有失敗市場，強制推進 LastSyncedDate，資料存在缺口",
					//		currentDate, daysBehind);
					//	lastSyncState.LastSyncedDate = currentDate;
					//	lastSyncState.UpdatedAt = DateTime.UtcNow;
					//	await dbCore.SaveChangesAsync(stoppingToken);
					//}

					// 這裡不推進，先直接 return，停止這輪同步
					return false; // 有失敗就停，不繼續跑後面的天
				}

				// 當日所有市場處理完畢，一次性提交資料庫更改 (原子性操作)
				lastSyncState.LastSyncedDate = currentDate;
				lastSyncState.UpdatedAt = DateTime.UtcNow;
				await dbMarket.SaveChangesAsync(stoppingToken); // 先把 AgriProductsTrans 的新增寫入資料庫，確保資料已經存在了
				await dbCore.SaveChangesAsync(stoppingToken);
				_logger.LogInformation("{Date} 同步完成", currentDate);
			}
			// for 迴圈正常跑完（全部天都成功）才到這裡
			return true;
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
