using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TaiwanAgri.Modules.Pet.Data;
using TaiwanAgri.Modules.Pet.Entities;
using TaiwanAgri.Modules.Pet.Services;
using TaiwanAgri.Modules.Weather.Data;
using TaiwanAgri.Modules.Weather.Entities;
using TaiwanAgri.Modules.Weather.Services;

namespace TaiwanAgri.Tests.Core
{
	/// <summary>
	/// 存成 UTC 的時間戳，回應給前端時要帶「這是 UTC」的標記（JSON 結尾的 Z）。
	/// <para>
	/// SQL Server 的 datetime2 不存時區，讀回來的 Kind 是 Unspecified，序列化就少了 Z，
	/// 瀏覽器把它當成本地時間，台灣的使用者看到的時間差 8 小時（剛產生的通知顯示「8 小時前」）。
	/// EF InMemory 會保留寫入時的 Kind，用 UtcNow 種資料的話這個差別完全看不出來——
	/// 這也是既有的服務測試沒有抓到這個問題的原因。所以這裡的種子資料刻意用 Kind=Unspecified，
	/// 模擬從 SQL Server 讀回來的樣子。
	/// </para>
	/// </summary>
	public class UtcTimestampResponseTests
	{
		/// <summary>2026-11-12 20:53:41 UTC＝台灣 11-13 04:53，沒有標記的話會被當成台灣晚上八點</summary>
		private static readonly DateTime StoredUtcWithoutKind =
			DateTime.SpecifyKind(new DateTime(2026, 11, 12, 20, 53, 41), DateTimeKind.Unspecified);

		private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

		private static void AssertUtcInJson(DateTime value, string json)
		{
			Assert.Equal(DateTimeKind.Utc, value.Kind);
			Assert.Equal(StoredUtcWithoutKind.Ticks, value.Ticks); // 只補標記，不換算時間
			Assert.Contains("\"2026-11-12T20:53:41Z\"", json);
		}

		[Fact]
		public async Task 通知列表的觸發時間帶UTC標記()
		{
			using var db = new WeatherDbContext(new DbContextOptionsBuilder<WeatherDbContext>()
				.UseInMemoryDatabase(nameof(通知列表的觸發時間帶UTC標記)).Options);
			var rule = new PestRuleConfig { UserId = "u1", RuleName = "高溫", RuleType = "Numeric", SourceTable = "PestDecade", IsActive = true };
			db.PestRuleConfigs.Add(rule);
			await db.SaveChangesAsync();
			db.UserNotifications.Add(new UserNotification
			{
				UserId = "u1", PestRuleConfigId = rule.Id, Message = "m", TriggeredAt = StoredUtcWithoutKind
			});
			await db.SaveChangesAsync();

			var page = await new NotificationService(db).GetUserNotificationsAsync("u1", 1);

			var item = Assert.Single(page.Items);
			AssertUtcInJson(item.TriggeredAt, JsonSerializer.Serialize(item, WebJson));
		}

		[Fact]
		public async Task 通知規則的建立時間帶UTC標記()
		{
			var name = nameof(通知規則的建立時間帶UTC標記);
			using var db = new WeatherDbContext(new DbContextOptionsBuilder<WeatherDbContext>()
				.UseInMemoryDatabase(name).Options);
			var rule = new PestRuleConfig
			{
				UserId = "u1", RuleName = "高溫", RuleType = "Numeric", SourceTable = "PestDecade",
				IsActive = true, CreatedAt = StoredUtcWithoutKind
			};
			db.PestRuleConfigs.Add(rule);
			await db.SaveChangesAsync();

			var services = new ServiceCollection();
			services.AddDbContext<WeatherDbContext>(o => o.UseInMemoryDatabase(name));
			var engine = new PestRuleEngine(
				NullLogger<PestRuleEngine>.Instance,
				services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
				TimeProvider.System);
			var service = new NotificationRuleService(db, engine, TimeProvider.System);

			var dto = await service.GetRuleByIdAsync(rule.Id, "u1");

			Assert.NotNull(dto);
			AssertUtcInJson(dto.CreatedAt, JsonSerializer.Serialize(dto, WebJson));
		}

		[Fact]
		public async Task 走失啟事的建立與更新時間帶UTC標記()
		{
			using var db = new PetDbContext(new DbContextOptionsBuilder<PetDbContext>()
				.UseInMemoryDatabase(nameof(走失啟事的建立與更新時間帶UTC標記)).Options);
			var post = new LostPetPost
			{
				UserId = "u1", Title = "小白", Description = "d", Phone = "0912345678",
				CreatedAt = StoredUtcWithoutKind, UpdatedAt = StoredUtcWithoutKind
			};
			db.LostPetPosts.Add(post);
			await db.SaveChangesAsync();

			var dto = await new PetService(db, TimeProvider.System).GetLostPetPostByIdAsync(post.Id, null);

			Assert.NotNull(dto);
			var json = JsonSerializer.Serialize(dto, WebJson);
			AssertUtcInJson(dto.CreatedAt, json);
			AssertUtcInJson(dto.UpdatedAt, json);
		}
	}
}
