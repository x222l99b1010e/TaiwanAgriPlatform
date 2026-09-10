using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TaiwanAgri.Core.Dtos;
using TaiwanAgri.Modules.Weather.Constants;
using TaiwanAgri.Modules.Weather.Data;
using TaiwanAgri.Modules.Weather.Dtos.ApiRequests;
using TaiwanAgri.Modules.Weather.Entities;
using TaiwanAgri.Modules.Weather.Services;

namespace TaiwanAgri.Tests.Weather
{
	/// <summary>
	/// 規則 CRUD 與「立即檢查」。
	/// <para>
	/// 重點有三處。其一是越權：每一支方法都把 userId 寫進查詢條件，
	/// 漏掉的那一支不會有任何訊號、而且會是一個越權漏洞，所以擋下來之外還要斷言那筆資料真的沒被改動
	/// ——只斷言「回 false」的話，一個「先改了再回 false」的實作照樣會過。
	/// 其二是伺服器補上的值（保留天數的預設、另一型態欄位清成 null、水位）：
	/// 這些是使用者送不出來的東西，錯了只會表現成通知太多、太少或太早消失。
	/// 其三是刪除規則連帶刪除通知——那個不變式由資料庫的外鍵維護，不是由這裡的程式碼維護。
	/// </para>
	/// </summary>
	public class NotificationRuleServiceTests
	{
		private static readonly DateTime FixedNow = new(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);

		/// <summary>固定時刻的 TimeProvider，讓 CreatedAt 斷言可重現（比照 PetServiceTests 既有寫法）</summary>
		private sealed class FixedTimeProvider : TimeProvider
		{
			private readonly DateTimeOffset _utcNow;
			public FixedTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;
			public override DateTimeOffset GetUtcNow() => _utcNow;
		}

		private static WeatherDbContext CreateDbContext(string databaseName) =>
			new(new DbContextOptionsBuilder<WeatherDbContext>()
				.UseInMemoryDatabase(databaseName)
				.Options);

		/// <summary>
		/// 服務依賴規則引擎（「立即檢查」用），而引擎自己從 IServiceScopeFactory 開 scope 取 DbContext，
		/// 所以這裡建一個真的 ServiceProvider 指向同一個 InMemory 資料庫
		/// </summary>
		private static NotificationRuleService CreateService(WeatherDbContext db, string databaseName)
		{
			var services = new ServiceCollection();
			services.AddDbContext<WeatherDbContext>(o => o.UseInMemoryDatabase(databaseName));
			var provider = services.BuildServiceProvider();

			var engine = new PestRuleEngine(
				NullLogger<PestRuleEngine>.Instance,
				provider.GetRequiredService<IServiceScopeFactory>(),
				new FixedTimeProvider(FixedNow));

			return new NotificationRuleService(db, engine, new FixedTimeProvider(FixedNow));
		}

		private static NotificationRuleRequestDto NumericRequest(
			string ruleName = "高溫警戒", int? expiryDays = null) => new()
			{
				RuleName = ruleName,
				RuleType = "Numeric",
				SourceTable = "WeatherObservation",
				MetricName = "Temperature",
				Comparison = "GreaterThan",
				Threshold = 32.0m,
				FilterCity = "臺中市",
				ExpiryDays = expiryDays
			};

		private static NotificationRuleRequestDto EventRequest(
			string ruleName = "疫情警報", int? expiryDays = null) => new()
			{
				RuleName = ruleName,
				RuleType = "Event",
				SourceTable = "PlantEpidemic",
				FilterCity = "臺中市",
				FilterPlantName = "檸檬",
				FilterDateFrom = new DateOnly(2026, 6, 12),
				ExpiryDays = expiryDays
			};

		private static PestRuleConfig SeedRule(
			string userId = "u1", string ruleName = "既有規則", bool isActive = true,
			string ruleType = "Numeric", DateTime? lastEvaluatedAt = null) => new()
			{
				UserId = userId,
				RuleName = ruleName,
				RuleType = ruleType,
				SourceTable = ruleType == "Numeric" ? "WeatherObservation" : "PlantEpidemic",
				IsActive = isActive,
				ExpiryDays = 7,
				MetricName = ruleType == "Numeric" ? "Temperature" : null,
				Comparison = ruleType == "Numeric" ? "GreaterThan" : null,
				Threshold = ruleType == "Numeric" ? 32.0m : null,
				LastEvaluatedAt = lastEvaluatedAt,
				CreatedAt = FixedNow
			};

		// ── 伺服器補上的值 ────────────────────────────────────────────────

		[Fact]
		public async Task 數值型沒帶保留天數時填入七天()
		{
			var dbName = nameof(數值型沒帶保留天數時填入七天);
			await using var db = CreateDbContext(dbName);

			var rule = await CreateService(db, dbName).CreateRuleAsync("u1", NumericRequest());

			Assert.Equal(NotificationRule.Limits.NumericDefaultExpiryDays, rule.ExpiryDays);
		}

		[Fact]
		public async Task 事件型沒帶保留天數時填入三十天()
		{
			var dbName = nameof(事件型沒帶保留天數時填入三十天);
			await using var db = CreateDbContext(dbName);

			var rule = await CreateService(db, dbName).CreateRuleAsync("u1", EventRequest());

			Assert.Equal(NotificationRule.Limits.EventDefaultExpiryDays, rule.ExpiryDays);
		}

		/// <summary>
		/// 預設值只在沒帶值時生效。少了這條，一個「一律用預設值」的實作也會讓上面兩條變綠，
		/// 而使用者填的天數會被安靜地丟掉
		/// </summary>
		[Fact]
		public async Task 使用者填的保留天數優先於預設值()
		{
			var dbName = nameof(使用者填的保留天數優先於預設值);
			await using var db = CreateDbContext(dbName);

			var rule = await CreateService(db, dbName).CreateRuleAsync("u1", NumericRequest(expiryDays: 21));

			Assert.Equal(21, rule.ExpiryDays);
		}

		/// <summary>
		/// 前端切換型態時若沒清乾淨舊值，那些值會照樣送上來。它們對這個型態沒有作用，
		/// 留在資料庫裡只會讓下一個查資料的人以為這條規則有在比那些條件
		/// </summary>
		[Fact]
		public async Task 建立事件型規則會清掉數值型的三個欄位()
		{
			var dbName = nameof(建立事件型規則會清掉數值型的三個欄位);
			await using var db = CreateDbContext(dbName);

			var request = EventRequest();
			request.MetricName = "Temperature";
			request.Comparison = "GreaterThan";
			request.Threshold = 32.0m;

			await CreateService(db, dbName).CreateRuleAsync("u1", request);

			await using var verify = CreateDbContext(dbName);
			var saved = verify.PestRuleConfigs.Single();
			Assert.Null(saved.MetricName);
			Assert.Null(saved.Comparison);
			Assert.Null(saved.Threshold);
			// 事件型自己的欄位要留著，否則這條測試會被一個「全部清空」的實作通過
			Assert.Equal("檸檬", saved.FilterPlantName);
			Assert.Equal(new DateOnly(2026, 6, 12), saved.FilterDateFrom);
		}

		[Fact]
		public async Task 建立數值型規則會清掉事件型的兩個欄位()
		{
			var dbName = nameof(建立數值型規則會清掉事件型的兩個欄位);
			await using var db = CreateDbContext(dbName);

			var request = NumericRequest();
			request.FilterPlantName = "檸檬";
			request.FilterDateFrom = new DateOnly(2026, 6, 12);

			await CreateService(db, dbName).CreateRuleAsync("u1", request);

			await using var verify = CreateDbContext(dbName);
			var saved = verify.PestRuleConfigs.Single();
			Assert.Null(saved.FilterPlantName);
			Assert.Null(saved.FilterDateFrom);
			Assert.Equal(32.0m, saved.Threshold);
			// 縣市是兩種型態共用的，不能跟著被清掉
			Assert.Equal("臺中市", saved.FilterCity);
		}

		// ── 規則數上限 ────────────────────────────────────────────────────

		[Fact]
		public async Task 超過上限的那一條要被擋下()
		{
			var dbName = nameof(超過上限的那一條要被擋下);
			await using var db = CreateDbContext(dbName);
			for (var i = 0; i < NotificationRule.Limits.MaxRulesPerUser; i++)
				db.PestRuleConfigs.Add(SeedRule(ruleName: $"規則 {i}"));
			await db.SaveChangesAsync();

			await Assert.ThrowsAsync<RuleLimitExceededException>(
				() => CreateService(db, dbName).CreateRuleAsync("u1", NumericRequest()));

			await using var verify = CreateDbContext(dbName);
			Assert.Equal(NotificationRule.Limits.MaxRulesPerUser, verify.PestRuleConfigs.Count());
		}

		/// <summary>
		/// 上限之內的最後一條要建得起來。只有「超過就擋」那條的話，
		/// 一個把上限寫成 0、什麼都建不了的實作也會通過
		/// </summary>
		[Fact]
		public async Task 上限之內的最後一條要建得起來()
		{
			var dbName = nameof(上限之內的最後一條要建得起來);
			await using var db = CreateDbContext(dbName);
			for (var i = 0; i < NotificationRule.Limits.MaxRulesPerUser - 1; i++)
				db.PestRuleConfigs.Add(SeedRule(ruleName: $"規則 {i}"));
			await db.SaveChangesAsync();

			var rule = await CreateService(db, dbName).CreateRuleAsync("u1", NumericRequest());

			Assert.True(rule.Id > 0);
		}

		/// <summary>
		/// 上限算的是總數不是啟用中的數量——只算啟用中的話，
		/// 使用者可以建無限多條停用的規則，儲存就沒有上界了
		/// </summary>
		[Fact]
		public async Task 停用中的規則也算進上限()
		{
			var dbName = nameof(停用中的規則也算進上限);
			await using var db = CreateDbContext(dbName);
			for (var i = 0; i < NotificationRule.Limits.MaxRulesPerUser; i++)
				db.PestRuleConfigs.Add(SeedRule(ruleName: $"規則 {i}", isActive: false));
			await db.SaveChangesAsync();

			await Assert.ThrowsAsync<RuleLimitExceededException>(
				() => CreateService(db, dbName).CreateRuleAsync("u1", NumericRequest()));
		}

		/// <summary>上限是逐使用者算的，別人的規則不佔自己的額度</summary>
		[Fact]
		public async Task 別人的規則不佔自己的額度()
		{
			var dbName = nameof(別人的規則不佔自己的額度);
			await using var db = CreateDbContext(dbName);
			for (var i = 0; i < NotificationRule.Limits.MaxRulesPerUser; i++)
				db.PestRuleConfigs.Add(SeedRule(userId: "u2", ruleName: $"規則 {i}"));
			await db.SaveChangesAsync();

			var rule = await CreateService(db, dbName).CreateRuleAsync("u1", NumericRequest());

			Assert.True(rule.Id > 0);
		}

		// ── 越權 ──────────────────────────────────────────────────────────

		[Fact]
		public async Task 查不到別人的規則()
		{
			var dbName = nameof(查不到別人的規則);
			await using var db = CreateDbContext(dbName);
			var others = SeedRule(userId: "u2");
			db.PestRuleConfigs.Add(others);
			await db.SaveChangesAsync();

			Assert.Null(await CreateService(db, dbName).GetRuleByIdAsync(others.Id, "u1"));
		}

		[Fact]
		public async Task 列表只回自己的規則()
		{
			var dbName = nameof(列表只回自己的規則);
			await using var db = CreateDbContext(dbName);
			db.PestRuleConfigs.AddRange(
				SeedRule(ruleName: "我的一"),
				SeedRule(ruleName: "我的二"),
				SeedRule(userId: "u2", ruleName: "別人的"));
			await db.SaveChangesAsync();

			var result = await CreateService(db, dbName).GetRulesAsync("u1", new PagedQueryDto());

			Assert.Equal(2, result.TotalCount);
			Assert.All(result.Items, x => Assert.StartsWith("我的", x.RuleName));
		}

		/// <summary>
		/// 查詢條件少了 UserId 那半時，別人的規則會被改掉。只斷言回 false 抓不到
		/// ——一個「先改了再回 false」的實作照樣會通過
		/// </summary>
		[Fact]
		public async Task 更新別人的規則要被擋下而且不能改到那筆資料()
		{
			var dbName = nameof(更新別人的規則要被擋下而且不能改到那筆資料);
			int othersRuleId;
			await using (var db = CreateDbContext(dbName))
			{
				var others = SeedRule(userId: "u2", ruleName: "別人的規則");
				db.PestRuleConfigs.Add(others);
				await db.SaveChangesAsync();
				othersRuleId = others.Id;
			}

			await using (var db = CreateDbContext(dbName))
			{
				var updated = await CreateService(db, dbName)
					.UpdateRuleAsync(othersRuleId, "u1", NumericRequest(ruleName: "被改掉的名字"));
				Assert.False(updated);
			}

			await using var verify = CreateDbContext(dbName);
			Assert.Equal("別人的規則", verify.PestRuleConfigs.Single(x => x.Id == othersRuleId).RuleName);
		}

		[Fact]
		public async Task 刪除別人的規則要被擋下而且那筆資料還在()
		{
			var dbName = nameof(刪除別人的規則要被擋下而且那筆資料還在);
			int othersRuleId;
			await using (var db = CreateDbContext(dbName))
			{
				var others = SeedRule(userId: "u2");
				db.PestRuleConfigs.Add(others);
				await db.SaveChangesAsync();
				othersRuleId = others.Id;
			}

			await using (var db = CreateDbContext(dbName))
				Assert.False(await CreateService(db, dbName).DeleteRuleAsync(othersRuleId, "u1"));

			await using var verify = CreateDbContext(dbName);
			Assert.True(verify.PestRuleConfigs.Any(x => x.Id == othersRuleId));
		}

		// ── 更新的語意：往後套用 ──────────────────────────────────────────

		/// <summary>
		/// 改條件是「從此刻起往後套用」，不是刪掉重建：既有通知留著、水位不重置。
		/// 重置水位等於往前追，把已經看過的觀測重新灌成一批通知，而那正是水位要解決的問題
		/// </summary>
		[Fact]
		public async Task 更新規則不刪既有通知也不重置水位()
		{
			var dbName = nameof(更新規則不刪既有通知也不重置水位);
			var watermark = FixedNow.AddHours(-2);
			int ruleId;
			await using (var db = CreateDbContext(dbName))
			{
				var rule = SeedRule(lastEvaluatedAt: watermark);
				db.PestRuleConfigs.Add(rule);
				await db.SaveChangesAsync();
				ruleId = rule.Id;
				db.UserNotifications.Add(new UserNotification
				{
					UserId = "u1",
					PestRuleConfigId = ruleId,
					SourceRecordId = 1,
					Message = "臺中市大甲區｜2026-09-10 10:00｜氣溫 33.0°C，超過門檻 32.0°C",
					TriggeredAt = FixedNow.AddHours(-1)
				});
				await db.SaveChangesAsync();
			}

			await using (var db = CreateDbContext(dbName))
			{
				var request = NumericRequest(ruleName: "門檻改高");
				request.Threshold = 35.0m;
				Assert.True(await CreateService(db, dbName).UpdateRuleAsync(ruleId, "u1", request));
			}

			await using var verify = CreateDbContext(dbName);
			var saved = verify.PestRuleConfigs.Single(x => x.Id == ruleId);
			Assert.Equal(35.0m, saved.Threshold);
			Assert.Equal(watermark, saved.LastEvaluatedAt);
			// 「當時確實超過 32 度」這件事沒有因為門檻被改成 35 就變成沒發生過
			Assert.Single(verify.UserNotifications);
		}

		/// <summary>
		/// 水位是數值型專用欄位。改成事件型還留著舊水位的話，日後改回數值型時
		/// 會從一個很舊的落地時刻開始掃，一次灌出一批通知
		/// </summary>
		[Fact]
		public async Task 改成事件型會清掉水位()
		{
			var dbName = nameof(改成事件型會清掉水位);
			int ruleId;
			await using (var db = CreateDbContext(dbName))
			{
				var rule = SeedRule(lastEvaluatedAt: FixedNow.AddHours(-2));
				db.PestRuleConfigs.Add(rule);
				await db.SaveChangesAsync();
				ruleId = rule.Id;
			}

			await using (var db = CreateDbContext(dbName))
				Assert.True(await CreateService(db, dbName).UpdateRuleAsync(ruleId, "u1", EventRequest()));

			await using var verify = CreateDbContext(dbName);
			Assert.Null(verify.PestRuleConfigs.Single(x => x.Id == ruleId).LastEvaluatedAt);
		}

		// ── 刪除規則與它的通知 ────────────────────────────────────────────

		/// <summary>
		/// 「每筆通知都對應到一條存在的規則」這個不變式由資料庫的外鍵維護，
		/// 服務層沒有任何一行程式碼在刪通知。所以先直接把設定本身釘住——
		/// 這一條是 <see cref="刪除規則會連帶刪除它的通知而且未讀數同步下降"/> 的前提：
		/// 刪除行為若被改成 Restrict，或 FK 被改成可為 null，那條不變式就不再由資料庫保證
		/// </summary>
		[Fact]
		public void 通知對規則的外鍵設定為串聯刪除且不可為空()
		{
			using var db = CreateDbContext(nameof(通知對規則的外鍵設定為串聯刪除且不可為空));

			var foreignKey = db.Model.FindEntityType(typeof(UserNotification))!
				.GetForeignKeys()
				.Single(fk => fk.PrincipalEntityType.ClrType == typeof(PestRuleConfig));

			Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
			Assert.False(foreignKey.Properties.Single().IsNullable);
		}

		/// <summary>
		/// 刪除規則會連帶刪除它產生的通知，未讀數跟著下降。
		/// <para>
		/// ⚠ 測試環境與生產環境走的不是同一條路：EF InMemory 沒有外鍵約束，
		/// 串聯刪除是由變更追蹤器做的、只作用在已經載入的相依列，
		/// 所以這裡要先把通知載進同一個 DbContext；生產環境是資料庫的外鍵在做，不需要載入。
		/// 兩者由同一份設定驅動（DeleteBehavior.Cascade），那份設定由上一條測試直接釘住。
		/// </para>
		/// 另一條規則的通知一起種進去：只驗自己那邊歸零的話，
		/// 一個「把整張表清空」的實作也會通過
		/// </summary>
		[Fact]
		public async Task 刪除規則會連帶刪除它的通知而且未讀數同步下降()
		{
			var dbName = nameof(刪除規則會連帶刪除它的通知而且未讀數同步下降);
			int ruleId, keptRuleId;
			await using (var db = CreateDbContext(dbName))
			{
				var doomed = SeedRule(ruleName: "要刪的規則");
				var kept = SeedRule(ruleName: "留著的規則");
				db.PestRuleConfigs.AddRange(doomed, kept);
				await db.SaveChangesAsync();
				ruleId = doomed.Id;
				keptRuleId = kept.Id;

				db.UserNotifications.AddRange(
					new UserNotification { UserId = "u1", PestRuleConfigId = ruleId, SourceRecordId = 1, Message = "要刪的一", TriggeredAt = FixedNow },
					new UserNotification { UserId = "u1", PestRuleConfigId = ruleId, SourceRecordId = 2, Message = "要刪的二", TriggeredAt = FixedNow },
					new UserNotification { UserId = "u1", PestRuleConfigId = keptRuleId, SourceRecordId = 3, Message = "留著的", TriggeredAt = FixedNow });
				await db.SaveChangesAsync();
			}

			await using (var db = CreateDbContext(dbName))
				Assert.Equal(3, (await new NotificationService(db).GetUnreadCountAsync("u1")).Count);

			await using (var db = CreateDbContext(dbName))
			{
				await db.UserNotifications.LoadAsync();
				Assert.True(await CreateService(db, dbName).DeleteRuleAsync(ruleId, "u1"));
			}

			await using (var db = CreateDbContext(dbName))
			{
				Assert.Equal(1, (await new NotificationService(db).GetUnreadCountAsync("u1")).Count);
				Assert.DoesNotContain(db.UserNotifications.ToList(), n => n.PestRuleConfigId == ruleId);
				Assert.Contains(db.UserNotifications.ToList(), n => n.PestRuleConfigId == keptRuleId);
			}
		}

		/// <summary>
		/// 刪除確認對話框要講得出「會一併刪掉幾則」，所以列表要帶著這個數字。
		/// 沒有通知的規則要回 0 而不是被漏掉
		/// </summary>
		[Fact]
		public async Task 列表帶出每條規則目前的通知則數()
		{
			var dbName = nameof(列表帶出每條規則目前的通知則數);
			await using var db = CreateDbContext(dbName);
			var withNotifications = SeedRule(ruleName: "有通知的");
			var withoutNotifications = SeedRule(ruleName: "沒通知的");
			db.PestRuleConfigs.AddRange(withNotifications, withoutNotifications);
			await db.SaveChangesAsync();
			db.UserNotifications.AddRange(
				new UserNotification { UserId = "u1", PestRuleConfigId = withNotifications.Id, SourceRecordId = 1, Message = "一", TriggeredAt = FixedNow },
				new UserNotification { UserId = "u1", PestRuleConfigId = withNotifications.Id, SourceRecordId = 2, Message = "二", TriggeredAt = FixedNow });
			await db.SaveChangesAsync();

			var result = await CreateService(db, dbName).GetRulesAsync("u1", new PagedQueryDto());

			Assert.Equal(2, result.Items.Single(x => x.RuleName == "有通知的").NotificationCount);
			Assert.Equal(0, result.Items.Single(x => x.RuleName == "沒通知的").NotificationCount);
		}

		// ── 立即檢查 ──────────────────────────────────────────────────────

		/// <summary>
		/// 「立即檢查」只評估呼叫者自己的規則，並回報最新觀測時刻與它夠不夠新——
		/// 少了後兩個值，「條件沒命中」與「同步 Worker 沒在跑」在畫面上長得一模一樣
		/// </summary>
		[Fact]
		public async Task 立即檢查只評估自己的規則並回報最新觀測時刻()
		{
			var dbName = nameof(立即檢查只評估自己的規則並回報最新觀測時刻);
			var observedAt = FixedNow.AddMinutes(-30);
			await using var db = CreateDbContext(dbName);
			db.PestRuleConfigs.AddRange(SeedRule(), SeedRule(userId: "u2"));
			db.WeatherObservations.Add(new WeatherObservation
			{
				StationId = "C0F9M0",
				StationName = "測試站",
				ObservedAt = observedAt,
				SyncedAt = observedAt,
				Temperature = 35.0m,
				CityCode = "66000",
				CityName = "臺中市",
				TownName = "大甲區"
			});
			await db.SaveChangesAsync();

			var outcome = await CreateService(db, dbName).EvaluateNowAsync("u1");

			Assert.Equal(1, outcome.RulesEvaluated);
			Assert.Equal(observedAt, outcome.LatestObservedAt);
			Assert.True(outcome.HasFreshObservation);

			await using var verify = CreateDbContext(dbName);
			Assert.All(verify.UserNotifications.ToList(), n => Assert.Equal("u1", n.UserId));
		}

		/// <summary>
		/// 同步中斷後最新的觀測會越來越舊，此時不論門檻設多少都不會有通知。
		/// 這個狀態要讓呼叫端分得出來，否則使用者按了按鈕沒反應只能猜是不是壞了
		/// </summary>
		[Fact]
		public async Task 最新觀測太舊時要回報資料不夠新()
		{
			var dbName = nameof(最新觀測太舊時要回報資料不夠新);
			var observedAt = FixedNow.AddDays(-19);
			await using var db = CreateDbContext(dbName);
			db.PestRuleConfigs.Add(SeedRule());
			db.WeatherObservations.Add(new WeatherObservation
			{
				StationId = "C0F9M0",
				StationName = "測試站",
				ObservedAt = observedAt,
				SyncedAt = FixedNow.AddMinutes(-5),
				Temperature = 35.0m,
				CityCode = "66000",
				CityName = "臺中市",
				TownName = "大甲區"
			});
			await db.SaveChangesAsync();

			var outcome = await CreateService(db, dbName).EvaluateNowAsync("u1");

			Assert.False(outcome.HasFreshObservation);
			Assert.Equal(observedAt, outcome.LatestObservedAt);
			Assert.Equal(0, outcome.NotificationsCreated);
		}
	}
}
