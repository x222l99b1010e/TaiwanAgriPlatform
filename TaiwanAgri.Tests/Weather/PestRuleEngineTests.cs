using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using TaiwanAgri.Modules.Weather.Data;
using TaiwanAgri.Modules.Weather.Dtos.WorkerResponses;
using TaiwanAgri.Modules.Weather.Entities;
using TaiwanAgri.Modules.Weather.Services;

namespace TaiwanAgri.Tests.Weather
{
	/// <summary>
	/// PestRuleEngine.EvaluateAsync——整條通知鏈的第一節，由 PestRuleEngineWorker 每天呼叫一次。
	/// 它同時做三件事：刪過期通知、取啟用中的規則、逐條比對來源資料產生通知。
	/// 測試重點在「跳過」這個行為身上：規則設定不完整時它選擇跳過而不是拋例外，
	/// 於是每一種跳過都是靜默的——使用者只會發現通知沒來，不會有任何錯誤訊號。
	/// <para>
	/// ⚠ 刻意沒測的一項：FilterJson 是格式錯誤的字串時，JsonSerializer 會拋 JsonException，
	/// 而程式碼的 filter == null 檢查只擋得住字面上的 "null"。這是既有缺陷而非本輪要改的東西，
	/// 寫成測試等於把它鎖成規格，修的時候還要先刪測試。已記入技術債，
	/// 與規則 CRUD 功能同一輪處理——那時才會有人存得進壞掉的 FilterJson
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
				provider.GetRequiredService<IServiceScopeFactory>());

			return (engine, () => new WeatherDbContext(
				new DbContextOptionsBuilder<WeatherDbContext>().UseInMemoryDatabase(databaseName).Options));
		}

		private static PestRuleConfig NumericRule(int? threshold, int expiryDays = 7, bool isActive = true) => new()
		{
			UserId = "u1",
			RuleName = "密度警戒",
			RuleType = "Numeric",
			SourceTable = "PestDecade",
			IsActive = isActive,
			Threshold = threshold,
			ExpiryDays = expiryDays
		};

		private static PestRuleConfig EventRule(
			string? city, string? plantName, string sourceTable = "PlantEpidemic", bool isActive = true) => new()
			{
				UserId = "u1",
				RuleName = "疫情警報",
				RuleType = "Event",
				SourceTable = sourceTable,
				IsActive = isActive,
				ExpiryDays = 14,
				FilterJson = city == null && plantName == null
					? null
					: JsonSerializer.Serialize(new PestRuleFilter { City = city ?? "", PlantName = plantName ?? "" })
			};

		private static PestDecadeSummary Decade(decimal average) => new()
		{
			PestName = "褐飛蝨",
			Year = 2026,
			Month = 9,
			TenDays = 1,
			City = "臺北市",
			Town = "中正區",
			Average = average
		};

		private static PestAlert Alert(string subject, string city, string crop) => new()
		{
			Subject = subject,
			Body = "內文",
			Prescription = "用藥建議",
			PubDate = new DateOnly(2026, 9, 1),
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
			using (var db = openDb())
			{
				var rule = NumericRule(threshold: null, isActive: false);  // 不啟用，避免產生新通知干擾
				db.PestRuleConfigs.Add(rule);
				await db.SaveChangesAsync();
				db.UserNotifications.AddRange(
					new UserNotification { UserId = "u1", PestRuleConfigId = rule.Id, Message = "已過期", TriggeredAt = now, ExpireAt = now.AddDays(-1) },
					new UserNotification { UserId = "u1", PestRuleConfigId = rule.Id, Message = "未過期", TriggeredAt = now, ExpireAt = now.AddDays(1) },
					new UserNotification { UserId = "u1", PestRuleConfigId = rule.Id, Message = "不過期", TriggeredAt = now, ExpireAt = null });
				await db.SaveChangesAsync();
			}

			await engine.EvaluateAsync(CancellationToken.None);

			using var readDb = openDb();
			var messages = await readDb.UserNotifications.Select(n => n.Message).ToListAsync();
			Assert.Equal(2, messages.Count);
			Assert.Contains("未過期", messages);
			Assert.Contains("不過期", messages);
		}

		// ── 規則篩選 ─────────────────────────────────────────────────────────

		/// <summary>
		/// 停用中的規則不參與評估。這是使用者關掉某條規則之後唯一的預期行為，
		/// 而漏掉 IsActive 篩選的症狀是「關掉了還是一直收到通知」
		/// </summary>
		[Fact]
		public async Task 停用中的規則不產生通知()
		{
			var (engine, openDb) = CreateEngine(nameof(停用中的規則不產生通知));
			using (var db = openDb())
			{
				db.PestRuleConfigs.Add(NumericRule(threshold: 10, isActive: false));
				db.PestDecadeSummaries.Add(Decade(99m));
				await db.SaveChangesAsync();
			}

			await engine.EvaluateAsync(CancellationToken.None);

			using var readDb = openDb();
			Assert.Empty(readDb.UserNotifications);
		}

		// ── Numeric 規則 ─────────────────────────────────────────────────────

		/// <summary>
		/// 閾值比較是嚴格大於，相等不觸發。這條邊界寫成大於等於的話，
		/// 設定「超過 10 就通知」的使用者會在剛好 10 的時候收到通知
		/// </summary>
		[Fact]
		public async Task 數值規則的閾值是嚴格大於相等不觸發()
		{
			var (engine, openDb) = CreateEngine(nameof(數值規則的閾值是嚴格大於相等不觸發));
			using (var db = openDb())
			{
				db.PestRuleConfigs.Add(NumericRule(threshold: 10));
				db.PestDecadeSummaries.AddRange(Decade(10m), Decade(11m));
				await db.SaveChangesAsync();
			}

			await engine.EvaluateAsync(CancellationToken.None);

			using var readDb = openDb();
			var notification = Assert.Single(readDb.UserNotifications);
			Assert.Contains("11", notification.Message);
		}

		/// <summary>
		/// 數值規則少了閾值就無從比較，程式選擇跳過而不是拋例外——
		/// 因為一條規則設定不全不該讓其他人的通知全部停擺。代價是使用者收不到通知也不會知道為什麼
		/// </summary>
		[Fact]
		public async Task 數值規則缺少閾值時跳過且不影響其他規則()
		{
			var (engine, openDb) = CreateEngine(nameof(數值規則缺少閾值時跳過且不影響其他規則));
			using (var db = openDb())
			{
				db.PestRuleConfigs.AddRange(NumericRule(threshold: null), NumericRule(threshold: 10));
				db.PestDecadeSummaries.Add(Decade(50m));
				await db.SaveChangesAsync();
			}

			await engine.EvaluateAsync(CancellationToken.None);

			using var readDb = openDb();
			Assert.Single(readDb.UserNotifications);  // 只有那條有閾值的規則產生了通知
		}

		/// <summary>
		/// 去重的鍵是「規則 + 來源記錄」這組合，所以 Worker 天天跑也只會通知一次。
		/// 少了這道檢查的症狀最明顯：同一則通知每天多一筆，一週後列表被同一句話塞滿
		/// </summary>
		[Fact]
		public async Task 同一條規則對同一筆來源資料只通知一次()
		{
			var (engine, openDb) = CreateEngine(nameof(同一條規則對同一筆來源資料只通知一次));
			using (var db = openDb())
			{
				db.PestRuleConfigs.Add(NumericRule(threshold: 10));
				db.PestDecadeSummaries.Add(Decade(50m));
				await db.SaveChangesAsync();
			}

			await engine.EvaluateAsync(CancellationToken.None);
			await engine.EvaluateAsync(CancellationToken.None);  // 模擬隔天再跑一次

			using var readDb = openDb();
			Assert.Single(readDb.UserNotifications);
		}

		/// <summary>
		/// 通知的過期時間是觸發當下加上規則設定的天數；ExpiryDays 為 0 代表當天到期。
		/// 這個欄位是上面那條清理邏輯唯一的輸入，設錯的話通知不是永遠不消失就是立刻消失
		/// </summary>
		[Fact]
		public async Task 通知的過期時間等於觸發時間加上規則設定的天數()
		{
			var (engine, openDb) = CreateEngine(nameof(通知的過期時間等於觸發時間加上規則設定的天數));
			using (var db = openDb())
			{
				db.PestRuleConfigs.Add(NumericRule(threshold: 10, expiryDays: 30));
				db.PestDecadeSummaries.Add(Decade(50m));
				await db.SaveChangesAsync();
			}

			await engine.EvaluateAsync(CancellationToken.None);

			using var readDb = openDb();
			var notification = Assert.Single(readDb.UserNotifications);
			Assert.Equal(notification.TriggeredAt.AddDays(30), notification.ExpireAt);
		}

		/// <summary>
		/// 通知會記下是哪一筆來源資料觸發的，這個欄位同時是去重的另一半鍵
		/// </summary>
		[Fact]
		public async Task 通知要記錄觸發它的來源記錄與規則()
		{
			var (engine, openDb) = CreateEngine(nameof(通知要記錄觸發它的來源記錄與規則));
			int ruleId, sourceId;
			using (var db = openDb())
			{
				var rule = NumericRule(threshold: 10);
				var decade = Decade(50m);
				db.PestRuleConfigs.Add(rule);
				db.PestDecadeSummaries.Add(decade);
				await db.SaveChangesAsync();
				(ruleId, sourceId) = (rule.Id, decade.Id);
			}

			await engine.EvaluateAsync(CancellationToken.None);

			using var readDb = openDb();
			var notification = Assert.Single(readDb.UserNotifications);
			Assert.Equal(ruleId, notification.PestRuleConfigId);
			Assert.Equal(sourceId, notification.SourceRecordId);
			Assert.Equal("u1", notification.UserId);
		}

		// ── Event 規則 ───────────────────────────────────────────────────────

		/// <summary>
		/// 事件型規則的兩個條件是且，不是或——縣市與作物都要命中才通知。
		/// 寫成或的症狀是使用者收到一堆「我的縣市但不是我的作物」的無關警報
		/// </summary>
		[Fact]
		public async Task 事件規則要縣市與作物同時命中才觸發()
		{
			var (engine, openDb) = CreateEngine(nameof(事件規則要縣市與作物同時命中才觸發));
			using (var db = openDb())
			{
				db.PestRuleConfigs.Add(EventRule("臺北", "水稻"));
				db.PestAlerts.AddRange(
					Alert("兩者皆中", "臺北市", "水稻田"),
					Alert("只中縣市", "臺北市", "玉米"),
					Alert("只中作物", "臺中市", "水稻田"));
				await db.SaveChangesAsync();
			}

			await engine.EvaluateAsync(CancellationToken.None);

			using var readDb = openDb();
			var notification = Assert.Single(readDb.UserNotifications);
			Assert.Contains("兩者皆中", notification.Message);
		}

		/// <summary>
		/// 比對用的是 Contains 而不是相等，所以「臺北」這個設定值對得上「臺北市」這筆資料。
		/// 這是刻意放寬的——來源資料的縣市寫法不一致，要求完全相等會讓大部分規則永遠不觸發
		/// </summary>
		[Fact]
		public async Task 事件規則的縣市與作物採包含比對而非完全相等()
		{
			var (engine, openDb) = CreateEngine(nameof(事件規則的縣市與作物採包含比對而非完全相等));
			using (var db = openDb())
			{
				db.PestRuleConfigs.Add(EventRule("臺北", "水稻"));
				db.PestAlerts.Add(Alert("疫情", "臺北市", "水稻田"));
				await db.SaveChangesAsync();
			}

			await engine.EvaluateAsync(CancellationToken.None);

			using var readDb = openDb();
			Assert.Single(readDb.UserNotifications);
		}

		/// <summary>
		/// 事件型規則少了過濾條件就無從比對，同樣選擇跳過
		/// </summary>
		[Fact]
		public async Task 事件規則缺少過濾條件時跳過()
		{
			var (engine, openDb) = CreateEngine(nameof(事件規則缺少過濾條件時跳過));
			using (var db = openDb())
			{
				db.PestRuleConfigs.Add(EventRule(null, null));  // FilterJson 為 null
				db.PestAlerts.Add(Alert("疫情", "臺北市", "水稻"));
				await db.SaveChangesAsync();
			}

			await engine.EvaluateAsync(CancellationToken.None);

			using var readDb = openDb();
			Assert.Empty(readDb.UserNotifications);
		}

		/// <summary>
		/// TreePest 這個來源尚未實作，引擎只記一則警告就跳過。
		/// 這裡用「有沒有產生通知」來驗而不是去驗它寫了什麼 log——
		/// log 訊息不是對外契約，改一個字就讓測試變紅的斷言只會讓人習慣忽略紅燈
		/// </summary>
		[Fact]
		public async Task 尚未實作的來源不產生通知也不中斷其他規則()
		{
			var (engine, openDb) = CreateEngine(nameof(尚未實作的來源不產生通知也不中斷其他規則));
			using (var db = openDb())
			{
				db.PestRuleConfigs.AddRange(
					EventRule("臺北", "水稻", sourceTable: "TreePest"),
					NumericRule(threshold: 10));
				db.PestAlerts.Add(Alert("疫情", "臺北市", "水稻"));
				db.PestDecadeSummaries.Add(Decade(50m));
				await db.SaveChangesAsync();
			}

			await engine.EvaluateAsync(CancellationToken.None);

			using var readDb = openDb();
			// TreePest 那條不產生任何東西，但不能拖累後面那條數值規則
			var notification = Assert.Single(readDb.UserNotifications);
			Assert.Contains("超過閾值", notification.Message);
		}

		/// <summary>
		/// 未知的規則型態與未知的來源表都只跳過。這兩個分支是設定值打錯字時的落點，
		/// 而落到這裡的規則會安靜地什麼都不做
		/// </summary>
		[Fact]
		public async Task 未知的規則型態與來源表都不產生通知()
		{
			var (engine, openDb) = CreateEngine(nameof(未知的規則型態與來源表都不產生通知));
			using (var db = openDb())
			{
				var 未知型態 = NumericRule(threshold: 10);
				未知型態.RuleType = "Unknown";
				db.PestRuleConfigs.AddRange(未知型態, EventRule("臺北", "水稻", sourceTable: "Unknown"));
				db.PestDecadeSummaries.Add(Decade(50m));
				db.PestAlerts.Add(Alert("疫情", "臺北市", "水稻"));
				await db.SaveChangesAsync();
			}

			await engine.EvaluateAsync(CancellationToken.None);

			using var readDb = openDb();
			Assert.Empty(readDb.UserNotifications);
		}

		/// <summary>
		/// 沒有任何啟用規則時整支方法要能正常跑完——這正是目前正式環境的狀態，
		/// 因為規則還沒有任何建立管道
		/// </summary>
		[Fact]
		public async Task 沒有任何規則時不拋例外也不產生通知()
		{
			var (engine, openDb) = CreateEngine(nameof(沒有任何規則時不拋例外也不產生通知));
			using (var db = openDb())
			{
				db.PestDecadeSummaries.Add(Decade(50m));
				await db.SaveChangesAsync();
			}

			await engine.EvaluateAsync(CancellationToken.None);

			using var readDb = openDb();
			Assert.Empty(readDb.UserNotifications);
		}
	}
}
