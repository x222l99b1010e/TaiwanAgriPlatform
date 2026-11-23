using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
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
		private readonly ILogger<MarketService> _logger;
		public MarketService(MarketDbContext context, IDistributedCache cache, IOptions<MarketQueryOptions> options, TimeProvider timeProvider, ILogger<MarketService> logger)
		{
			_context = context;
			_cache = cache;
			_options = options.Value;
			_timeProvider = timeProvider;
			_logger = logger;
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
			DateOnly finalStart = startDate ?? DateHelper.DaysBefore(finalEnd, 365);

			// 類別一律看交易資料自己的 TcType（Veg→N04、Fruit→N05、Flower→N06），不看市場主檔的類別：
			// 主檔以（代碼、名稱）為鍵，同時賣蔬菜與水果的市場只記在先同步的蔬菜底下，
			// 原本用主檔篩，水果只剩 3 個市場、只算到 6–8% 的交易（2026-11 總檢查 3-A2）。
			// 作物清單（GetCropsAsync）早就是這樣做，這裡與市場清單當時沒有跟著改
			var tcType = MarketTypeMapping.ToTcType(marketType);
			if (tcType == null)
				return new List<PriceResponseDto>();

			// 2. 組裝 Cache Key（cropCodes 排序確保任意排列命中同一 slot）
			var cacheKey = BuildPricesCacheKey(marketType, cropCodes, marketCode, finalStart, finalEnd);

			// 3. Cache-Aside：先查快取，沒有才查資料庫、查完寫回（快取失敗時怎麼降級見 GetOrLoadCachedAsync）
			return await GetOrLoadCachedAsync(
				cacheKey,
				() => QueryPricesAsync(tcType, cropCodes, marketCode, finalStart, finalEnd, cancellationToken),
				cancellationToken);
		}

		/// <summary>
		/// 行情價格的資料庫查詢（GetPricesAsync 的快取沒命中時才走到這裡）。
		/// AgriProductsTrans JOIN CropInfos（取作物名稱）。類別用交易資料的 TcType 篩，
		/// 不 JOIN 市場主檔（理由見 GetPricesAsync 的 tcType 那段）
		/// </summary>
		private async Task<List<PriceResponseDto>> QueryPricesAsync(
			string tcType, string[] cropCodes, string? marketCode,
			DateOnly finalStart, DateOnly finalEnd, CancellationToken cancellationToken)
		{
			var baseQuery = from t in _context.AgriProductsTrans
							join c in _context.CropInfos
								on t.CropCode equals c.CropCode
							where t.TransDate >= finalStart
							   && t.TransDate <= finalEnd
							   && t.TcType == tcType
							   && cropCodes.Contains(t.CropCode)
							select new { t, c.CropName };

			// 動態追加市場過濾（marketCode 為 null 時查所有市場）
			if (!string.IsNullOrEmpty(marketCode))
			{
				baseQuery = baseQuery.Where(x => x.t.MarketCode == marketCode);
			}

			// GroupBy + 聚合：同一天同一作物跨市場取 AVG 價格、SUM 數量
			return await baseQuery
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
		}

		/// <summary>
		/// 快取的有效期限 25 小時：農業部資料每天同步一次（昨天的歷史資料），25 小時確保跨天不提早過期。
		/// 快取只靠這個期限過期——Worker 同步完會發 RabbitMQ 事件，但 Web 這一側的 PriceUpdatedConsumer
		/// 收到之後只記一則記錄、沒有清快取（README 已知限制「價格更新事件的訊息佇列只是骨架」、SA/SD §12.47 的否決條件），
		/// 所以同步完之後最多 25 小時內，看到的可能還是同步之前的結果
		/// </summary>
		private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(25);

		/// <summary>
		/// Cache-Aside 的讀與寫：先查快取，命中就回；沒命中（或快取出問題）才呼叫 <paramref name="load"/> 查資料庫，查完寫回。
		/// 行情價格與作物清單共用（作物清單是 2026-11 總檢查 G-C2 才加的快取，抽出來讓兩支走同一套降級規則）。
		/// <para>
		/// 讀與寫的任何失敗都不能往上拋，兩種失敗的理由不同但結論相同：
		/// ・連線層失敗——快取是加速層、資料庫才是真相，所以快取不可用時正確的行為是「慢一點」而不是「壞掉」。
		///   不接住的話整支查詢會跟著倒，而且 StackExchange.Redis 會把指令排進 backlog 等到逾時，使用者要先等數秒才看到錯誤
		/// ・反序列化失敗——回應的 DTO 改欄位後，舊部署留在 Redis 的 payload 會讓每個請求都炸掉，
		///   而且要等 25 小時 TTL 到期才會自己好
		/// 兩者都當成沒命中、落回資料庫查詢並覆寫該 key，是唯一能自我修復的處理方式。
		/// 寫入失敗更不能拋：結果已經查到手上了，為了「沒能存進快取」讓整支查詢失敗，是拿加速層的問題去砸掉已經成功的查詢。
		/// </para>
		/// <para>
		/// 接 Exception 而不是 RedisConnectionException：本模組只認識 IDistributedCache 這個抽象，
		/// 為了接特定例外型別而加 Redis 套件參考，等於把「底下是哪個實作」漏進不該知道的這一層；是哪一種失敗由記錄裡的例外型別分辨。
		/// 唯一放行的是請求本身被取消（使用者關掉頁面）：那不是快取的問題，記成「快取讀取失敗」會誤導，往上拋由全域例外處理收掉
		/// </para>
		/// </summary>
		private async Task<T> GetOrLoadCachedAsync<T>(string cacheKey, Func<Task<T>> load, CancellationToken cancellationToken)
			where T : class
		{
			try
			{
				var cached = await _cache.GetStringAsync(cacheKey, cancellationToken);
				if (cached != null)
				{
					var hit = JsonSerializer.Deserialize<T>(cached);
					if (hit != null)
						return hit;
				}
			}
			catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
			{
				_logger.LogWarning(ex, "[Market] 快取讀取失敗，改查資料庫：{CacheKey}", cacheKey);
			}

			var result = await load();

			try
			{
				await _cache.SetStringAsync(
					cacheKey,
					JsonSerializer.Serialize(result),
					new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl },
					cancellationToken);
			}
			catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
			{
				_logger.LogWarning(ex, "[Market] 快取寫入失敗，本次結果不進快取：{CacheKey}", cacheKey);
			}

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

			// 分組要含類別：同一場豪雨同一天常同時有土石流（D）與大規模崩塌（L）兩種警戒
			// （2026-11 本機資料 38 組「事件＋日期」裡 23 組兩種都有），原本只取第一筆的類別，
			// 另一種的縣市被併進來、類別也是隨機的
			var items = groupedRaw
				.GroupBy(d => new { d.DisasterName, d.AlertDate, d.AlertType })
				.Select(g => new DisasterResponseDto
				{
					DisasterName = g.Key.DisasterName,
					AlertType = g.Key.AlertType,
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

		/// <summary>
		/// 某個類別（蔬菜／水果／花卉）有交易過的作物，給下拉選單與作物自動完成用。
		/// <para>
		/// 有快取（2026-11 總檢查 G-C2）：第一段要對整張交易表做 DISTINCT（本機 2026-11 量：執行計畫是平行掃描整個唯一索引、
		/// 744.8 萬列，每次 CPU 2.2–3.1 秒），農場設定頁一進來就查三次（三個類別各一次）。
		/// 結果跟誰在看無關、只有三種條件、只隨每天的同步改變，符合 SA/SD §12.47 的三條快取判準，
		/// 也沒有「寫入後要立刻讀到」的否決條件
		/// </para>
		/// </summary>
		public async Task<List<CropResponseDto>> GetCropsAsync(string marketType, CancellationToken cancellationToken = default)
		{
			//1. MarketType 轉 TcType
			var tcType = MarketTypeMapping.ToTcType(marketType);
			if (tcType == null)
				return new List<CropResponseDto>();

			return await GetOrLoadCachedAsync(
				CacheKeys.MarketCropsPrefix + tcType,
				() => QueryCropsAsync(tcType, cancellationToken),
				cancellationToken);
		}

		/// <summary>作物清單的資料庫查詢（GetCropsAsync 的快取沒命中時才走到這裡）</summary>
		private async Task<List<CropResponseDto>> QueryCropsAsync(string tcType, CancellationToken cancellationToken)
		{
			// 兩段式的 SQL 是固定的兩條獨立查詢，不會互相依賴，效能穩定。
			// Step 1：先從 AgriProductsTrans 撈出該 TcType 下所有出現過的 CropCode
			//         翻譯為：SELECT DISTINCT CropCode FROM AgriProductsTrans WHERE TcType = 'N04'（蔬菜；水果 N05、花卉 N06）
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

		/// <summary>
		/// 某個類別（蔬菜／水果／花卉）近一年實際有交易的市場。
		/// <para>
		/// 「這個類別有哪些市場」看交易資料自己的類別（TcType），不看市場主檔的類別：主檔以（代碼、名稱）為鍵，
		/// 同時賣蔬菜與水果的市場只記在先同步的蔬菜底下，原本水果下拉只有 3 個市場，
		/// 交易量前四大的市場都選不到（2026-11 總檢查 3-A2）。
		/// </para>
		/// <para>
		/// 只看近一年：已經停止交易的舊市場代碼不列（2026-11 本機資料：水果歷來 17 個代碼、近一年 13 個）；
		/// 日期條件也讓查詢走 (TransDate, TcType, …) 索引的範圍，不必掃整張交易表（本機實測整表 1.2 秒、近一年 0.09 秒）
		/// </para>
		/// </summary>
		public async Task<List<MarketResponseDto>> GetMarketsAsync(string marketType, CancellationToken cancellationToken = default)
		{
			var tcType = MarketTypeMapping.ToTcType(marketType);
			if (tcType == null)
				return new List<MarketResponseDto>();

			var since = TaiwanTime.Today(_timeProvider).AddDays(-365);
			var codes = await _context.AgriProductsTrans
				.Where(t => t.TransDate >= since && t.TcType == tcType)
				.Select(t => t.MarketCode)
				.Distinct()
				.ToListAsync(cancellationToken);

			// 名稱從主檔取。同一個代碼在主檔可能有兩筆（例如 400 是蔬菜的「台中市」與花卉的「台中市場」，
			// 兩個市場共用代碼）：優先取同類別的那一筆，沒有就取蔬菜的，再沒有就依名稱取第一筆。
			// 主檔裡找不到名稱的代碼不列——下拉選單上只有一串數字沒有用
			var infos = await _context.MarketInfos
				.AsNoTracking()
				.Where(m => codes.Contains(m.MarketCode))
				.Select(m => new { m.MarketCode, m.MarketName, m.MarketType })
				.ToListAsync(cancellationToken);

			return infos
				.GroupBy(m => m.MarketCode)
				.Select(g => g
					.OrderBy(m => m.MarketType == marketType ? 0 : m.MarketType == "Veg" ? 1 : 2)
					.ThenBy(m => m.MarketName, StringComparer.Ordinal)
					.First())
				.OrderBy(m => m.MarketCode, StringComparer.Ordinal)
				.Select(m => new MarketResponseDto
				{
					MarketCode = m.MarketCode,
					MarketName = m.MarketName
				})
				.ToList();
		}

		public async Task<List<RestDayResponseDto>> GetRestDaysAsync(string marketCode, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
		{
			// ── Step 1：SQL 階段 ──────────────────────────────────────────
			// MarketRestDays 用民國年/月/日三欄儲存，
			// EF Core 無法在 SQL 層將三欄組合為 DateOnly 再做精確範圍比較，
			// 但可以先用民國「年」粗篩，把撈回記憶體的筆數從全表縮到查詢區間附近
			//
			// 只取蔬菜（V）那一份：同一個市場代碼底下，蔬菜、水果、花卉（V／F／L）各有一份休市表。
			// 原本全部一起回，同一天被算兩三次（台北一 2026 年統計卡 158 天、月曆實際 79 天），
			// 共用代碼的花卉市場（400 台中市場）的休市日也會標到蔬菜市場的月曆上（2026-11 總檢查 3-A3）。
			// 休市日頁的市場清單本來就是蔬菜市場（前端 getMarkets('Veg')），所以取 V
			var rocYearStart = startDate.Year - 1911;
			var rocYearEnd = endDate.Year - 1911;
			var records = await _context.MarketRestDays
				.AsNoTracking()
				.Where(r => r.MarketCode == marketCode
						 && r.MarketType == RestDayVegType
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
				// 去重與排序不能省：唯一鍵含類別，只靠上面的類別條件保證不重複太脆弱；
				// 統計卡的天數就是這份清單的長度
				.Distinct()
				.OrderBy(d => d)
				.Select(d => new RestDayResponseDto { RestDate = d })
				.ToList();
		}

		/// <summary>休市日資料表裡「蔬菜」的類別代碼（另有 F 水果、L 花卉）</summary>
		private const string RestDayVegType = "V";

		public async Task<List<PorkResponseDto>> GetPorkAsync(string? marketName = null, DateOnly? startDate = null, DateOnly? endDate = null, CancellationToken cancellationToken = default)
		{
			DateOnly finalEnd = endDate ?? TaiwanTime.Today(_timeProvider);
			DateOnly finalStart = startDate ?? DateHelper.DaysBefore(finalEnd, 365);

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
			DateOnly finalStart = startDate ?? DateHelper.DaysBefore(finalEnd, 365);

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

			// DisplayName 在記憶體端補上：PoultryMetrics.DisplayNames 是 C# 字典，資料庫裡沒有它。
			// 寫進上面的 Select 不行——TryGetValue 的 out 變數在查詢運算式裡編不過，
			// 改用索引子則會被 EF 以「投影引用了字典實例」為由在執行期拒絕
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
		/// 不能在分組裡巢狀 g.Max：EF 會照翻成 AVG 裡套 MAX，但 SQL Server 不接受聚合函式巢狀；
		/// 也不該逐作物查一次（那是 N+1）。
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
