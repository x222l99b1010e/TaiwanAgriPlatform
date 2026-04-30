using Azure;
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

			var lastSyncState = await dbCore.SyncStates
				.FirstOrDefaultAsync(s => s.SyncKey == "Market_AgriProductsTrans", cancellationToken: stoppingToken);
			// 從上次同步的下一天開始同步
			if (lastSyncState == null)
			{
				// 情況一：資料不存在，建立新的
				var firstState = new SyncState
				{
					SyncKey = "Market_AgriProductsTrans",
					LastSyncedDate = new DateOnly(2018, 06, 30),					
					UpdatedAt = DateTime.UtcNow
				};

				dbCore.SyncStates.Add(firstState);
				await dbCore.SaveChangesAsync(stoppingToken);
				lastSyncState = firstState;
			}
			DateOnly startDate = lastSyncState.LastSyncedDate.AddDays(1);

			// 1. 取得台灣時區 (台北標準時間)
			// 注意：在 Windows 上是 "Taipei Standard Time"，在 Linux/macOS 上通常是 "Asia/Taipei"
			string tzId = OperatingSystem.IsWindows()
							? "Taipei Standard Time"
							: "Asia/Taipei";
			TimeZoneInfo taipeiZone = TimeZoneInfo.FindSystemTimeZoneById(tzId);

			// 2. 取得目前的台灣時間 (先抓 UTC 再轉換，這最準確)
			DateTime taipeiNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, taipeiZone);

			// 3. 取得「昨天」
			DateTime taipeiYesterday = taipeiNow.AddDays(-1);

			// 4. 轉換成 DateOnly (如果你資料庫存的是 DateOnly)
			DateOnly yesterdayDate = DateOnly.FromDateTime(taipeiYesterday);

			//從 dbMarket 撈所有 MarketInfo
			var marketInfos = await dbMarket.MarketInfos.ToListAsync(stoppingToken);
			//var startTimeUrl = $"?Start_time=";
			//var endTimeUrl = $"&End_time=";
			//var marketNameUrl = $"&MarketName=";
			for (DateOnly currentDate = startDate; currentDate <= yesterdayDate; currentDate = currentDate.AddDays(1))
			{
				//在這裡（每天重置）
				foreach (var market in marketInfos)
				{
					var url = $"{MoaApiEndpoints.AgriProductsTrans}?Start_time={DateHelper.FormatRocDate(currentDate)}&End_time={DateHelper.FormatRocDate(currentDate)}&MarketName={market.MarketName}";
					var json = await _httpClient.GetStringAsync(url, stoppingToken);
					var response = JsonSerializer.Deserialize<AgriProductsTransTypeApiResponse>(json);

					if (response?.RS != "OK")
					{						
						_logger.LogWarning("[SyncAgriProductsTransAsync] API回應異常或無資料，停止同步");
						break;
					}
					else if (response.Data == null || response.Data.Count == 0)
					{
						_logger.LogInformation("[SyncAgriProductsTransAsync] API回應成功但無資料，繼續下一筆");
						continue;
					}
					var resData = response.Data;
					//過濾休市（CropCode == "-"）
					var incoming = resData
						.Where(x => x.CropCode != "-")
						.ToList();
					//抽出Crop資料要存入資料庫
					var cropCodes = incoming
						.DistinctBy(x => x.CropCode)
						.ToList();
					var existingCropCodes = await dbMarket.CropInfos
						.Where(x => cropCodes.Select(c => c.CropCode).Contains(x.CropCode))
						.Select(x => x.CropCode)
						.ToListAsync(stoppingToken);
					var newCropCodes = cropCodes
						.Where(x => !existingCropCodes.Contains(x.CropCode))
						.Select(x => new CropInfo
						{
							CropCode = x.CropCode,
							CropName = x.CropName,
							CreatedAt = DateTime.UtcNow
						})
						.ToList();
					if (newCropCodes.Count > 0)
					{
						_logger.LogInformation("[SyncAgriProductsTransAsync] 發現 {Count} 筆新作物代碼，正在新增 CropInfo 資料", newCropCodes.Count);
						dbMarket.CropInfos.AddRange(newCropCodes);
						await dbMarket.SaveChangesAsync(stoppingToken);
					}

					//去重
					//先針對抓回來的資料去重
					// 注意：這裡用 .ToList() 而不是 .ToHashSet()
					// .ToHashSet() 對 DTO 物件使用參考相等（reference equality）來判斷重複，
					// 也就是比較記憶體位址，而不是欄位內容。
					// 每筆 DTO 都是從 JSON 反序列化出來的獨立物件，位址各不相同，
					// 即使內容完全一樣，.ToHashSet() 也不會去重，加在這裡沒有意義。
					// 去重的工作已經由 .DistinctBy() 根據欄位內容完成，直接 .ToList() 即可。
					var targetData = incoming
						.DistinctBy(x => new { x.TransDate, x.CropCode, x.MarketCode, x.TcType }) // 根據日期、作物代碼、市場代碼去重複
						.ToList();
					//接著從DB撈出當天資料做比對
					// 資料表結構有 TransDate、CropCode、MarketCode、TcType 這幾個欄位，根據這些欄位來比對是否已經存在(不需要全撈出來)
					var existingKeys = await dbMarket.AgriProductsTrans
						.Where(x => x.TransDate == currentDate)
						.Select(x => new { x.TransDate, x.CropCode, x.MarketCode, x.TcType })
						.ToListAsync(stoppingToken);
					//將從資料庫查回來的現有資料，轉換成一個 HashSet（雜湊集）。
					var existingKeySet = existingKeys
						.Select(x => (x.TransDate, x.CropCode, x.MarketCode, x.TcType))
						.ToHashSet();
					//最後過濾掉已存在的資料
					var newData = targetData
						.Where(x => !existingKeySet.Contains((DateHelper.ParseRocDate(x.TransDate), x.CropCode, x.MarketCode, x.TcType)))
						.ToList();

					var saveData = newData
						.Select(MapToEntity)
						.ToList();

					_logger.LogInformation("[SyncAgriProductsTransAsync] 成功抓取 共 {Count} 筆資料", saveData.Count);
					dbMarket.AgriProductsTrans.AddRange(saveData);
					await dbMarket.SaveChangesAsync(stoppingToken);
				}
				// 這一天所有市場都跑完 → 寫入 DB → 更新 SyncState
				lastSyncState.LastSyncedDate = currentDate;
				lastSyncState.UpdatedAt = DateTime.UtcNow;
				await dbCore.SaveChangesAsync(stoppingToken);
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
