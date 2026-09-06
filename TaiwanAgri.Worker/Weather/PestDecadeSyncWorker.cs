using System.Text.Json;
using TaiwanAgri.Core.Constants;
using TaiwanAgri.Modules.Weather.Entities;
using TaiwanAgri.Modules.Weather.Data;
using Microsoft.EntityFrameworkCore;
using TaiwanAgri.Modules.Weather.Dtos.WorkerResponses;
using TaiwanAgri.Worker;

namespace TaiwanAgri.Worker.Weather
{
	public class PestDecadeSyncWorker : ScheduledSyncWorkerBase
	{
		private readonly ILogger<PestDecadeSyncWorker> _logger;
		private readonly HttpClient _httpClient;
		private readonly IServiceScopeFactory _scopeFactory;
		public PestDecadeSyncWorker(ILogger<PestDecadeSyncWorker> logger, IHttpClientFactory httpClientFactory, IServiceScopeFactory scopeFactory)
			: base(logger)
		{
			_logger = logger;
			_httpClient = httpClientFactory.CreateClient("MoaApi");
			_scopeFactory = scopeFactory;
		}

		protected override TimeSpan Interval => TimeSpan.FromDays(10); // 正式排程每10天1次
		protected override string LogPrefix => "[PestDecadeSync]";

		protected override async Task SyncAsync(CancellationToken stoppingToken)
		{
			using var scope = _scopeFactory.CreateScope();
			var db = scope.ServiceProvider.GetRequiredService<WeatherDbContext>();

			var allDtos = new List<PestDecadeSummaryDto>();
			int page = 1;
			while (true)
			{
				var url = (page == 1) ? MoaApiEndpoints.FruitPestControl : $"{MoaApiEndpoints.FruitPestControl}?page={page}";
				var json = await _httpClient.GetStringAsync(url, stoppingToken);
				var response = JsonSerializer.Deserialize<PestDecadeSummaryApiResponse>(json);

				if ( response?.RS != "OK" || response.Data.Count == 0 )
				{
					if (page == 1)
						_logger.LogWarning("[PestDecadeSync] API 回傳異常或無資料");
					else
						_logger.LogInformation("[PestDecadeSync] 第 {page} 頁無資料或無分頁權限，停止抓取", page);
					break;
				}
				_logger.LogInformation("[PestDecadeSync] 第 {Page} 頁取得 {Count} 筆資料", page, response.Data.Count);
				allDtos.AddRange(response.Data);
				if (!response.Next)
					break;
				page++;

				if (page > 20)
				{
					_logger.LogWarning("[PestDecadeSync] 已達分頁上限（20頁），停止繼續抓取");
					break;
				}
			}
			_logger.LogInformation("[PestDecadeSync] 合計取得 {Count} 筆原始資料", allDtos.Count);

			//先從DTO 轉換成 entity，並檢查回傳資料為空的情況
			var incoming = allDtos
				.Select(MapToEntity)
				.Where(e => e != null)
				.Cast<PestDecadeSummary>()
				.DistinctBy(e => new { e.PestName, e.Year, e.Month, e.TenDays, e.City, e.Town })
				.ToList();

			if (incoming.Count == 0)
			{
				_logger.LogInformation("[PestDecadeSync] API 回傳資料為空，略過本次同步");
				return;
			}

			// 1. 取得資料庫現有的 Key 集合
			var existingHashset = (await db.PestDecadeSummaries
				.Select(e => e.PestName + "_" + e.Year + "_" + e.Month + "_" + e.TenDays + "_" + e.City + "_" + e.Town)
				.ToListAsync(stoppingToken))
				.ToHashSet();

			// 2. 篩選出「資料庫還不存在」的項目
			var toAdd = incoming
				.Where(a =>
				{
					// 產生當前物件的 Unique Key
					var key = $"{a.PestName}_{a.Year}_{a.Month}_{a.TenDays}_{a.City}_{a.Town}";
					// 檢查這個 Key 是否不在現有的集合中
					return !existingHashset.Contains(key);
				})
				.ToList();

			if (toAdd.Count == 0)
			{
				_logger.LogInformation("[PestDecadeSync] 無新資料需寫入（全部已存在）");
				return;
			}

			await db.PestDecadeSummaries.AddRangeAsync(toAdd, stoppingToken);
			await db.SaveChangesAsync(stoppingToken);
			_logger.LogInformation("[PestDecadeSync] 成功寫入 {Count} 筆，略過 {Skipped} 筆重複", toAdd.Count, incoming.Count - toAdd.Count);

		}

		/// <summary>
		/// 外部 DTO → entity。無法解析或缺必要欄位時回傳 null，由呼叫端過濾掉
		/// （回傳型別標成可空才與這個行為一致）。
		/// PestName／City／Town 三者都是 entity 的 NOT NULL 欄位，缺任一都必須在這裡擋下來——
		/// 放行的話會等到 SaveChanges 才以資料庫例外爆開，整批一起失敗
		/// </summary>
		private PestDecadeSummary? MapToEntity(PestDecadeSummaryDto dto)
		{
			if (!int.TryParse(dto.Year, out var year)) return null;
			if (!int.TryParse(dto.Month, out var month)) return null;
			if (!int.TryParse(dto.Decade, out var tenDays)) return null;
			if (string.IsNullOrWhiteSpace(dto.PestName)
				|| string.IsNullOrWhiteSpace(dto.City)
				|| string.IsNullOrWhiteSpace(dto.Town))
			{
				_logger.LogWarning("[PestDecadeSync] 略過缺少害蟲名稱/縣市/鄉鎮欄位的資料：PestName={PestName}, Year={Year}, Month={Month}, TenDays={TenDays}, City={City}, Town={Town}",
					dto.PestName, dto.Year, dto.Month, dto.Decade, dto.City, dto.Town);
				return null;
			}

			return new PestDecadeSummary
			{
				PestName = dto.PestName,
				Year = year,
				Month = month,
				TenDays = tenDays,
				City = dto.City,
				Town = dto.Town,
				Average = ParseDecimal(dto.Average),
				ProportionIsland = ParseDecimal(dto.ProportionIsland),
				CreatedAt = DateTime.UtcNow
			};
		}

		private static decimal? ParseDecimal(string? s) 
		=>	decimal.TryParse(s,System.Globalization.NumberStyles.Any, 
			System.Globalization.CultureInfo.InvariantCulture ,out var v)
			? v : null;

		private static int ParseInt(string s)
			=> int.TryParse(s, out var v) ? v : 0;
	}
}
