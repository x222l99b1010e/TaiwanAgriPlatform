using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TaiwanAgri.Core.Constants;
using TaiwanAgri.Modules.Market.Data;
using TaiwanAgri.Modules.Market.Dtos.WorkerResponses;
using TaiwanAgri.Modules.Market.Entities;
using TaiwanAgri.Worker;

namespace TaiwanAgri.Worker.Market
{
	public class CropMarketSyncWorker : ScheduledSyncWorkerBase
	{
		private readonly ILogger<CropMarketSyncWorker> _logger;
		private readonly HttpClient _httpClient;
		private readonly IServiceScopeFactory _scopeFactory;
		public CropMarketSyncWorker(ILogger<CropMarketSyncWorker> logger, IHttpClientFactory httpClientFactory, IServiceScopeFactory scopeFactory)
			: base(logger)
		{
			_logger = logger;
			_httpClient = httpClientFactory.CreateClient("MoaApi");
			_scopeFactory = scopeFactory;
		}
		protected override TimeSpan Interval => TimeSpan.FromDays(14); // 每14天執行一次
		protected override string LogPrefix => "[CropMarketSync]";

		protected override async Task SyncAsync(CancellationToken stoppingToken)
		{
			// 使用範圍工廠來取得 DbContext，確保每次執行都有新的 DbContext 實例
			using var scope = _scopeFactory.CreateScope();
			var db = scope.ServiceProvider.GetRequiredService<MarketDbContext>();

			if (!await db.MarketInfos.AnyAsync(m => m.MarketCode == "105" && m.MarketName == "台北市場"))
			{
				db.MarketInfos.Add(new MarketInfo
				{
					MarketCode = "105",
					MarketName = "台北市場",
					MarketType = "Flower",
					CreatedAt = DateTime.UtcNow,
					UpdatedAt = DateTime.UtcNow
				});
				await db.SaveChangesAsync(stoppingToken);
			}

			var existingMarketCodes = await db.MarketInfos
				.Select(m => new ValueTuple<string, string> ( m.MarketCode, m.MarketName ))
				.ToHashSetAsync();

			var url = MoaApiEndpoints.CropMarketType;
			var marketTypes = new[] { "Veg", "Fruit", "Flower" };
			var totalNewCount = 0;
			foreach (var item in marketTypes)
			{
				var newURL = $"{url}{item}";
				var json = await _httpClient.GetStringAsync(newURL, stoppingToken);
				var response = JsonSerializer.Deserialize<CropMarketTypeApiResponse>(json);

				if (response?.RS != "OK" || response.Data.Count == 0)
				{
					_logger.LogWarning("[CropMarketSync] API 回傳異常或無資料: {item}類型", item);
					continue;
				}
				_logger.LogInformation("[CropMarketSync] 成功取得 {item}類型資料，共 {count} 筆", item, response.Data.Count);
				var resData = response.Data;
				//這邊不用allDtos.AddRange(response.Data)，第二次迴圈（Fruit）時，allDtos 裡面已經有 Veg 的資料了。
				//然後 incoming 從 allDtos 全部 select，全部帶入 item = "Fruit"—— 最後所有市場類型都被標記成 Flower 了。
				//所以改成直接從 response.Data select，這樣每次迴圈就只會處理當前類型的資料。
				//allDtos 這個累積 List 在這個 Worker 裡根本不需要——每次迴圈只需要處理當前這隻 API 的資料，不需要跨迴圈累積。
				//所以直接把 response.Data 拿來用就好，allDtos 可以整個移除。
				var incoming = resData
					.Select(dto => MapToEntity(dto, item))
					.ToHashSet();

				var toAdd = incoming
					.Where(m => !existingMarketCodes.Contains((m.MarketCode, m.MarketName)) 
					&& !(m.MarketCode?.Trim() == "105"&& m.MarketName?.Trim() == "台北花市"))
					.ToList();
				if (toAdd.Count == 0)
				{
					_logger.LogInformation("[CropMarketSync] 無新資料需寫入（全部已存在）");
					continue;
				}
				totalNewCount += toAdd.Count;
				await db.MarketInfos.AddRangeAsync(toAdd, stoppingToken);
				foreach (var m in toAdd)
				{
					existingMarketCodes.Add((m.MarketCode, m.MarketName));
				}
			}
			await db.SaveChangesAsync(stoppingToken);
			_logger.LogInformation("[CropMarketSync] 同步完成，共新增 {count} 筆資料", totalNewCount);
		}

		private MarketInfo MapToEntity(CropMarketTypeDto dto, string marketType)
		{
			return new MarketInfo
			{
				MarketCode = dto.MarketCode,
				MarketName = dto.MarketName,
				MarketType = marketType,
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			};
		}
	}
}
