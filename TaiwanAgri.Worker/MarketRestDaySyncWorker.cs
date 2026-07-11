using System.Text.Json;
using TaiwanAgri.Core.Constants;
using TaiwanAgri.Modules.Market.Data;
using TaiwanAgri.Modules.Market.Dtos.WorkerResponses;
using TaiwanAgri.Modules.Market.Entities;

namespace TaiwanAgri.Worker
{
	public class MarketRestDaySyncWorker : ScheduledSyncWorkerBase
	{
		private readonly ILogger<MarketRestDaySyncWorker> _logger;
		private readonly HttpClient _httpClient;
		private readonly IServiceScopeFactory _scopeFactory;

		public MarketRestDaySyncWorker(ILogger<MarketRestDaySyncWorker> logger, IHttpClientFactory httpClientFactory, IServiceScopeFactory scopeFactory)
			: base(logger)
		{
			_logger = logger;
			_httpClient = httpClientFactory.CreateClient("MoaApi");
			_scopeFactory = scopeFactory;
		}
		protected override TimeSpan Interval => TimeSpan.FromDays(7); // 正式排程每7天一次
		protected override string LogPrefix => "[MarketRestDaySync]";

		protected override async Task SyncAsync(CancellationToken stoppingToken)
		{
			using var scope = _scopeFactory.CreateScope();
			var db = scope.ServiceProvider.GetRequiredService<MarketDbContext>();

			var allDtos = new List<MarketRestDayDto>();
			int page = 1;
			while (true)
			{
				var url = (page == 1)? MoaApiEndpoints.MarketRestDay : $"{MoaApiEndpoints.MarketRestDay}?page={page}";
				var json = await _httpClient.GetStringAsync(url, stoppingToken);
				var response = JsonSerializer.Deserialize<MarketRestDayApiResponseDto>(json);

				if (response?.RS != "OK" || response.Data.Count == 0)
				{
					if (page == 1)
						{
						_logger.LogWarning("[MarketRestDaySync] API回應異常或無資料，停止同步");
					}
					else
					{
						_logger.LogInformation("[MarketRestDaySync] 第 {Page} 頁無資料或無分頁權限，停止抓取", page);
					}
					break;
				}
				_logger.LogInformation("[MarketRestDaySync] 成功抓取第 {Page} 頁， 共 {Count} 筆資料", page, response.Data.Count);
				allDtos.AddRange(response.Data);
				if(!response.Next) //如果API回傳沒有下一頁，則停止抓取
					break;
				page++; //如果Next == true，繼續抓取下一頁
				if (page > 20)
				{
					_logger.LogWarning("[MarketRestDaySync] 已達分頁上限（20頁），停止繼續抓取");
					break;
				}
			}
			var entities = new List<MarketRestDay>();
			foreach (var market in allDtos)
			{
				foreach (var type in market.MarketTypeList)
				{
					foreach (var year in type.YearList)
					{
						foreach (var month in year.MonthList)
						{
							string[] splitRestDay = month.RestDay.Split('、');
							foreach (var dayStr in splitRestDay)
							{
								if(!int.TryParse(dayStr, out int day)) continue;

								entities.Add(new MarketRestDay
								{
									MarketCode = market.MarketCode,
									MarketName = market.MarketName,
									MarketType = type.Type,
									Year = year.Year,
									Month = month.Month,
									RestDay = day,
									CreatedAt = DateTime.UtcNow
								});
							}
						}
					}
				}
			}
			// 撈出資料庫已存在的自然鍵
			var existingKeys = db.MarketRestDays
				.Select(r => new { r.MarketCode, r.MarketType, r.Year, r.Month, r.RestDay })
				.ToHashSet();
			// 篩選出API資料中不存在於資料庫的紀錄，也就是要寫回資料庫的部分
			var toInsert = entities
				.Where(e => !existingKeys.Contains(new { e.MarketCode, e.MarketType, e.Year, e.Month, e.RestDay }))
				.ToList();

			if (toInsert.Count == 0)
			{
				_logger.LogInformation("[MarketRestDaySync] 無新資料需要同步");
				return;
			}
			db.MarketRestDays.AddRange(toInsert);
			await db.SaveChangesAsync(stoppingToken);
			_logger.LogInformation("[MarketRestDaySync] 新增 {Count} 筆休市日資料", toInsert.Count);

		}
	}
}