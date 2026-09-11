using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TaiwanAgri.Modules.Weather.Data;
using TaiwanAgri.Modules.Weather.Entities;
using TaiwanAgri.Modules.Weather.Services;

namespace TaiwanAgri.Tests.Weather
{
	/// <summary>
	/// PestRuleEngine.EvaluateAsync——整條通知鏈的第一節，由 PestRuleEngineWorker 每天呼叫一次，
	/// 也由使用者手動觸發的端點呼叫（帶自己的 UserId）。
	/// 它做四件事：刪過期通知、取啟用中的規則、逐條比對來源資料產生通知、回寫數值型規則的水位。
	/// <para>
	/// 測試重點有兩處。其一是「跳過」這個行為：規則設定不完整時引擎選擇跳過而不是拋例外，
	/// 於是每一種跳過都是靜默的——使用者只會發現通知沒來，不會有任何錯誤訊號，
	/// 所以每一種跳過都要有測試釘住它確實跳過、而且不拖累其他規則。
	/// 其二是數值型的兩道範圍限制（水位與新鮮度）：兩者都是「不產生通知」的形態，
	/// 壞掉時同樣沒有任何訊號，只會表現成通知太多或太少。
	/// </para>
	/// </summary>
	public class PestRuleEngineTests
	{
		/// <summary>
		/// EvaluateAsync 自己從 IServiceScopeFactory 開 scope 取 DbContext，所以這裡建一個真的
		/// ServiceProvider 而不是假的——假的 scope factory 要串三層 Mock，比真的還脆弱。
		/// InMemory 依名稱共用資料，所以測試用同名的 DbContext 就能驗證引擎寫進去的東西
		/// </summary>
		private static (PestRuleEngine Engine, Func<WeatherDbContext> OpenDb) CreateEngine(string databaseName)
		{
			var services = new ServiceCollection();
			services.AddDbContext<WeatherDbContext>(o => o.UseInMemoryDatabase(databaseName));
			var provider = services.BuildServiceProvider();

			var engine = new PestRuleEngine(
				NullLogger<PestRuleEngine>.Instance,
				provider.GetRequiredService<IServiceScopeFactory>(),
				TimeProvider.System);

			return (engine, () => new WeatherDbContext(
				new DbContextOptionsBuilder<WeatherDbContext>().UseInMemoryDatabase(databaseName).Options));
		}

		/// <summary>
		/// 觀測的兩個時間分開給，是因為引擎拿它們回答不同的問題：
		/// SyncedAt 決定「這筆我看過了沒」（水位），ObservedAt 決定「這筆夠不夠新」（新鮮度）。
		/// 測試要能讓兩者不一致，才驗得出各自的作用
		/// </summary>
		private static WeatherObservation Observation(
			decimal? temperature, DateTime observedAt, DateTime syncedAt,
			string cityName = "臺中市", string townName = "大甲區", decimal? rainfall24h = null) => new()
			{
				StationId = "C0F9M0",
				StationName = "測試站",
				ObservedAt = observedAt,
				Temperature = temperature,
				Rainfall24h = rainfall24h,
				CityCode = "66000",
				CityName = cityName,
				TownName = townName,
				SyncedAt = syncedAt
			};

		private static PestRuleConfig NumericRule(
			decimal? threshold,
			string metricName = "Temperature",
			string comparison = "GreaterThan",
			string? filterCity = null,
			DateTime? lastEvaluatedAt = null,
			string sourceTable = "WeatherObservation",
			int expiryDays = 7,
			bool isActive = true) => new()
			{
				UserId = "u1",
				RuleName = "高溫警戒",
				RuleType = "Numeric",
				SourceTable = sourceTable,
				IsActive = isActive,
				Threshold = threshold,
				MetricName = metricName,
				Comparison = comparison,
				FilterCity = filterCity,
				LastEvaluatedAt = lastEvaluatedAt,
				ExpiryDays = expiryDays
			};

		private static PestRuleConfig EventRule(
			string? city, string? plantName, DateOnly? dateFrom = null,
			string sourceTable = "PlantEpidemic", bool isActive = true) => new()
			{
				UserId = "u1",
				RuleName = "疫情警報",
				RuleType = "Event",
				SourceTable = sourceTable,
				IsActive = isActive,
				ExpiryDays = 14,
				FilterCity = city,
				FilterPlantName = plantName,
				FilterDateFrom = dateFrom
			};

		private static PestAlert Alert(string subject, string city, string crop, DateOnly? pubDate = null) => new()
		{
			Subject = subject,
			Body = "內文",
			Prescription = "用藥建議",
			PubDate = pubDate ?? new DateOnly(2026, 9, 1),
			Issue = "第一期",
			Cities = [new PestAlertCity { CityName = city }],
			Crops = [new PestAlertCrop { CropName = crop }]
		};

		// ── 過期通知清理 ─────────────────────────────────────────────────────

		/// <summary>
		/// 過期清理是這條鏈唯一會刪資料的地方，判斷式是 ExpireAt 不為 null 而且已經過去。
		/// ExpireAt 為 null 代表「不過期」，把 null 一起刪掉的症狀是使用者的通知莫名其妙變少
		/// </summary>
		[Fact]
		public async Task 只刪已過期的通知不動未過期與不過期的()
		{
			var (engine, openDb) = CreateEngine(nameof(只刪已過期的通知不動未過期與不過期的));
			var now = DateTime.UtcNow;
			await using (var db = openDb())
			{
				db.PestRuleConfigs.Add(NumericRule(30m, isActive: false));
				await db.SaveChangesAsync();
				db.UserNotifications.AddRange(
					new UserNotification { UserId = "u1", PestRuleConfigId = 1, Message = "已過期", ExpireAt = now.AddDays(-1) },
					new UserNotification { UserId = "u1", PestRuleConfigId = 1, Message = "還沒過期", ExpireAt = now.AddDays(1) },
					new UserNotification { UserId = "u1", PestRuleConfigId = 1, Message = "不過期", ExpireAt = null });
				await db.SaveChangesAsync();
			}

			await engine.EvaluateAsync(null, CancellationToken.None);

			await using (var db = openDb())
			{
				var remaining = await db.UserNotifications.Select(n => n.Message).ToListAsync();
				Assert.Equal(2, remaining.Count);
				Assert.Contains("還沒過期", remaining);
				Assert.Contains("不過期", remaining);
			}
		}

		// ── 規則的取用範圍 ───────────────────────────────────────────────────

		/// <summary>
		/// 停用是使用者「暫時不要收這條」的表達方式，規則本身要留著。
		/// 沒有這個條件的話停用等同無效，使用者只剩刪除一途，而刪除會連帶清掉歷史通知
		/// </summary>
		[Fact]
		public async Task 停用中的規則不產生通知()
		{
			var (engine, openDb) = CreateEngine(nameof(停用中的規則不產生通知));
			var now = DateTime.UtcNow;
			await using (var db = openDb())
			{
				db.PestRuleConfigs.Add(NumericRule(30m, isActive: false));
				db.WeatherObservations.Add(Observation(35m, now.AddMinutes(-30), now.AddMinutes(-30)));
				await db.SaveChangesAsync();
			}

			await engine.EvaluateAsync(null, CancellationToken.None);

			await using (var db = openDb())
				Assert.Empty(await db.UserNotifications.ToListAsync());
		}

		/// <summary>
		/// 帶 UserId 呼叫是手動觸發端點的路徑：一個使用者按「立即檢查」不該讓別人的規則跟著跑。
		/// 這條同時守住爆炸半徑——逐條規則的例外會離開整支方法，限定範圍等於把影響收在呼叫者身上
		/// </summary>
		[Fact]
		public async Task 指定使用者時只評估該使用者的規則()
		{
			var (engine, openDb) = CreateEngine(nameof(指定使用者時只評估該使用者的規則));
			var now = DateTime.UtcNow;
			await using (var db = openDb())
			{
				var mine = NumericRule(30m);
				var others = NumericRule(30m);
				others.UserId = "u2";
				db.PestRuleConfigs.AddRange(mine, others);
				db.WeatherObservations.Add(Observation(35m, now.AddMinutes(-30), now.AddMinutes(-30)));
				await db.SaveChangesAsync();
			}

			var outcome = await engine.EvaluateAsync("u1", CancellationToken.None);

			Assert.Equal(1, outcome.RulesEvaluated);
			await using (var db = openDb())
				Assert.All(await db.UserNotifications.ToListAsync(), n => Assert.Equal("u1", n.UserId));
		}

		// ── 數值型：水位 ─────────────────────────────────────────────────────

		/// <summary>
		/// 水位為 null 代表這條規則還沒評估過。此時只看最後一批同步進來的觀測，
		/// 而不是整張表——去重擋得住「同一筆重複通知」，擋不住「第一次把歷史全撈出來」，
		/// 對一條新規則來說每一筆歷史都是第一次。
		/// 這裡的舊資料刻意讓 ObservedAt 維持新鮮，才驗得出擋掉它的是水位而不是新鮮度
		/// </summary>
		[Fact]
		public async Task 第一次評估只看最後一批落地的觀測不撈整張表()
		{
			var (engine, openDb) = CreateEngine(nameof(第一次評估只看最後一批落地的觀測不撈整張表));
			var now = DateTime.UtcNow;
			await using (var db = openDb())
			{
				db.PestRuleConfigs.Add(NumericRule(30m));
				db.WeatherObservations.AddRange(
					// 兩天前落地：早於「最新落地時刻減 15 分鐘」，不該被第一次評估看到
					Observation(35m, now.AddDays(-2), now.AddDays(-2), townName: "舊批次"),
					// 剛落地：最後一批
					Observation(35m, now.AddMinutes(-10), now.AddMinutes(-10), townName: "最新批次"));
				await db.SaveChangesAsync();
			}

			await engine.EvaluateAsync(null, CancellationToken.None);

			await using (var db = openDb())
			{
				var notification = Assert.Single(await db.UserNotifications.ToListAsync());
				Assert.Contains("最新批次", notification.Message);
			}
		}

		/// <summary>
		/// 評估完水位要往前推，否則下一輪會重掃同一批。
		/// 推的是本輪的掃描上界而不是「命中資料的最大落地時刻」——沒命中的那些也已經看過了，
		/// 推到命中值會讓不命中的區間被反覆重掃
		/// </summary>
		[Fact]
		public async Task 評估完要把水位推到本輪的掃描上界()
		{
			var (engine, openDb) = CreateEngine(nameof(評估完要把水位推到本輪的掃描上界));
			var now = DateTime.UtcNow;
			var latestSyncedAt = now.AddMinutes(-1);
			await using (var db = openDb())
			{
				db.PestRuleConfigs.Add(NumericRule(30m));
				db.WeatherObservations.AddRange(
					Observation(35m, now.AddMinutes(-10), now.AddMinutes(-10)),   // 命中
					Observation(10m, now.AddMinutes(-1), latestSyncedAt));        // 不命中，但落地時刻最新
				await db.SaveChangesAsync();
			}

			await engine.EvaluateAsync(null, CancellationToken.None);

			await using (var db = openDb())
			{
				var rule = await db.PestRuleConfigs.SingleAsync();
				Assert.Equal(latestSyncedAt, rule.LastEvaluatedAt);
			}
		}

		/// <summary>
		/// 有水位之後只看比水位更晚落地的觀測。這條守的是穩定狀態下的通知量——
		/// 氣象觀測是每小時約 876 筆的滾動視窗，少了這個條件，一條規則每輪都會把整個視窗重掃一次
		/// </summary>
		[Fact]
		public async Task 已有水位時只看比水位更晚落地的觀測()
		{
			var (engine, openDb) = CreateEngine(nameof(已有水位時只看比水位更晚落地的觀測));
			var now = DateTime.UtcNow;
			var watermark = now.AddHours(-2);
			await using (var db = openDb())
			{
				db.PestRuleConfigs.Add(NumericRule(30m, lastEvaluatedAt: watermark));
				db.WeatherObservations.AddRange(
					Observation(35m, now.AddHours(-3), now.AddHours(-3), townName: "水位之前"),
					Observation(35m, now.AddHours(-1), now.AddHours(-1), townName: "水位之後"));
				await db.SaveChangesAsync();
			}

			await engine.EvaluateAsync(null, CancellationToken.None);

			await using (var db = openDb())
			{
				var notification = Assert.Single(await db.UserNotifications.ToListAsync());
				Assert.Contains("水位之後", notification.Message);
			}
		}

		// ── 數值型：新鮮度 ───────────────────────────────────────────────────

		/// <summary>
		/// 新鮮度擋的是同步中斷後才被評估到的陳舊資料——「臺中市 19 天前氣溫 35 度」沒有行動價值。
		/// 這裡讓觀測的落地時刻很新、觀測時刻很舊，所以它過得了水位那一關，
		/// 唯一擋得住它的是新鮮度；反過來設計就分不出是哪一道條件生效
		/// </summary>
		[Fact]
		public async Task 觀測時刻超過新鮮度上限的資料不產生通知()
		{
			var (engine, openDb) = CreateEngine(nameof(觀測時刻超過新鮮度上限的資料不產生通知));
			var now = DateTime.UtcNow;
			await using (var db = openDb())
			{
				db.PestRuleConfigs.Add(NumericRule(30m));
				db.WeatherObservations.Add(Observation(35m, now.AddDays(-8), now.AddMinutes(-10)));
				await db.SaveChangesAsync();
			}

			await engine.EvaluateAsync(null, CancellationToken.None);

			await using (var db = openDb())
				Assert.Empty(await db.UserNotifications.ToListAsync());
		}

		/// <summary>
		/// 新鮮度窗內的資料照常通知。與上一條配成一對——只有上一條的話，
		/// 一個永遠回傳空集合的查詢也會通過測試
		/// </summary>
		[Fact]
		public async Task 觀測時刻在新鮮度上限內的資料照常產生通知()
		{
			var (engine, openDb) = CreateEngine(nameof(觀測時刻在新鮮度上限內的資料照常產生通知));
			var now = DateTime.UtcNow;
			await using (var db = openDb())
			{
				db.PestRuleConfigs.Add(NumericRule(30m));
				db.WeatherObservations.Add(Observation(35m, now.AddDays(-6), now.AddMinutes(-10)));
				await db.SaveChangesAsync();
			}

			await engine.EvaluateAsync(null, CancellationToken.None);

			await using (var db = openDb())
				Assert.Single(await db.UserNotifications.ToListAsync());
		}

		// ── 數值型：門檻、方向與觀測項目 ─────────────────────────────────────

		/// <summary>
		/// 門檻是嚴格比較。相等要不要觸發是使用者填「超過 30」時的實際預期，
		/// 邊界改成寬鬆的症狀是每次剛好等於門檻都多一則通知
		/// </summary>
		[Fact]
		public async Task 數值規則的門檻是嚴格比較相等不觸發()
		{
			var (engine, openDb) = CreateEngine(nameof(數值規則的門檻是嚴格比較相等不觸發));
			var now = DateTime.UtcNow;
			await using (var db = openDb())
			{
				db.PestRuleConfigs.Add(NumericRule(30m));
				db.WeatherObservations.Add(Observation(30m, now.AddMinutes(-10), now.AddMinutes(-10)));
				await db.SaveChangesAsync();
			}

			await engine.EvaluateAsync(null, CancellationToken.None);

			await using (var db = openDb())
				Assert.Empty(await db.UserNotifications.ToListAsync());
		}

		/// <summary>
		/// 門檻收到小數點後一位，所以 32.5 這種值必須真的比得出來。
		/// 型別若退回整數，32.4 與 32.5 會被當成同一個門檻，而使用者看得到自己填的是哪一個
		/// </summary>
		[Fact]
		public async Task 門檻可以帶一位小數並如實比較()
		{
			var (engine, openDb) = CreateEngine(nameof(門檻可以帶一位小數並如實比較));
			var now = DateTime.UtcNow;
			await using (var db = openDb())
			{
				db.PestRuleConfigs.Add(NumericRule(32.5m));
				db.WeatherObservations.AddRange(
					Observation(32.4m, now.AddMinutes(-10), now.AddMinutes(-10), townName: "低於門檻"),
					Observation(32.6m, now.AddMinutes(-10), now.AddMinutes(-10), townName: "高於門檻"));
				await db.SaveChangesAsync();
			}

			await engine.EvaluateAsync(null, CancellationToken.None);

			await using (var db = openDb())
			{
				var notification = Assert.Single(await db.UserNotifications.ToListAsync());
				Assert.Contains("高於門檻", notification.Message);
			}
		}

		/// <summary>
		/// 低於方向是農業的實際需求（寒害），不是對稱性的裝飾。
		/// 方向若被忽略，一條「低於 10 度」的規則會變成「超過 10 度」，
		/// 症狀是使用者整個夏天被通知不停，而他要的是冬天的那幾天
		/// </summary>
		[Fact]
		public async Task 低於方向的規則在數值低於門檻時觸發()
		{
			var (engine, openDb) = CreateEngine(nameof(低於方向的規則在數值低於門檻時觸發));
			var now = DateTime.UtcNow;
			await using (var db = openDb())
			{
				db.PestRuleConfigs.Add(NumericRule(10m, comparison: "LessThan"));
				db.WeatherObservations.AddRange(
					Observation(8m, now.AddMinutes(-10), now.AddMinutes(-10), townName: "寒害"),
					Observation(20m, now.AddMinutes(-10), now.AddMinutes(-10), townName: "正常"));
				await db.SaveChangesAsync();
			}

			await engine.EvaluateAsync(null, CancellationToken.None);

			await using (var db = openDb())
			{
				var notification = Assert.Single(await db.UserNotifications.ToListAsync());
				Assert.Contains("寒害", notification.Message);
				Assert.Contains("低於門檻", notification.Message);
			}
		}

		/// <summary>
		/// 觀測項目決定比對哪一個欄位。兩個項目的數值範圍差很多（氣溫個位到三十幾、雨量可到數百），
		/// 比錯欄位不會報錯，只會安靜地通知錯誤的事情
		/// </summary>
		[Fact]
		public async Task 觀測項目為雨量時比對雨量而不是氣溫()
		{
			var (engine, openDb) = CreateEngine(nameof(觀測項目為雨量時比對雨量而不是氣溫));
			var now = DateTime.UtcNow;
			await using (var db = openDb())
			{
				db.PestRuleConfigs.Add(NumericRule(100m, metricName: "Rainfall24h"));
				db.WeatherObservations.AddRange(
					// 氣溫很高但雨量不足：比錯欄位的話這筆會被通知
					Observation(35m, now.AddMinutes(-10), now.AddMinutes(-10), townName: "只有高溫", rainfall24h: 5m),
					Observation(20m, now.AddMinutes(-10), now.AddMinutes(-10), townName: "大雨", rainfall24h: 150m));
				await db.SaveChangesAsync();
			}

			await engine.EvaluateAsync(null, CancellationToken.None);

			await using (var db = openDb())
			{
				var notification = Assert.Single(await db.UserNotifications.ToListAsync());
				Assert.Contains("大雨", notification.Message);
				Assert.Contains("mm", notification.Message);
			}
		}

		/// <summary>
		/// 觀測項目或方向是不認得的值時跳過。這是引擎的兜底層：
		/// API 已經擋掉不合法的輸入，這裡擋的是繞過 API 直接寫進資料庫的路徑，
		/// 而兜底兜的是爆炸半徑——把「整支評估拋例外、全體使用者停擺」降級成「這一條規則失效」
		/// </summary>
		[Fact]
		public async Task 觀測項目或方向不合法時跳過且不影響其他規則()
		{
			var (engine, openDb) = CreateEngine(nameof(觀測項目或方向不合法時跳過且不影響其他規則));
			var now = DateTime.UtcNow;
			await using (var db = openDb())
			{
				db.PestRuleConfigs.AddRange(
					NumericRule(30m, metricName: "Humidity"),          // 不在支援清單裡
					NumericRule(30m, comparison: "NotEqualTo"),        // 不認得的方向
					NumericRule(30m));                                 // 正常規則
				db.WeatherObservations.Add(Observation(35m, now.AddMinutes(-10), now.AddMinutes(-10)));
				await db.SaveChangesAsync();
			}

			await engine.EvaluateAsync(null, CancellationToken.None);

			await using (var db = openDb())
			{
				var notification = Assert.Single(await db.UserNotifications.ToListAsync());
				Assert.Equal(3, notification.PestRuleConfigId);
			}
		}

		/// <summary>
		/// 缺少門檻的數值型規則跳過。門檻是數值型唯一必要的參數，缺了就無從比較
		/// </summary>
		[Fact]
		public async Task 數值規則缺少門檻時跳過且不影響其他規則()
		{
			var (engine, openDb) = CreateEngine(nameof(數值規則缺少門檻時跳過且不影響其他規則));
			var now = DateTime.UtcNow;
			await using (var db = openDb())
			{
				db.PestRuleConfigs.AddRange(NumericRule(null), NumericRule(30m));
				db.WeatherObservations.Add(Observation(35m, now.AddMinutes(-10), now.AddMinutes(-10)));
				await db.SaveChangesAsync();
			}

			await engine.EvaluateAsync(null, CancellationToken.None);

			await using (var db = openDb())
			{
				var notification = Assert.Single(await db.UserNotifications.ToListAsync());
				Assert.Equal(2, notification.PestRuleConfigId);
			}
		}

		// ── 縣市篩選與台／臺用字 ─────────────────────────────────────────────

		/// <summary>
		/// 縣市篩選是數值型收斂通知量的主要手段：全台 876 個測站，沒有縣市條件的規則
		/// 每輪都會噴出幾百則
		/// </summary>
		[Fact]
		public async Task 數值規則的縣市條件會排除其他縣市的觀測()
		{
			var (engine, openDb) = CreateEngine(nameof(數值規則的縣市條件會排除其他縣市的觀測));
			var now = DateTime.UtcNow;
			await using (var db = openDb())
			{
				db.PestRuleConfigs.Add(NumericRule(30m, filterCity: "臺中市"));
				db.WeatherObservations.AddRange(
					Observation(35m, now.AddMinutes(-10), now.AddMinutes(-10), cityName: "臺中市"),
					Observation(35m, now.AddMinutes(-10), now.AddMinutes(-10), cityName: "高雄市"));
				await db.SaveChangesAsync();
			}

			await engine.EvaluateAsync(null, CancellationToken.None);

			await using (var db = openDb())
			{
				var notification = Assert.Single(await db.UserNotifications.ToListAsync());
				Assert.Contains("臺中市", notification.Message);
			}
		}

		/// <summary>
		/// 兩張來源表的縣市用字不同：疫情警報全部用「台」、氣象觀測全部用「臺」，
		/// 而規則只存得下一種寫法。不兩種都試的話，同一條規則在其中一個型態上會靜默比不到任何一筆
		/// ——不會報錯，只會沒有通知。這是本輪最容易被漏掉的一項，兩個方向都要釘
		/// </summary>
		[Theory]
		[InlineData("臺中市", "台中市")]   // 規則存「臺」，來源用「台」
		[InlineData("台中市", "臺中市")]   // 規則存「台」，來源用「臺」
		public async Task 縣市比對時台與臺兩種寫法都要命中(string ruleCity, string sourceCity)
		{
			var (engine, openDb) = CreateEngine($"{nameof(縣市比對時台與臺兩種寫法都要命中)}_{ruleCity}");
			var now = DateTime.UtcNow;
			await using (var db = openDb())
			{
				db.PestRuleConfigs.Add(NumericRule(30m, filterCity: ruleCity));
				db.WeatherObservations.Add(
					Observation(35m, now.AddMinutes(-10), now.AddMinutes(-10), cityName: sourceCity));
				await db.SaveChangesAsync();
			}

			await engine.EvaluateAsync(null, CancellationToken.None);

			await using (var db = openDb())
				Assert.Single(await db.UserNotifications.ToListAsync());
		}

		// ── 去重與通知內容 ───────────────────────────────────────────────────

		/// <summary>
		/// 去重靠 (規則, 來源記錄) 這組合。水位擋的是「這輪要看哪些資料」，去重擋的是「這一筆發過沒有」
		/// ——水位若因為評估中途失敗而沒有回寫，下一輪會重掃同一批，那時就靠這裡兜底。
		/// 這裡刻意把水位倒回去模擬那個情境
		/// </summary>
		[Fact]
		public async Task 同一條規則對同一筆來源資料只通知一次()
		{
			var (engine, openDb) = CreateEngine(nameof(同一條規則對同一筆來源資料只通知一次));
			var now = DateTime.UtcNow;
			await using (var db = openDb())
			{
				db.PestRuleConfigs.Add(NumericRule(30m));
				db.WeatherObservations.Add(Observation(35m, now.AddMinutes(-10), now.AddMinutes(-10)));
				await db.SaveChangesAsync();
			}

			await engine.EvaluateAsync(null, CancellationToken.None);

			// 模擬水位沒有回寫成功：倒回 null，下一輪會重掃同一批
			await using (var db = openDb())
			{
				var rule = await db.PestRuleConfigs.SingleAsync();
				rule.LastEvaluatedAt = null;
				await db.SaveChangesAsync();
			}

			await engine.EvaluateAsync(null, CancellationToken.None);

			await using (var db = openDb())
				Assert.Single(await db.UserNotifications.ToListAsync());
		}

		/// <summary>
		/// 通知的過期時間由規則的 ExpiryDays 決定，過期後由下一輪評估的第一步刪掉。
		/// 算錯的症狀是通知提早消失或永遠不消失，兩者都不會報錯
		/// </summary>
		[Fact]
		public async Task 通知的過期時間等於觸發時間加上規則設定的天數()
		{
			var (engine, openDb) = CreateEngine(nameof(通知的過期時間等於觸發時間加上規則設定的天數));
			var now = DateTime.UtcNow;
			await using (var db = openDb())
			{
				db.PestRuleConfigs.Add(NumericRule(30m, expiryDays: 5));
				db.WeatherObservations.Add(Observation(35m, now.AddMinutes(-10), now.AddMinutes(-10)));
				await db.SaveChangesAsync();
			}

			await engine.EvaluateAsync(null, CancellationToken.None);

			await using (var db = openDb())
			{
				var notification = await db.UserNotifications.SingleAsync();
				Assert.Equal(notification.TriggeredAt.AddDays(5), notification.ExpireAt);
			}
		}

		/// <summary>
		/// 訊息在觸發當下組好存成字串，之後不再改變——氣象觀測是 30 天滾動刪除，
		/// 來源列日後必然不存在，訊息若改成顯示時即時組裝，舊通知就會顯示不出來。
		/// 所以訊息必須自己講完「什麼、哪裡、什麼時候、多少」，這條釘的就是那四樣都在
		/// </summary>
		[Fact]
		public async Task 數值型通知的訊息要帶地點時間量值與門檻()
		{
			var (engine, openDb) = CreateEngine(nameof(數值型通知的訊息要帶地點時間量值與門檻));
			var now = DateTime.UtcNow;
			var observedAt = now.AddMinutes(-10);
			await using (var db = openDb())
			{
				db.PestRuleConfigs.Add(NumericRule(32m));
				db.WeatherObservations.Add(Observation(32.1m, observedAt, observedAt));
				await db.SaveChangesAsync();
			}

			await engine.EvaluateAsync(null, CancellationToken.None);

			await using (var db = openDb())
			{
				var message = (await db.UserNotifications.SingleAsync()).Message;
				Assert.Contains("臺中市大甲區", message);                        // 哪裡
				Assert.Contains(observedAt.ToString("yyyy-MM-dd HH:mm"), message); // 什麼時候
				Assert.Contains("氣溫", message);                                  // 什麼
				Assert.Contains("32.1", message);                                  // 多少
				Assert.Contains("超過門檻", message);
			}
		}

		/// <summary>
		/// 通知要記得住是誰、哪條規則、哪一筆來源觸發的。
		/// UserId 抄自規則而不是來源資料——來源是全體共用的公開資料，沒有歸屬
		/// </summary>
		[Fact]
		public async Task 通知要記錄觸發它的來源記錄與規則()
		{
			var (engine, openDb) = CreateEngine(nameof(通知要記錄觸發它的來源記錄與規則));
			var now = DateTime.UtcNow;
			await using (var db = openDb())
			{
				db.PestRuleConfigs.Add(NumericRule(30m));
				db.WeatherObservations.Add(Observation(35m, now.AddMinutes(-10), now.AddMinutes(-10)));
				await db.SaveChangesAsync();
			}

			await engine.EvaluateAsync(null, CancellationToken.None);

			await using (var db = openDb())
			{
				var observationId = (await db.WeatherObservations.SingleAsync()).Id;
				var notification = await db.UserNotifications.SingleAsync();
				Assert.Equal("u1", notification.UserId);
				Assert.Equal(1, notification.PestRuleConfigId);
				Assert.Equal(observationId, notification.SourceRecordId);
			}
		}

		// ── 事件型 ───────────────────────────────────────────────────────────

		/// <summary>
		/// 縣市與作物是 AND 不是 OR。改成 OR 的症狀是使用者收到別的縣市的疫情，
		/// 而他設定的兩個條件明明都在畫面上
		/// </summary>
		[Fact]
		public async Task 事件規則要縣市與作物同時命中才觸發()
		{
			var (engine, openDb) = CreateEngine(nameof(事件規則要縣市與作物同時命中才觸發));
			await using (var db = openDb())
			{
				db.PestRuleConfigs.Add(EventRule("台中市", "柑橘"));
				db.PestAlerts.AddRange(
					Alert("兩個條件都中", "台中市", "柑橘"),
					Alert("只中縣市", "台中市", "水稻"),
					Alert("只中作物", "高雄市", "柑橘"));
				await db.SaveChangesAsync();
			}

			await engine.EvaluateAsync(null, CancellationToken.None);

			await using (var db = openDb())
			{
				var notification = Assert.Single(await db.UserNotifications.ToListAsync());
				Assert.Contains("兩個條件都中", notification.Message);
			}
		}

		/// <summary>
		/// 作物用包含比對而非相等：上游的作物名是自由字串（「廣東檸檬」「無子檸檬」「其他橙類」），
		/// 使用者輸入「檸檬」應該要命中這一整族。
		/// 縣市則相反，用相等——值來自前端的固定清單，用包含會讓「台南市」誤中「臺南市東區」這類字串
		/// </summary>
		[Fact]
		public async Task 事件規則的作物採包含比對縣市採相等比對()
		{
			var (engine, openDb) = CreateEngine(nameof(事件規則的作物採包含比對縣市採相等比對));
			await using (var db = openDb())
			{
				db.PestRuleConfigs.Add(EventRule("台中市", "檸檬"));
				db.PestAlerts.AddRange(
					Alert("作物部分命中", "台中市", "廣東檸檬"),
					Alert("縣市只是包含不算命中", "台中市東區", "檸檬"));
				await db.SaveChangesAsync();
			}

			await engine.EvaluateAsync(null, CancellationToken.None);

			await using (var db = openDb())
			{
				var notification = Assert.Single(await db.UserNotifications.ToListAsync());
				Assert.Contains("作物部分命中", notification.Message);
			}
		}

		/// <summary>
		/// 事件型用使用者選的起日限制範圍，而不是水位——來源每三四天才新增一兩筆、
		/// 累積 54 筆且不刪除，使用者選一個起日就夠，用水位反而會讓他等好幾天才收到第一則。
		/// 同一個「限制範圍」的需求，在兩種成長速度的資料源上要用不同的機制表達
		/// </summary>
		[Fact]
		public async Task 事件規則只通知起日之後發布的疫情()
		{
			var (engine, openDb) = CreateEngine(nameof(事件規則只通知起日之後發布的疫情));
			await using (var db = openDb())
			{
				db.PestRuleConfigs.Add(EventRule("台中市", "柑橘", dateFrom: new DateOnly(2026, 6, 1)));
				db.PestAlerts.AddRange(
					Alert("起日之前", "台中市", "柑橘", new DateOnly(2026, 5, 31)),
					Alert("起日當天", "台中市", "柑橘", new DateOnly(2026, 6, 1)),
					Alert("起日之後", "台中市", "柑橘", new DateOnly(2026, 7, 1)));
				await db.SaveChangesAsync();
			}

			await engine.EvaluateAsync(null, CancellationToken.None);

			await using (var db = openDb())
			{
				var messages = await db.UserNotifications.Select(n => n.Message).ToListAsync();
				Assert.Equal(2, messages.Count);
				Assert.DoesNotContain(messages, m => m.Contains("起日之前"));
			}
		}

		/// <summary>
		/// 事件型的篩選條件全部是選填：沒有條件就是「這個來源的每一則都通知我」，
		/// 那是合法的意圖，不該被當成設定不完整而跳過
		/// </summary>
		[Fact]
		public async Task 事件規則沒有任何篩選條件時通知該來源的全部警報()
		{
			var (engine, openDb) = CreateEngine(nameof(事件規則沒有任何篩選條件時通知該來源的全部警報));
			await using (var db = openDb())
			{
				db.PestRuleConfigs.Add(EventRule(null, null));
				db.PestAlerts.AddRange(
					Alert("第一則", "台中市", "柑橘"),
					Alert("第二則", "高雄市", "水稻"));
				await db.SaveChangesAsync();
			}

			await engine.EvaluateAsync(null, CancellationToken.None);

			await using (var db = openDb())
				Assert.Equal(2, await db.UserNotifications.CountAsync());
		}

		// ── 不開放的來源與未知型態 ───────────────────────────────────────────

		/// <summary>
		/// 兩個來源不開放給規則建立，但理由不同：TreePest 是資料的形狀不對（沒有時間戳、
		/// 沒有唯一識別欄位、語意是歷史診斷案例），PestDecade 是要比對的那一格根本沒有值
		/// （旬平均值上游未提供，我方 136 筆全為 null）。
		/// 兩條分開寫，日後其中一個來源改善了只會有一條測試需要改。
		/// API 已經擋掉這兩個值，這裡守的是資料庫裡可能留著的歷史列
		/// </summary>
		[Theory]
		[InlineData("Event", "TreePest")]
		[InlineData("Numeric", "PestDecade")]
		public async Task 不開放的來源不產生通知也不中斷其他規則(string ruleType, string sourceTable)
		{
			var (engine, openDb) = CreateEngine($"{nameof(不開放的來源不產生通知也不中斷其他規則)}_{sourceTable}");
			var now = DateTime.UtcNow;
			await using (var db = openDb())
			{
				var blocked = ruleType == "Event"
					? EventRule("台中市", "柑橘", sourceTable: sourceTable)
					: NumericRule(30m, sourceTable: sourceTable);
				db.PestRuleConfigs.AddRange(blocked, NumericRule(30m));
				db.PestAlerts.Add(Alert("疫情", "台中市", "柑橘"));
				db.WeatherObservations.Add(Observation(35m, now.AddMinutes(-10), now.AddMinutes(-10)));
				await db.SaveChangesAsync();
			}

			await engine.EvaluateAsync(null, CancellationToken.None);

			await using (var db = openDb())
			{
				var notification = Assert.Single(await db.UserNotifications.ToListAsync());
				Assert.Equal(2, notification.PestRuleConfigId);   // 只有正常那條產生通知
			}
		}

		/// <summary>
		/// 未知的規則型態同樣是跳過而不是拋例外。逐條規則的例外會離開整支方法，
		/// 後面所有使用者的規則都不會被評估——把一筆髒資料的影響從「全體停擺」降級成「這一條失效」
		/// </summary>
		[Fact]
		public async Task 未知的規則型態不產生通知也不中斷其他規則()
		{
			var (engine, openDb) = CreateEngine(nameof(未知的規則型態不產生通知也不中斷其他規則));
			var now = DateTime.UtcNow;
			await using (var db = openDb())
			{
				var unknown = NumericRule(30m);
				unknown.RuleType = "Unknown";
				db.PestRuleConfigs.AddRange(unknown, NumericRule(30m));
				db.WeatherObservations.Add(Observation(35m, now.AddMinutes(-10), now.AddMinutes(-10)));
				await db.SaveChangesAsync();
			}

			await engine.EvaluateAsync(null, CancellationToken.None);

			await using (var db = openDb())
			{
				var notification = Assert.Single(await db.UserNotifications.ToListAsync());
				Assert.Equal(2, notification.PestRuleConfigId);
			}
		}

		// ── 回傳結果 ─────────────────────────────────────────────────────────

		/// <summary>
		/// 手動觸發的端點靠回傳值分辨兩種「沒有新通知」：條件沒命中，還是資料根本不夠新。
		/// 少了 LatestObservedAt，兩種情況在畫面上長得一模一樣，使用者會以為功能壞了
		/// </summary>
		[Fact]
		public async Task 回傳結果要帶得出最新觀測時刻讓呼叫端分辨兩種空結果()
		{
			var (engine, openDb) = CreateEngine(nameof(回傳結果要帶得出最新觀測時刻讓呼叫端分辨兩種空結果));
			var now = DateTime.UtcNow;
			var staleObservedAt = now.AddDays(-19);
			await using (var db = openDb())
			{
				db.PestRuleConfigs.Add(NumericRule(30m));
				// 落地時刻很新、觀測時刻很舊：過得了水位，過不了新鮮度
				db.WeatherObservations.Add(Observation(35m, staleObservedAt, now.AddMinutes(-10)));
				await db.SaveChangesAsync();
			}

			var outcome = await engine.EvaluateAsync(null, CancellationToken.None);

			Assert.Equal(1, outcome.RulesEvaluated);
			Assert.Equal(0, outcome.NotificationsCreated);
			Assert.Equal(staleObservedAt, outcome.LatestObservedAt);
		}

		/// <summary>
		/// 第三種「沒有新通知」：規則沒問題、資料也夠新，但上次檢查之後根本沒有新的觀測落地。
		/// 這一種在畫面上最容易被誤讀成「條件沒命中」，於是使用者去調門檻——
		/// 而掃描範圍是空的，門檻怎麼調都是 0 則。這裡把水位直接設成表裡最新的落地時刻，
		/// 就是使用者連按兩次檢查、或改完條件立刻檢查時的狀態
		/// </summary>
		[Fact]
		public async Task 水位已追到掃描上界時要回報這一輪沒有新觀測可比對()
		{
			var (engine, openDb) = CreateEngine(nameof(水位已追到掃描上界時要回報這一輪沒有新觀測可比對));
			var now = DateTime.UtcNow;
			var latestSyncedAt = now.AddMinutes(-5);
			await using (var db = openDb())
			{
				// 門檻低到必然命中，所以「0 則」只可能來自掃描範圍是空的
				db.PestRuleConfigs.Add(NumericRule(10m, lastEvaluatedAt: latestSyncedAt));
				db.WeatherObservations.Add(Observation(35m, now.AddMinutes(-5), latestSyncedAt));
				await db.SaveChangesAsync();
			}

			var outcome = await engine.EvaluateAsync(null, CancellationToken.None);

			Assert.Equal(0, outcome.NotificationsCreated);
			Assert.Equal(1, outcome.NumericRulesEvaluated);
			Assert.Equal(0, outcome.NumericRulesWithNewObservations);
			Assert.True(outcome.HasFreshObservation);   // 資料是新的，不是「太舊」那一種空結果
		}

		/// <summary>
		/// 反向：水位落在最新落地時刻之前時，這一輪確實有新觀測可比對。
		/// 少了這一條，一個永遠回報「沒有新觀測」的實作也會讓上一條通過
		/// </summary>
		[Fact]
		public async Task 水位落在掃描上界之前時要回報有新觀測可比對()
		{
			var (engine, openDb) = CreateEngine(nameof(水位落在掃描上界之前時要回報有新觀測可比對));
			var now = DateTime.UtcNow;
			await using (var db = openDb())
			{
				db.PestRuleConfigs.Add(NumericRule(30m, lastEvaluatedAt: now.AddHours(-2)));
				db.WeatherObservations.Add(Observation(35m, now.AddMinutes(-5), now.AddMinutes(-5)));
				await db.SaveChangesAsync();
			}

			var outcome = await engine.EvaluateAsync(null, CancellationToken.None);

			Assert.Equal(1, outcome.NumericRulesEvaluated);
			Assert.Equal(1, outcome.NumericRulesWithNewObservations);
		}

		/// <summary>
		/// 被跳過的規則不算進「跑過比對的數值型規則數」。
		/// 兩者混在一起的話，一條缺門檻的規則會讓畫面說「還沒有新的觀測進來」，
		/// 而真正的原因是那條規則根本沒被評估——那是規則本身要修的問題，不是等資料就會好
		/// </summary>
		[Fact]
		public async Task 缺門檻而被跳過的數值型規則不算進評估數()
		{
			var (engine, openDb) = CreateEngine(nameof(缺門檻而被跳過的數值型規則不算進評估數));
			var now = DateTime.UtcNow;
			await using (var db = openDb())
			{
				db.PestRuleConfigs.Add(NumericRule(null));
				db.WeatherObservations.Add(Observation(35m, now.AddMinutes(-5), now.AddMinutes(-5)));
				await db.SaveChangesAsync();
			}

			var outcome = await engine.EvaluateAsync(null, CancellationToken.None);

			Assert.Equal(1, outcome.RulesEvaluated);        // 規則總數照算
			Assert.Equal(0, outcome.NumericRulesEvaluated); // 但它沒有跑到比對
			Assert.Equal(0, outcome.NumericRulesWithNewObservations);
		}

		/// <summary>
		/// 沒有任何規則時不拋例外。這是新使用者的第一天，也是排程在空資料庫上的第一次執行
		/// </summary>
		[Fact]
		public async Task 沒有任何規則時不拋例外也不產生通知()
		{
			var (engine, openDb) = CreateEngine(nameof(沒有任何規則時不拋例外也不產生通知));

			var outcome = await engine.EvaluateAsync(null, CancellationToken.None);

			Assert.Equal(0, outcome.RulesEvaluated);
			Assert.Equal(0, outcome.NotificationsCreated);
			Assert.Null(outcome.LatestObservedAt);
			await using (var db = openDb())
				Assert.Empty(await db.UserNotifications.ToListAsync());
		}
	}
}
