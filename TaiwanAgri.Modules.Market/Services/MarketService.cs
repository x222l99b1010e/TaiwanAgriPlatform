using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using TaiwanAgri.Modules.Market.Constants;
using TaiwanAgri.Modules.Market.Data;
using TaiwanAgri.Modules.Market.Dtos.ApiResponses;
using TaiwanAgri.Core.Helpers;
using Microsoft.Extensions.Options;

namespace TaiwanAgri.Modules.Market.Services
{
	public class MarketService : IMarketService
	{
		private readonly MarketDbContext _context;
		private readonly IDistributedCache _cache;
		private readonly MarketQueryOptions _options;
		private readonly TimeProvider _timeProvider;
		public MarketService(MarketDbContext context, IDistributedCache cache, IOptions<MarketQueryOptions> options, TimeProvider timeProvider)
		{
			_context = context;
			_cache = cache;
			_options = options.Value;
			_timeProvider = timeProvider;
		}
		public async Task<DateOnly?> GetLatestTransDateAsync(string marketCode, CancellationToken cancellationToken = default)
		{
			var latest = await _context.AgriProductsTrans
				.Where(t => t.MarketCode == marketCode)
				.OrderByDescending(t => t.TransDate)
				.Select(t => (DateOnly?)t.TransDate)
				.FirstOrDefaultAsync(cancellationToken);

			return latest;
		}
		public async Task<List<PriceResponseDto>> GetPricesAsync(
			string marketType,
			string[] cropCodes,
			string? marketCode = null,
			DateOnly? startDate = null,
			DateOnly? endDate = null, CancellationToken cancellationToken = default)
		{
			// 1. 日期解析與預設值（先解析，cache key 才能用實際日期）
			//    預設查到「今天」＝台灣時區日界（DateTime.Today 是主機本地時區，部署在 UTC 環境會差一天）
			DateOnly finalEnd = endDate ?? TaiwanTime.Today(_timeProvider);
			DateOnly finalStart = startDate ?? finalEnd.AddDays(-365);

			// 2. 組裝 Cache Key（cropCodes 排序確保任意排列命中同一 slot）
			var cacheKey = BuildPricesCacheKey(marketType, cropCodes, marketCode, finalStart, finalEnd);

			// 3. Cache-Aside Step 1：查 Redis
			//    命中（Hit）→ 直接反序列化回傳，跳過 DB 查詢
			//    反序列化失敗不能往上拋：PriceResponseDto 改欄位後，舊部署留在 Redis 的 payload
			//    會讓每個請求都炸掉，而且要等 25 小時 TTL 到期才會自己好。當成 miss 落回 DB
			//    查詢並覆寫該 key，是唯一能自我修復的處理方式
			var cached = await _cache.GetStringAsync(cacheKey);
			if (cached != null)
			{
				try
				{
					var hit = JsonSerializer.Deserialize<List<PriceResponseDto>>(cached);
					if (hit != null)
						return hit;
				}
				catch (JsonException)
				{
					// 落下去走 DB 查詢，並在 Step 3 覆寫這個 key
				}
			}

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
				.ToListAsync(cancellationToken);

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
		/// <summary>
		/// 天災事件查詢。回傳值第二格是「結果有沒有被上限截斷」——截斷時 AffectedCounties
		/// 會不完整，呼叫端必須讓使用者知道，否則拿到的是一份看起來完整、實際殘缺的清單
		/// （比照寵物模組地圖端點當年對截斷加訊號的做法）
		/// </summary>
		public async Task<(List<DisasterResponseDto> Items, bool IsTruncated)> GetDisastersAsync(
			string[] counties,
			DateOnly startDate,
			DateOnly endDate, CancellationToken cancellationToken = default)
		{
			DateTime startDateTime = startDate.ToDateTime(TimeOnly.MinValue);
			DateTime endDateTime = endDate.ToDateTime(TimeOnly.MaxValue);

			var query = _context.DebrisAlertRecords
				.Where(d => d.LastUpdateDate >= startDateTime && d.LastUpdateDate <= endDateTime);

			// 為了避免一次撈出超過 10 萬筆資料導致 OutOfMemory，先設定一個合理的上限
			var limit = _options.DisasterRecordLimit;

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
				.Take(limit + 1)   // 多撈一筆用來判斷有沒有被截斷，回傳前再砍掉
				.ToListAsync(cancellationToken);

			var isTruncated = groupedRaw.Count > limit;
			if (isTruncated)
				groupedRaw = groupedRaw.Take(limit).ToList();

			var items = groupedRaw
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

			return (items, isTruncated);
		}

		public async Task<List<CropResponseDto>> GetCropsAsync(string marketType, CancellationToken cancellationToken = default)
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
				.ToListAsync(cancellationToken);

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
				.ToListAsync(cancellationToken);
		}

		public async Task<List<MarketResponseDto>> GetMarketsAsync(string marketType, CancellationToken cancellationToken = default)
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
				.ToListAsync(cancellationToken);
		}

		public async Task<List<RestDayResponseDto>> GetRestDaysAsync(string marketCode, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
		{
			// ── Step 1：SQL 階段 ──────────────────────────────────────────
			// MarketRestDays 用民國年/月/日三欄儲存，
			// EF Core 無法在 SQL 層將三欄組合為 DateOnly 再做精確範圍比較，
			// 但可以先用民國「年」粗篩，把撈回記憶體的筆數從全表縮到查詢區間附近
			var rocYearStart = startDate.Year - 1911;
			var rocYearEnd = endDate.Year - 1911;
			var records = await _context.MarketRestDays
				.AsNoTracking()
				.Where(r => r.MarketCode == marketCode
						 && r.Year >= rocYearStart
						 && r.Year <= rocYearEnd)
				.ToListAsync(cancellationToken);

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

		public async Task<List<PorkResponseDto>> GetPorkAsync(string? marketName = null, DateOnly? startDate = null, DateOnly? endDate = null, CancellationToken cancellationToken = default)
		{
			DateOnly finalEnd = endDate ?? TaiwanTime.Today(_timeProvider);
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
				.ToListAsync(cancellationToken);

			return porkList;
		}

		public async Task<List<PoultryResponseDto>> GetPoultryAsync(
			string[]? metricCodes = null,
			DateOnly? startDate = null,
			DateOnly? endDate = null, CancellationToken cancellationToken = default)
		{
			// 預設區間比照 GetPorkAsync：未指定就給最近一年
			DateOnly finalEnd = endDate ?? TaiwanTime.Today(_timeProvider);
			DateOnly finalStart = startDate ?? finalEnd.AddDays(-365);

			// null 或空陣列＝不篩選指標（回傳全部 17 個）。
			// Controller 已用 PoultryMetrics.IsValid 擋掉無效代碼，這裡不重複驗證
			var filterCodes = metricCodes is { Length: > 0 } ? metricCodes : null;

			var rows = await _context.PoultryTrans
				.Where(p => p.TransDate >= finalStart && p.TransDate <= finalEnd)
				.Where(p => filterCodes == null || filterCodes.Contains(p.MetricCode))
				// 排序放在投影前、且一定要有（V30 教訓：Skip/Take 或 Take 前必加 OrderBy）。
				// 先依指標分組再依日期排，前端多線折線圖可直接依序切線，不必自己重排
				.OrderBy(p => p.MetricCode)
				.ThenBy(p => p.TransDate)
				.Select(p => new
				{
					p.TransDate,
					p.MetricCode,
					p.Price,
					p.PriceStatus,
					p.RawValue
				})
				.ToListAsync(cancellationToken);

			// DisplayName 在記憶體端補上：PoultryMetrics.DisplayNames 是 C# 字典，
			// 放進 Select 會讓 EF 無法轉譯（比照 MapToResponseDto 的教訓）
			return rows.Select(p => new PoultryResponseDto
			{
				TransDate = p.TransDate,
				MetricCode = p.MetricCode,
				DisplayName = PoultryMetrics.DisplayNames.TryGetValue(p.MetricCode, out var name)
					? name
					: p.MetricCode,
				Price = p.Price,
				PriceStatus = p.PriceStatus.ToString(),
				RawValue = p.RawValue
			}).ToList();
		}

		public async Task<List<LatestPriceDto>> GetLatestPricesAsync(
			IEnumerable<(string CropCode, string? MarketCode)> keys, CancellationToken cancellationToken = default)
		{
			var keyList = keys.Distinct().ToList();
			if (keyList.Count == 0)
				return new List<LatestPriceDto>();

			// 指定市場與不指定市場是兩種查詢，分開處理後再合併。
			// 不分開的話，未指定市場那些鍵的 MarketCode 是 null，而 SQL 的 IN 永遠不匹配 NULL，
			// 結果是那些監看項目查不到任何價格，且沒有任何錯誤訊號
			var scopedKeys = keyList.Where(k => k.MarketCode != null).ToList();
			var crossMarketCrops = keyList
				.Where(k => k.MarketCode == null)
				.Select(k => k.CropCode)
				.Distinct()
				.ToList();

			var result = new List<LatestPriceDto>();

			if (scopedKeys.Count > 0)
				result.AddRange(await GetLatestPricesForMarketsAsync(scopedKeys, cancellationToken));

			if (crossMarketCrops.Count > 0)
				result.AddRange(await GetLatestCrossMarketPricesAsync(crossMarketCrops, cancellationToken));

			return result;
		}

		/// <summary>
		/// 指定市場的最新價：SQL 端先用兩個 IN 縮小範圍（可能多撈到交叉組合），
		/// GroupBy 後每組取最新一筆，一次查詢取代逐組查詢，最後再濾掉呼叫端沒要求的配對。
		/// </summary>
		private async Task<List<LatestPriceDto>> GetLatestPricesForMarketsAsync(
			List<(string CropCode, string? MarketCode)> scopedKeys, CancellationToken cancellationToken)
		{
			var cropCodes = scopedKeys.Select(k => k.CropCode).Distinct().ToList();
			var marketCodes = scopedKeys.Select(k => k.MarketCode!).Distinct().ToList();

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
				.ToListAsync(cancellationToken);

			var requested = scopedKeys.ToHashSet();
			return latest
				.Where(x => requested.Contains((x.CropCode, x.MarketCode)))
				.ToList();
		}

		/// <summary>
		/// 不指定市場的最新價＝該作物最新交易日的跨市場均價，與 GetPricesAsync
		/// 在 marketCode 為 null 時的聚合語意一致。
		/// <para>
		/// 固定兩次查詢、不隨作物數成長：先取每個作物的最新交易日，再以其中最早的那個日期
		/// 當下界撈回這段區間的資料，在記憶體端依各作物自己的最新日過濾後平均。
		/// 不能寫成單一查詢裡的巢狀 g.Max（EF 無法翻譯），也不該逐作物查一次（那是 N+1）。
		/// </para>
		/// </summary>
		private async Task<List<LatestPriceDto>> GetLatestCrossMarketPricesAsync(
			List<string> cropCodes, CancellationToken cancellationToken)
		{
			var latestDates = await _context.AgriProductsTrans
				.Where(t => cropCodes.Contains(t.CropCode))
				.GroupBy(t => t.CropCode)
				.Select(g => new { CropCode = g.Key, LatestDate = g.Max(t => t.TransDate) })
				.ToListAsync(cancellationToken);

			if (latestDates.Count == 0)
				return new List<LatestPriceDto>();

			var earliestBound = latestDates.Min(x => x.LatestDate);
			var rows = await _context.AgriProductsTrans
				.Where(t => cropCodes.Contains(t.CropCode) && t.TransDate >= earliestBound)
				.Select(t => new { t.CropCode, t.TransDate, t.AvgPrice })
				.ToListAsync(cancellationToken);

			var latestByCrop = latestDates.ToDictionary(x => x.CropCode, x => x.LatestDate);

			return rows
				.Where(r => latestByCrop[r.CropCode] == r.TransDate)
				.GroupBy(r => r.CropCode)
				.Select(g => new LatestPriceDto
				{
					CropCode = g.Key,
					MarketCode = null,
					TransDate = latestByCrop[g.Key],
					AvgPrice = g.Average(r => r.AvgPrice)
				})
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
			return $"{CacheKeys.MarketPricesPrefix}{marketType}:{sortedCrops}:{marketCode ?? ""}:{finalStart}:{finalEnd}";
		}
	}
}
