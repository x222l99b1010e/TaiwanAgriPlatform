using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TaiwanAgri.Modules.Weather.Data;
using TaiwanAgri.Modules.Weather.Entities;

namespace TaiwanAgri.Modules.Weather.Services
{
	/// <summary>一次評估的結果。手動觸發的端點靠它回答「為什麼沒有新通知」。</summary>
	/// <param name="RulesEvaluated">實際被評估的規則數</param>
	/// <param name="NotificationsCreated">新產生的通知數</param>
	/// <param name="LatestObservedAt">氣象觀測表裡最新一筆的觀測時刻；null 代表表裡沒有資料。
	/// 呼叫端拿它與現在時刻相減，就能分辨「條件沒命中」與「資料不夠新」這兩種空結果</param>
	public sealed record RuleEvaluationOutcome(int RulesEvaluated, int NotificationsCreated, DateTime? LatestObservedAt);

	public class PestRuleEngine
	{
		/// <summary>
		/// 觀測資料的新鮮度上限：超過這個歲數的觀測不產生通知。
		/// 正常運作下永遠碰不到（觀測是幾分鐘前的），它擋的是同步中斷後才被評估到的陳舊資料
		/// ——「臺中市 19 天前氣溫 32 度」對使用者沒有任何行動價值。
		/// 之所以不設得更緊：開發與錄影環境的同步 Worker 未必一直開著，設一兩天會讓正常測試也產不出通知，
		/// 而那不是這道防護要解決的問題。
		/// </summary>
		private static readonly TimeSpan FreshnessWindow = TimeSpan.FromDays(7);

		/// <summary>
		/// 規則第一次評估時，水位往回退的幅度：只看最後一批同步進來的觀測。
		/// 一批約 876 筆（一站一筆）且 SyncedAt 是逐列取值的，實測單批寫入跨度約 112 秒，
		/// 所以緩衝必須大於它；同時必須小於同步間隔（1 小時），否則會把前一批也撈進來。
		/// 15 分鐘對兩邊都留了數量級的餘裕。
		/// </summary>
		private static readonly TimeSpan FirstRunLookback = TimeSpan.FromMinutes(15);

		private readonly ILogger<PestRuleEngine> _logger;
		private readonly IServiceScopeFactory _scopeFactory;

		public PestRuleEngine(ILogger<PestRuleEngine> logger, IServiceScopeFactory scopeFactory)
		{
			_logger = logger;
			_scopeFactory = scopeFactory;
		}

		// 已知效能債：規則引擎逐條規則各查一次 DB（N+1）。目前規則數量與觸發頻率下尚未構成瓶頸，
		// 未排程優化，待實際負載出現效能問題再處理。

		/// <summary>
		/// 評估規則並產生通知。
		/// </summary>
		/// <param name="userId">只評估這個使用者的規則；null 代表評估全系統（排程 Worker 用）。
		/// 手動觸發的端點一定要帶值——一個使用者按按鈕不該讓別人的規則跟著跑，
		/// 而且逐條規則的例外會離開整支方法，限定範圍等於把爆炸半徑收在呼叫者自己身上。</param>
		public async Task<RuleEvaluationOutcome> EvaluateAsync(string? userId, CancellationToken cancellationToken)
		{
			using var scope = _scopeFactory.CreateScope();
			var db = scope.ServiceProvider.GetRequiredService<WeatherDbContext>();
			var now = DateTime.UtcNow;

			// 刪除已過期的通知。這裡先載進記憶體再 RemoveRange，看起來可以換成 ExecuteDelete
			// 直接下一句 DELETE，但測試用的 InMemory 提供者不支援 ExecuteDelete——
			// 為此要把整套測試基礎設施換成 SQLite，而過期通知的量級（每人最多七條規則、
			// 每條每天數則、最長保留半年）根本不構成問題。優化與代價不成比例，維持現狀
			var expired = await db.UserNotifications
				.Where(n => n.ExpireAt != null && n.ExpireAt < now)
				.ToListAsync(cancellationToken);
			db.UserNotifications.RemoveRange(expired);
			await db.SaveChangesAsync(cancellationToken);

			// 這裡要追蹤，因為數值型規則評估完要回寫水位
			var activeRules = await db.PestRuleConfigs
				.Where(p => p.IsActive && (userId == null || p.UserId == userId))
				.OrderBy(p => p.Id)   // 沒有 OrderBy 的話回傳順序沒有承諾，例外時「誰被餓死」會隨機變動
				.ToListAsync(cancellationToken);

			// 整輪共用同一個掃描上界：評估期間才落地的觀測留給下一輪，
			// 否則水位推過去之後那些列就永遠不會被看到
			var scanUpTo = await db.WeatherObservations
				.MaxAsync(w => (DateTime?)w.SyncedAt, cancellationToken);
			var latestObservedAt = await db.WeatherObservations
				.MaxAsync(w => (DateTime?)w.ObservedAt, cancellationToken);

			var created = 0;
			foreach (var rule in activeRules)
			{
				switch (rule.RuleType)
				{
					case "Numeric":
						created += await EvaluateNumericAsync(db, rule, now, scanUpTo, cancellationToken);
						break;
					case "Event":
						created += await EvaluateEventAsync(db, rule, now, cancellationToken);
						break;
					default:
						_logger.LogWarning("[PestRuleEngine] 未知的 RuleType: {RuleType}", rule.RuleType);
						break;
				}
			}

			await db.SaveChangesAsync(cancellationToken);
			return new RuleEvaluationOutcome(activeRules.Count, created, latestObservedAt);
		}

		/// <summary>
		/// 數值型：比對自動氣象站觀測。
		/// 原本的來源是病蟲害旬報，但該來源的旬平均值上游未提供值（實測我方 136 筆與上游單頁 500 筆
		/// 全部為 null），任何門檻都不可能成立，因此改接氣象觀測。
		/// </summary>
		private async Task<int> EvaluateNumericAsync(
			WeatherDbContext db, PestRuleConfig rule, DateTime now, DateTime? scanUpTo, CancellationToken cancellationToken)
		{
			// 深度防禦：API 已擋掉不合法的 SourceTable，這裡再擋一次繞過 API 直接寫進資料庫的路徑。
			// 舊資料可能留著 "PestDecade"，那個來源已判定不適用
			if (rule.SourceTable != "WeatherObservation")
			{
				_logger.LogWarning("[PestRuleEngine] 規則 {RuleId} 的 SourceTable 為 {SourceTable}，數值型只支援 WeatherObservation，跳過",
					rule.Id, rule.SourceTable);
				return 0;
			}
			if (rule.Threshold == null || scanUpTo == null)
			{
				_logger.LogWarning("[PestRuleEngine] 規則 {RuleId} 的 Threshold 為 null 或氣象觀測表為空，跳過", rule.Id);
				return 0;
			}

			// 水位：只看上次評估之後才落地的觀測。第一次評估（水位為 null）退到最後一批，
			// 這樣建完規則立刻評估就有東西可看，又不會把整張表的歷史一次全變成通知
			var watermark = rule.LastEvaluatedAt ?? scanUpTo.Value - FirstRunLookback;
			var freshnessCutoff = now - FreshnessWindow;

			var query = db.WeatherObservations
				.AsNoTracking()
				.Where(w => w.SyncedAt > watermark && w.SyncedAt <= scanUpTo.Value
						 && w.ObservedAt >= freshnessCutoff);

			query = ApplyCityFilter(query, rule.FilterCity);

			// 觀測項目與方向的組合逐個列出，而不是動態組運算式：組合只有四種，
			// 列出來的話不認得的值會落到 default 被擋下，動態組則會在執行期才炸
			var threshold = rule.Threshold.Value;
			var filtered = (rule.MetricName, rule.Comparison) switch
			{
				("Temperature", "GreaterThan") => query.Where(w => w.Temperature > threshold),
				("Temperature", "LessThan") => query.Where(w => w.Temperature < threshold),
				("Rainfall24h", "GreaterThan") => query.Where(w => w.Rainfall24h > threshold),
				("Rainfall24h", "LessThan") => query.Where(w => w.Rainfall24h < threshold),
				_ => null
			};
			if (filtered == null)
			{
				_logger.LogWarning("[PestRuleEngine] 規則 {RuleId} 的 MetricName/Comparison 組合不合法（{Metric}/{Comparison}），跳過",
					rule.Id, rule.MetricName, rule.Comparison);
				return 0;
			}

			var matched = await filtered.ToListAsync(cancellationToken);
			var created = 0;
			foreach (var item in matched)
			{
				if (await NotificationExistsAsync(db, rule.Id, item.Id, cancellationToken))
					continue;

				var value = rule.MetricName == "Temperature" ? item.Temperature : item.Rainfall24h;
				db.UserNotifications.Add(new UserNotification
				{
					UserId = rule.UserId,
					PestRuleConfigId = rule.Id,
					SourceRecordId = item.Id,
					Message = BuildNumericMessage(rule, item, value),
					TriggeredAt = now,
					ExpireAt = now.AddDays(rule.ExpiryDays)
				});
				created++;
			}

			// 水位推到本輪的掃描上界，不是「命中資料的最大落地時刻」——沒命中的那些也已經看過了
			rule.LastEvaluatedAt = scanUpTo.Value;
			_logger.LogInformation("[PestRuleEngine] 規則 {RuleId}（數值型）比對 {Matched} 筆、新增 {Created} 則通知",
				rule.Id, matched.Count, created);
			return created;
		}

		/// <summary>事件型：比對植物疫情警報。</summary>
		private async Task<int> EvaluateEventAsync(
			WeatherDbContext db, PestRuleConfig rule, DateTime now, CancellationToken cancellationToken)
		{
			if (rule.SourceTable != "PlantEpidemic")
			{
				// "TreePest" 會落在這裡：該來源沒有時間戳也沒有唯一識別欄位，探勘後判定不適用，
				// 規則建立時已不開放。這個分支是給資料庫裡可能留著的歷史列用的
				_logger.LogWarning("[PestRuleEngine] 規則 {RuleId} 的 SourceTable 為 {SourceTable}，事件型只支援 PlantEpidemic，跳過",
					rule.Id, rule.SourceTable);
				return 0;
			}

			var query = db.PestAlerts.AsNoTracking().AsQueryable();

			if (!string.IsNullOrWhiteSpace(rule.FilterCity))
			{
				// 縣市用相等比對（值來自前端固定清單），台／臺兩種寫法都試
				var (primary, alternate) = CityNameVariants(rule.FilterCity);
				query = query.Where(p => p.Cities.Any(c => c.CityName == primary || c.CityName == alternate));
			}
			if (!string.IsNullOrWhiteSpace(rule.FilterPlantName))
			{
				// 作物用包含比對而非相等：上游的作物名是自由字串（「廣東檸檬」「無子檸檬」「其他橙類」），
				// 使用者輸入「檸檬」應該要命中這一整族
				var plantName = rule.FilterPlantName;
				query = query.Where(p => p.Crops.Any(c => c.CropName.Contains(plantName)));
			}
			if (rule.FilterDateFrom != null)
			{
				var from = rule.FilterDateFrom.Value;
				query = query.Where(p => p.PubDate >= from);
			}

			var matched = await query.ToListAsync(cancellationToken);
			var created = 0;
			foreach (var item in matched)
			{
				if (await NotificationExistsAsync(db, rule.Id, item.Id, cancellationToken))
					continue;

				db.UserNotifications.Add(new UserNotification
				{
					UserId = rule.UserId,
					PestRuleConfigId = rule.Id,
					SourceRecordId = item.Id,
					Message = $"{item.PubDate:yyyy-MM-dd} 植物疫情警報｜{item.Subject}",
					TriggeredAt = now,
					ExpireAt = now.AddDays(rule.ExpiryDays)
				});
				created++;
			}

			_logger.LogInformation("[PestRuleEngine] 規則 {RuleId}（事件型）比對 {Matched} 筆、新增 {Created} 則通知",
				rule.Id, matched.Count, created);
			return created;
		}

		/// <summary>
		/// 同一條規則對同一筆來源資料只通知一次。
		/// 水位擋的是「這輪要看哪些資料」，這裡擋的是「這一筆我發過沒有」——兩者防的東西不同：
		/// 水位若因為評估中途失敗而沒有回寫，下一輪會重掃同一批，那時就靠這裡兜底。
		/// </summary>
		private static Task<bool> NotificationExistsAsync(WeatherDbContext db, int ruleId, int sourceRecordId, CancellationToken cancellationToken)
			=> db.UserNotifications.AnyAsync(
				n => n.PestRuleConfigId == ruleId && n.SourceRecordId == sourceRecordId, cancellationToken);

		/// <summary>
		/// 縣市名稱的台／臺兩種寫法。疫情那側的來源全部用「台」、氣象那側全部用「臺」，
		/// 而使用者存的只有一種，不兩種都試就會靜默比不到任何一筆（不會報錯）。
		/// 候選值在這裡先算好再進 Where，翻成 SQL 是 IN (@p0, @p1)；
		/// 若寫成 w.CityName.Replace(...) 會翻成 SQL 的 REPLACE()，欄位被包在函式裡就用不到索引。
		/// </summary>
		private static (string Primary, string Alternate) CityNameVariants(string cityName)
			=> (cityName, cityName.Contains('臺') ? cityName.Replace('臺', '台') : cityName.Replace('台', '臺'));

		private static IQueryable<WeatherObservation> ApplyCityFilter(IQueryable<WeatherObservation> query, string? cityName)
		{
			if (string.IsNullOrWhiteSpace(cityName))
				return query;
			var (primary, alternate) = CityNameVariants(cityName);
			return query.Where(w => w.CityName == primary || w.CityName == alternate);
		}

		/// <summary>
		/// 通知訊息在觸發當下組好存成字串，之後不再改變。
		/// 這是刻意的：氣象觀測是 30 天滾動刪除，來源列日後必然不存在，訊息若改成顯示時即時組裝，
		/// 舊通知就會顯示不出來。訊息要自己講完「什麼、哪裡、什麼時候、多少」，不依賴任何外部資料。
		/// 規則名稱不塞進字串，由前端從導覽屬性取——規則改名時通知不該跟著變，那是已經發生過的事。
		/// </summary>
		private static string BuildNumericMessage(PestRuleConfig rule, WeatherObservation item, decimal? value)
		{
			var metricLabel = rule.MetricName == "Temperature" ? "氣溫" : "24 小時雨量";
			var unit = rule.MetricName == "Temperature" ? "°C" : "mm";
			var direction = rule.Comparison == "LessThan" ? "低於" : "超過";
			var place = string.IsNullOrWhiteSpace(item.TownName) ? item.CityName : $"{item.CityName}{item.TownName}";
			return $"{place}｜{item.ObservedAt:yyyy-MM-dd HH:mm}｜{metricLabel} {value}{unit}，{direction}門檻 {rule.Threshold}{unit}";
		}
	}
}
