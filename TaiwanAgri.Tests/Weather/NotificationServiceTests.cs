using Microsoft.EntityFrameworkCore;
using TaiwanAgri.Modules.Weather.Data;
using TaiwanAgri.Modules.Weather.Entities;
using TaiwanAgri.Modules.Weather.Services;

namespace TaiwanAgri.Tests.Weather
{
	/// <summary>
	/// NotificationService 四支方法。唯一依賴是 WeatherDbContext，所以直接用 InMemory 資料庫，
	/// 不需要 Mock。重點在三處會靜默出錯的地方：分頁的「總筆數剛好是每頁筆數倍數」邊界、
	/// 標記已讀的使用者歸屬檢查、以及規則名稱來自導覽屬性這件事
	/// </summary>
	public class NotificationServiceTests
	{
		/// <summary>排序基準時間；每筆通知往前推不同分鐘數，避免時間相同讓排序斷言不穩定</summary>
		private static readonly DateTime BaseTime = new(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc);

		/// <summary>
		/// InMemory 資料庫依名稱共用，所以每個測試各給一個唯一名稱避免互相污染；
		/// 同名的兩個 DbContext 會看到同一份資料，驗證存檔時要靠這個特性
		/// </summary>
		private static WeatherDbContext CreateDbContext(string databaseName) =>
			new(new DbContextOptionsBuilder<WeatherDbContext>()
				.UseInMemoryDatabase(databaseName)
				.Options);

		/// <summary>
		/// 種一筆規則並回傳它的 Id。列表查詢的 RuleName 取自導覽屬性，指向不存在的規則會讓
		/// 整筆通知在投影時被 JOIN 掉（筆數與 HasMore 都會被扭曲），所以每筆通知都要指到真規則；
		/// 一筆規則共用給全部通知即可
		/// </summary>
		private static async Task<int> SeedRuleAsync(WeatherDbContext db, string ruleName = "高溫警戒")
		{
			var rule = new PestRuleConfig
			{
				UserId = "rule-owner",
				RuleName = ruleName,
				RuleType = "Numeric",
				SourceTable = "PestDecade",
				IsActive = true
			};
			db.PestRuleConfigs.Add(rule);
			await db.SaveChangesAsync();
			return rule.Id;
		}

		/// <summary>minutesAgo 越大代表越舊；Message 預設帶上它，方便斷言順序與分頁切點</summary>
		private static UserNotification Notification(
			string userId, int ruleId, int minutesAgo, bool isRead = false, string? message = null) => new()
			{
				UserId = userId,
				PestRuleConfigId = ruleId,
				Message = message ?? $"通知 {minutesAgo}",
				TriggeredAt = BaseTime.AddMinutes(-minutesAgo),
				IsRead = isRead
			};

		/// <summary>種 count 筆屬於同一個使用者的通知，時間由新到舊</summary>
		private static async Task SeedNotificationsAsync(WeatherDbContext db, string userId, int ruleId, int count)
		{
			for (var i = 0; i < count; i++)
				db.UserNotifications.Add(Notification(userId, ruleId, i));
			await db.SaveChangesAsync();
		}

		// ── GetUserNotificationsAsync ────────────────────────────────────────

		[Fact]
		public async Task 總筆數剛好等於每頁筆數時第一頁不能說還有下一頁()
		{
			// 這是後端多撈一筆才有意義的那個邊界：舊做法看「這頁滿了沒」，滿 20 就說還有下一頁，
			// 使用者點下去拿到空陣列；新做法撈得到第 21 筆才說有
			using var db = CreateDbContext(nameof(總筆數剛好等於每頁筆數時第一頁不能說還有下一頁));
			var ruleId = await SeedRuleAsync(db);
			await SeedNotificationsAsync(db, "u1", ruleId, INotificationService.PageSize);

			var result = await new NotificationService(db).GetUserNotificationsAsync("u1", 1);

			Assert.Equal(INotificationService.PageSize, result.Items.Count);
			Assert.False(result.HasMore);
		}

		[Fact]
		public async Task 多出一筆時第一頁只回每頁筆數並說還有下一頁()
		{
			// 多撈的那筆只用來判斷 HasMore，不能混進回傳結果
			using var db = CreateDbContext(nameof(多出一筆時第一頁只回每頁筆數並說還有下一頁));
			var ruleId = await SeedRuleAsync(db);
			await SeedNotificationsAsync(db, "u1", ruleId, INotificationService.PageSize + 1);

			var result = await new NotificationService(db).GetUserNotificationsAsync("u1", 1);

			Assert.Equal(INotificationService.PageSize, result.Items.Count);
			Assert.True(result.HasMore);
			// 最舊的那筆（minutesAgo = PageSize）該留在第 2 頁
			Assert.DoesNotContain(result.Items, x => x.Message == $"通知 {INotificationService.PageSize}");
		}

		[Fact]
		public async Task 第二頁要跳過第一頁的資料且用完資料時說沒有下一頁()
		{
			// 只測第 1 頁看不出 Skip 有沒有生效（第 1 頁本來就跳過 0 筆），所以要有一條非第一頁的
			using var db = CreateDbContext(nameof(第二頁要跳過第一頁的資料且用完資料時說沒有下一頁));
			var ruleId = await SeedRuleAsync(db);
			await SeedNotificationsAsync(db, "u1", ruleId, INotificationService.PageSize * 2);

			var result = await new NotificationService(db).GetUserNotificationsAsync("u1", 2);

			Assert.Equal(INotificationService.PageSize, result.Items.Count);
			Assert.False(result.HasMore);
			Assert.Equal($"通知 {INotificationService.PageSize}", result.Items.First().Message);
			Assert.Equal($"通知 {INotificationService.PageSize * 2 - 1}", result.Items.Last().Message);
		}

		[Fact]
		public async Task 列表只回自己的通知()
		{
			using var db = CreateDbContext(nameof(列表只回自己的通知));
			var ruleId = await SeedRuleAsync(db);
			db.UserNotifications.AddRange(
				Notification("u1", ruleId, 1, message: "我的 1"),
				Notification("u1", ruleId, 2, message: "我的 2"),
				Notification("u1", ruleId, 3, message: "我的 3"),
				Notification("u2", ruleId, 4, message: "別人的 1"),
				Notification("u2", ruleId, 5, message: "別人的 2"));
			await db.SaveChangesAsync();

			var result = await new NotificationService(db).GetUserNotificationsAsync("u1", 1);

			Assert.Equal(3, result.Items.Count);
			Assert.All(result.Items, x => Assert.StartsWith("我的", x.Message));
		}

		[Fact]
		public async Task 列表依觸發時間由新到舊排序()
		{
			// 插入順序刻意打亂：若排序那行被拿掉，InMemory 會照插入順序回傳，斷言才抓得到
			using var db = CreateDbContext(nameof(列表依觸發時間由新到舊排序));
			var ruleId = await SeedRuleAsync(db);
			db.UserNotifications.AddRange(
				Notification("u1", ruleId, 2, message: "中間"),
				Notification("u1", ruleId, 3, message: "最舊"),
				Notification("u1", ruleId, 1, message: "最新"));
			await db.SaveChangesAsync();

			var result = await new NotificationService(db).GetUserNotificationsAsync("u1", 1);

			Assert.Equal(new[] { "最新", "中間", "最舊" }, result.Items.Select(x => x.Message));
		}

		[Fact]
		public async Task 列表帶出關聯規則的名稱()
		{
			// RuleName 不在通知表裡，是從關聯的規則取的；這條壞掉時不會有任何錯誤訊息
			using var db = CreateDbContext(nameof(列表帶出關聯規則的名稱));
			var ruleId = await SeedRuleAsync(db, "高溫警戒");
			await SeedNotificationsAsync(db, "u1", ruleId, 1);

			var result = await new NotificationService(db).GetUserNotificationsAsync("u1", 1);

			Assert.Equal("高溫警戒", Assert.Single(result.Items).RuleName);
		}

		// ── GetUnreadCountAsync ──────────────────────────────────────────────

		[Fact]
		public async Task 未讀數不計入已讀的通知()
		{
			using var db = CreateDbContext(nameof(未讀數不計入已讀的通知));
			var ruleId = await SeedRuleAsync(db);
			db.UserNotifications.AddRange(
				Notification("u1", ruleId, 1),
				Notification("u1", ruleId, 2),
				Notification("u1", ruleId, 3, isRead: true),
				Notification("u1", ruleId, 4, isRead: true),
				Notification("u1", ruleId, 5, isRead: true));
			await db.SaveChangesAsync();

			var result = await new NotificationService(db).GetUnreadCountAsync("u1");

			Assert.Equal(2, result.Count);
		}

		[Fact]
		public async Task 未讀數不計入別人的通知()
		{
			using var db = CreateDbContext(nameof(未讀數不計入別人的通知));
			var ruleId = await SeedRuleAsync(db);
			db.UserNotifications.AddRange(
				Notification("u1", ruleId, 1),
				Notification("u1", ruleId, 2),
				Notification("u2", ruleId, 3),
				Notification("u2", ruleId, 4),
				Notification("u2", ruleId, 5),
				Notification("u2", ruleId, 6));
			await db.SaveChangesAsync();

			var result = await new NotificationService(db).GetUnreadCountAsync("u1");

			Assert.Equal(2, result.Count);
		}

		// ── MarkAsReadAsync ──────────────────────────────────────────────────

		[Fact]
		public async Task 標記已讀要真的存進資料庫()
		{
			// 驗證一定要換一個 DbContext：同一個 context 會讀到追蹤中的實體，
			// 就算 SaveChanges 被拿掉也是 true，測試會綠得沒有意義
			var dbName = nameof(標記已讀要真的存進資料庫);
			int notificationId;
			using (var seed = CreateDbContext(dbName))
			{
				var ruleId = await SeedRuleAsync(seed);
				var notification = Notification("u1", ruleId, 1);
				seed.UserNotifications.Add(notification);
				await seed.SaveChangesAsync();
				notificationId = notification.Id;
			}

			using (var act = CreateDbContext(dbName))
				await new NotificationService(act).MarkAsReadAsync(notificationId, "u1");

			using var verify = CreateDbContext(dbName);
			Assert.True(verify.UserNotifications.Single(x => x.Id == notificationId).IsRead);
		}

		[Fact]
		public async Task 標記別人的通知要被擋下而且不能改到那筆資料()
		{
			// 查詢條件少了 UserId 那半時，別人的通知會被標成已讀。只斷言有丟例外抓不到
			// 「先改了才丟例外」的寫法，所以要回頭確認那筆仍是未讀
			var dbName = nameof(標記別人的通知要被擋下而且不能改到那筆資料);
			int othersNotificationId;
			using (var seed = CreateDbContext(dbName))
			{
				var ruleId = await SeedRuleAsync(seed);
				var notification = Notification("u2", ruleId, 1);
				seed.UserNotifications.Add(notification);
				await seed.SaveChangesAsync();
				othersNotificationId = notification.Id;
			}

			using (var act = CreateDbContext(dbName))
			{
				var service = new NotificationService(act);
				await Assert.ThrowsAsync<KeyNotFoundException>(
					() => service.MarkAsReadAsync(othersNotificationId, "u1"));
			}

			using var verify = CreateDbContext(dbName);
			Assert.False(verify.UserNotifications.Single(x => x.Id == othersNotificationId).IsRead);
		}

		[Fact]
		public async Task 標記不存在的通知要丟出找不到的例外()
		{
			// 少了 null 檢查會變成 NullReferenceException，Controller 就轉不成 404
			using var db = CreateDbContext(nameof(標記不存在的通知要丟出找不到的例外));
			var ruleId = await SeedRuleAsync(db);
			await SeedNotificationsAsync(db, "u1", ruleId, 1);

			var service = new NotificationService(db);

			await Assert.ThrowsAsync<KeyNotFoundException>(() => service.MarkAsReadAsync(9999, "u1"));
		}

		// ── MarkAllAsReadAsync ───────────────────────────────────────────────

		[Fact]
		public async Task 全部已讀回傳實際更新筆數並存進資料庫()
		{
			var dbName = nameof(全部已讀回傳實際更新筆數並存進資料庫);
			using (var seed = CreateDbContext(dbName))
			{
				var ruleId = await SeedRuleAsync(seed);
				seed.UserNotifications.AddRange(
					Notification("u1", ruleId, 1),
					Notification("u1", ruleId, 2),
					Notification("u1", ruleId, 3),
					Notification("u1", ruleId, 4, isRead: true),
					Notification("u1", ruleId, 5, isRead: true));
				await seed.SaveChangesAsync();
			}

			int affected;
			using (var act = CreateDbContext(dbName))
				affected = await new NotificationService(act).MarkAllAsReadAsync("u1");

			// 回傳的是「這次真的被改掉的筆數」，不是該使用者的通知總數
			Assert.Equal(3, affected);
			using var verify = CreateDbContext(dbName);
			Assert.All(verify.UserNotifications.ToList(), x => Assert.True(x.IsRead));
		}

		[Fact]
		public async Task 沒有未讀時回零而且不進資料庫()
		{
			// 零筆時提早 return 是刻意的：省掉一次沒有任何變更的 SaveChanges。
			// 只斷言回傳 0 看不出這件事（拿掉早退一樣回 0），所以訂閱 SavedChanges 事件來確認
			using var db = CreateDbContext(nameof(沒有未讀時回零而且不進資料庫));
			var ruleId = await SeedRuleAsync(db);
			db.UserNotifications.AddRange(
				Notification("u1", ruleId, 1, isRead: true),
				Notification("u1", ruleId, 2, isRead: true));
			await db.SaveChangesAsync();

			var saveCount = 0;
			db.SavedChanges += (_, _) => saveCount++;

			var affected = await new NotificationService(db).MarkAllAsReadAsync("u1");

			Assert.Equal(0, affected);
			Assert.Equal(0, saveCount);
		}

		[Fact]
		public async Task 全部已讀不能動到別人的通知()
		{
			var dbName = nameof(全部已讀不能動到別人的通知);
			using (var seed = CreateDbContext(dbName))
			{
				var ruleId = await SeedRuleAsync(seed);
				seed.UserNotifications.AddRange(
					Notification("u1", ruleId, 1),
					Notification("u1", ruleId, 2),
					Notification("u2", ruleId, 3),
					Notification("u2", ruleId, 4),
					Notification("u2", ruleId, 5));
				await seed.SaveChangesAsync();
			}

			int affected;
			using (var act = CreateDbContext(dbName))
				affected = await new NotificationService(act).MarkAllAsReadAsync("u1");

			Assert.Equal(2, affected);
			using var verify = CreateDbContext(dbName);
			Assert.All(verify.UserNotifications.Where(x => x.UserId == "u2").ToList(),
				x => Assert.False(x.IsRead));
		}
	}
}
