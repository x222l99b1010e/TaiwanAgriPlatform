using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using TaiwanAgri.Modules.Market.Constants;
using TaiwanAgri.Modules.Market.Data;
using TaiwanAgri.Modules.Market.Dtos.ApiResponses;
using TaiwanAgri.Core.Helpers;
using Microsoft.Extensions.Configuration;

namespace TaiwanAgri.Modules.Market.Services
{
	public class MarketService : IMarketService
	{
		private readonly MarketDbContext _context;
		private readonly IDistributedCache _cache;
		private readonly IConfiguration _configuration;
		public MarketService(MarketDbContext context, IDistributedCache cache, IConfiguration configuration)
		{
			_context = context;
			_cache = cache;
			_configuration = configuration;
		}
		public async Task<DateOnly?> GetLatestTransDateAsync(string marketCode)
		{
			var latest = await _context.AgriProductsTrans
				.Where(t => t.MarketCode == marketCode)
				.OrderByDescending(t => t.TransDate)
				.Select(t => (DateOnly?)t.TransDate)
				.FirstOrDefaultAsync();

			return latest;
		}
		public async Task<List<PriceResponseDto>> GetPricesAsync(
			string marketType,
			string[] cropCodes,
			string? marketCode = null,
			DateOnly? startDate = null,
			DateOnly? endDate = null)
		{
			// 1. 日期解析與預設值（先解析，cache key 才能用實際日期）
			DateOnly finalEnd = endDate ?? DateOnly.FromDateTime(DateTime.Today);
			DateOnly finalStart = startDate ?? finalEnd.AddDays(-365);

			// 2. 組裝 Cache Key（cropCodes 排序確保任意排列命中同一 slot）
			var cacheKey = BuildPricesCacheKey(marketType, cropCodes, marketCode, finalStart, finalEnd);

			// 3. Cache-Aside Step 1：查 Redis
			//    命中（Hit）→ 直接反序列化回傳，跳過 DB 查詢
			var cached = await _cache.GetStringAsync(cacheKey);
			if (cached != null)
				return JsonSerializer.Deserialize<List<PriceResponseDto>>(cached)!;

			// 4. Cache-Aside Step 2：Redis Miss，查 SQL
			//    三表 JOIN：AgriProductsTrans + CropInfos + MarketInfos
			var baseQuery = from t in _context.AgriProductsTrans
							join c in _context.CropInfos
								on t.CropCode equals c.CropCode
							join m in _context.MarketInfos
								on t.MarketCode equals m.MarketCode
							where t.TransDate >= finalStart
							   && t.TransDate <= finalEnd
							   && m.MarketType == marketType
							   && cropCodes.Contains(t.CropCode)
							select new { t, c.CropName };

			// 5. 動態追加市場過濾（marketCode 為 null 時查所有市場）
			if (!string.IsNullOrEmpty(marketCode))
			{
				baseQuery = baseQuery.Where(x => x.t.MarketCode == marketCode);
			}

			// 6. GroupBy + 聚合：同一天同一作物跨市場取 AVG 價格、SUM 數量
			var result = await baseQuery
				.GroupBy(x => new { x.t.TransDate, x.t.CropCode, x.CropName })
				.Select(g => new PriceResponseDto
				{
					TransDate = g.Key.TransDate,
					CropCode = g.Key.CropCode,
					CropName = g.Key.CropName,
					UpperPrice = g.Average(x => x.t.UpperPrice),
					MiddlePrice = g.Average(x => x.t.MiddlePrice),
					LowerPrice = g.Average(x => x.t.LowerPrice),
					AvgPrice = g.Average(x => x.t.AvgPrice),
					TransQuantity = g.Sum(x => x.t.TransQuantity)
				})
				.OrderByDescending(x => x.TransDate)
				.ToListAsync();

			// 7. Cache-Aside Step 3：結果寫進 Redis，TTL 25 小時
			//    農業部資料每天更新一次（昨天的歷史資料），25 小時確保跨天不提早過期
			//    TTL 是保底機制；Worker 同步完成後會透過 RabbitMQ 主動 invalidation
			var cacheOptions = new DistributedCacheEntryOptions
			{
				AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(25)
			};
			await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(result), cacheOptions);

			return result;
		}
		public async Task<List<DisasterResponseDto>> GetDisastersAsync(
			string[] counties,
			DateOnly startDate,
			DateOnly endDate)
		{
			DateTime startDateTime = startDate.ToDateTime(TimeOnly.MinValue);
			DateTime endDateTime = endDate.ToDateTime(TimeOnly.MaxValue);

			var query = _context.DebrisAlertRecords
				.Where(d => d.LastUpdateDate >= startDateTime && d.LastUpdateDate <= endDateTime);

			// 為了避免一次撈出超過 10 萬筆資料導致 OutOfMemory，先設定一個合理的上限
			var limit = _configuration.GetValue<int>("MarketQueryLimits:DisasterRecordLimit", 5000);

			if (counties != null && counties.Any())
				query = query.Where(d => counties.Contains(d.County));

			// 先撈出去，再在記憶體 GroupBy 去重
			// 同一天同一個災害可能有幾百筆（每個村落一筆），前端只需要唯一事件
			// Take 前必須有 OrderBy，否則 TOP(n) 取哪幾筆不確定，
			// 超量截斷時 AffectedCounties 會不完整且每次查詢結果不同
			var groupedRaw = await query
				.OrderByDescending(d => d.LastUpdateDate)
				.Select(d => new {
					d.DisasterName,
					d.AlertType,
					d.County,
					AlertDate = DateOnly.FromDateTime(d.LastUpdateDate)
				})
				.Take(limit)
				.ToListAsync();

			return groupedRaw
				.GroupBy(d => new { d.DisasterName, d.AlertDate })
				.Select(g => new DisasterResponseDto
				{
					DisasterName = g.Key.DisasterName,
					AlertType = g.First().AlertType,
					AlertDate = g.Key.AlertDate.ToString("yyyy-MM-dd"),
					AffectedCounties = g.Select(x => x.County)
										.Distinct()
										.OrderBy(c => c)
										.ToList()
				})
				.OrderBy(d => d.AlertDate)
				.ToList();
		}
		public async Task<List<CropResponseDto>> GetCropsAsync(string marketType)
		{
			//1. MarketType 轉 TcType
			var tcType = MarketTypeMapping.ToTcType(marketType);
			if (tcType == null)
				return new List<CropResponseDto>();

			// 兩段式的 SQL 是固定的兩條獨立查詢，不會互相依賴，效能穩定。
			// Step 1：先從 AgriProductsTrans 撈出該 TcType 下所有出現過的 CropCode
			//         翻譯為：SELECT DISTINCT CropCode FROM AgriProductsTrans WHERE TcType = 'V'
			var validCropCodes = await _context.AgriProductsTrans
				.Where(a => a.TcType == tcType)
				.Select(a => a.CropCode)
				.Distinct()
				.ToListAsync();

			// Step 2：再用 validCropCodes 清單過濾 CropInfos
			//         翻譯為：SELECT CropCode, CropName FROM CropInfos WHERE CropName != '' AND CropCode IN (...)
			return await _context.CropInfos
				.Where(c => c.CropName != "" && validCropCodes.Contains(c.CropCode))
				.Select(c => new CropResponseDto
				{
					CropCode = c.CropCode,
					CropName = c.CropName
				})
				.Distinct()
				.ToListAsync();

			//2. 查 CropInfos，條件是 CropName 不為空，且 CropCode 在 AgriProductsTrans 的 TcType 對應市場類型中有出現過
			//var crops = await _context.CropInfos
			//	.Where(c => c.CropName != "" &&
			//				_context.AgriProductsTrans
			//					.Where(a => a.TcType == tcType)
			//					.Select(a => a.CropCode)
			//					.Contains(c.CropCode))
			//	.Select(c => new CropResponseDto
			//	{
			//		CropCode = c.CropCode,
			//		CropName = c.CropName
			//	})
			//	.Distinct()
			//	.ToListAsync();

			//return crops;
		}

		public async Task<List<MarketResponseDto>> GetMarketsAsync(string marketType)
		{
			return await _context.MarketInfos
				.Where(m => m.MarketType == marketType)
				//MarketResponseDto 的 Select 只做一件事——把欄位值搬過去。
				//SELECT MarketCode, MarketName FROM MarketInfos WHERE MarketType = 'Veg'
				//SQL 完全理解「取這兩個欄位的值」。
				.Select(m => new MarketResponseDto
				{
					MarketCode = m.MarketCode,
					MarketName = m.MarketName
				})
				.ToListAsync();
		}

		public async Task<List<RestDayResponseDto>> GetRestDaysAsync(string marketCode, DateOnly startDate, DateOnly endDate)
		{
			// ── Step 1：SQL 階段 ──────────────────────────────────────────
			// MarketRestDays 用民國年/月/日三欄儲存，
			// EF Core 無法在 SQL 層將三欄組合為 DateOnly 再做精確範圍比較，
			// 但可以先用民國「年」粗篩，把撈回記憶體的筆數從全表縮到查詢區間附近
			var rocYearStart = startDate.Year - 1911;
			var rocYearEnd = endDate.Year - 1911;
			var records = await _context.MarketRestDays
				.Where(r => r.MarketCode == marketCode
						 && r.Year >= rocYearStart
						 && r.Year <= rocYearEnd)
				.ToListAsync();

			// ── Step 2：記憶體階段 ────────────────────────────────────────
			// 民國年三欄 → 西元 DateOnly → 篩日期範圍 → 組 DTO
			return records
				.Select(r => DateHelper.ConvertRocRestDay(r.Year, r.Month, r.RestDay))
				.Where(d => d.HasValue)
				.Select(d => d!.Value)
				.Where(d => d >= startDate && d <= endDate)
				.Select(d => new RestDayResponseDto { RestDate = d })
				.ToList();
		}

		public async Task<List<PorkResponseDto>> GetPorkAsync(string? marketName = null, DateOnly? startDate = null, DateOnly? endDate = null)
		{
			DateOnly finalEnd = endDate ?? DateOnly.FromDateTime(DateTime.Today);
			DateOnly finalStart = startDate ?? finalEnd.AddDays(-365);

			var porkList = await _context.PorkTrans
				.Where(p => p.TransDate >= finalStart && p.TransDate <= finalEnd)
				.Where(pm => marketName == null || pm.MarketName == marketName)
				.Select(pm => new PorkResponseDto
				{
					TransDate = pm.TransDate,
					MarketName = pm.MarketName,
					ExcludeFreezerAvgPrice = pm.ExcludeFreezerAvgPrice,
					ExcludeFreezerAvgWeight = pm.ExcludeFreezerAvgWeight,
					ExcludeFreezerCount = pm.ExcludeFreezerCount
				})
				.OrderByDescending(pm => pm.TransDate)
				.ToListAsync();

			return porkList;
		}

		public async Task<List<LatestPriceDto>> GetLatestPricesAsync(
			IEnumerable<(string CropCode, string MarketCode)> keys)
		{
			var keyList = keys.Distinct().ToList();
			if (keyList.Count == 0)
				return new List<LatestPriceDto>();

			var cropCodes = keyList.Select(k => k.CropCode).Distinct().ToList();
			var marketCodes = keyList.Select(k => k.MarketCode).Distinct().ToList();

			// SQL 端先用兩個 IN 縮小範圍（可能多撈到交叉組合，最後再精準過濾），
			// GroupBy + 每組取最新一筆，一次查詢取代逐組查詢
			var latest = await _context.AgriProductsTrans
				.Where(t => cropCodes.Contains(t.CropCode) && marketCodes.Contains(t.MarketCode))
				.GroupBy(t => new { t.CropCode, t.MarketCode })
				.Select(g => g
					.OrderByDescending(t => t.TransDate)
					.Select(t => new LatestPriceDto
					{
						CropCode = t.CropCode,
						MarketCode = t.MarketCode,
						TransDate = t.TransDate,
						AvgPrice = t.AvgPrice
					})
					.First())
				.ToListAsync();

			// 移除 IN 交叉組合多撈到、但呼叫端沒要求的配對
			var requested = keyList.ToHashSet();
			return latest
				.Where(x => requested.Contains((x.CropCode, x.MarketCode)))
				.ToList();
		}

		/// <summary>
		/// 組裝 GetPricesAsync 的 Redis Cache Key。
		/// cropCodes 排序後 Join，確保 ["A01","B02"] 和 ["B02","A01"] 命中同一個 cache。
		/// 使用 finalStart / finalEnd（已解析的實際日期），防止 null 預設值碰撞到同一個 Key。
		/// 格式：market:prices:{marketType}:{sortedCrops}:{marketCode}:{startDate}:{endDate}
		/// </summary>
		/// <param name="marketType"></param>
		/// <param name="cropCodes"></param>
		/// <param name="marketCode"></param>
		/// <param name="finalStart"></param>
		/// <param name="finalEnd"></param>
		/// <returns></returns>
		private static string BuildPricesCacheKey(
				string marketType,
				string[] cropCodes,
				string? marketCode,
				DateOnly finalStart,
				DateOnly finalEnd)
		{
			var sortedCrops = string.Join(",", cropCodes.OrderBy(c => c));
			//return $"market:prices:{marketType}:{sortedCrops}:{marketCode ?? ""}:{finalStart}:{finalEnd}";
			return $"{CacheKeys.MarketPricesPrefix}{marketType}:{sortedCrops}:{marketCode ?? ""}:{finalStart}:{finalEnd}";
		}
	}
}
