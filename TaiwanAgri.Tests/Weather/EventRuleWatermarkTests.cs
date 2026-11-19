using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TaiwanAgri.Modules.Weather.Data;
using TaiwanAgri.Modules.Weather.Dtos.ApiRequests;
using TaiwanAgri.Modules.Weather.Entities;
using TaiwanAgri.Modules.Weather.Services;

namespace TaiwanAgri.Tests.Weather
{
	/// <summary>
	/// 事件型規則的水位（2026-11 總檢查 3-B1）。
	/// 原本事件型沒有水位、只靠「那則通知還在不在」去重：通知過期被刪掉之後，下一輪同一則舊警報又符合條件，
	/// 會再通知一次。這組測試把那個時間差壓成「把通知刪掉、再評估一次」
	/// </summary>
	public class EventRuleWatermarkTests
	{
		private static readonly DateTime T0 = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

		private static (PestRuleEngine Engine, Func<WeatherDbContext> OpenDb, ServiceProvider Provider) CreateEngine(string name)
		{
			var services = new ServiceCollection();
			services.AddDbContext<WeatherDbContext>(o => o.UseInMemoryDatabase(name));
			var provider = services.BuildServiceProvider();
			var engine = new PestRuleEngine(
				NullLogger<PestRuleEngine>.Instance,
				provider.GetRequiredService<IServiceScopeFactory>(),
				TimeProvider.System);
			return (engine, () => new WeatherDbContext(
				new DbContextOptionsBuilder<WeatherDbContext>().UseInMemoryDatabase(name).Options), provider);
		}

		private static PestRuleConfig EventRule() => new()
		{
			UserId = "u1", RuleName = "疫情", RuleType = "Event", SourceTable = "PlantEpidemic",
			IsActive = true, ExpiryDays = 30, FilterCity = "臺中市"
		};

		private static PestAlert Alert(string subject, DateTime syncedAt) => new()
		{
			Subject = subject, Body = "b", Prescription = "p", PubDate = new DateOnly(2026, 9, 1), Issue = "i",
			SourceHash = subject, SyncedAt = syncedAt,
			Cities = [new PestAlertCity { CityName = "台中市" }],
			Crops = [new PestAlertCrop { CropName = "柑橘" }]
		};

		[Fact]
		public async Task 通知過期刪除後同一則舊警報不會再通知一次()
		{
			var (engine, openDb, _) = CreateEngine(nameof(通知過期刪除後同一則舊警報不會再通知一次));
			await using (var db = openDb())
			{
				db.PestRuleConfigs.Add(EventRule());
				db.PestAlerts.Add(Alert("舊警報", T0));
				await db.SaveChangesAsync();
			}

			await engine.EvaluateAsync(null, CancellationToken.None);
			await using (var db = openDb())
			{
				Assert.Equal(1, await db.UserNotifications.CountAsync());
				// 模擬保留天數到了、通知被刪掉
				db.UserNotifications.RemoveRange(db.UserNotifications);
				await db.SaveChangesAsync();
			}

			await engine.EvaluateAsync(null, CancellationToken.None);

			await using (var db = openDb())
				Assert.Equal(0, await db.UserNotifications.CountAsync());
		}

		[Fact]
		public async Task 上次評估之後才落地的警報照樣通知()
		{
			var (engine, openDb, _) = CreateEngine(nameof(上次評估之後才落地的警報照樣通知));
			await using (var db = openDb())
			{
				db.PestRuleConfigs.Add(EventRule());
				db.PestAlerts.Add(Alert("第一則", T0));
				await db.SaveChangesAsync();
			}
			await engine.EvaluateAsync(null, CancellationToken.None);

			await using (var db = openDb())
			{
				db.PestAlerts.Add(Alert("隔天新同步的", T0.AddDays(1)));
				await db.SaveChangesAsync();
			}
			await engine.EvaluateAsync(null, CancellationToken.None);

			await using (var db = openDb())
			{
				var messages = await db.UserNotifications.Select(n => n.Message).ToListAsync();
				Assert.Equal(2, messages.Count);
				Assert.Contains(messages, m => m.Contains("隔天新同步的"));
			}
		}

		[Fact]
		public async Task 改了事件型的條件之後重新比對全部警報()
		{
			// 畫面上的說明是「改條件後按立即檢查，就會補上符合新條件的疫情警報」，水位要在改條件時清掉
			var name = nameof(改了事件型的條件之後重新比對全部警報);
			var (engine, openDb, provider) = CreateEngine(name);
			int ruleId;
			await using (var db = openDb())
			{
				var rule = EventRule();
				rule.FilterCity = "高雄市"; // 一開始的條件比不到
				db.PestRuleConfigs.Add(rule);
				db.PestAlerts.Add(Alert("臺中的舊警報", T0));
				await db.SaveChangesAsync();
				ruleId = rule.Id;
			}
			await engine.EvaluateAsync(null, CancellationToken.None);

			await using (var db = openDb())
			{
				Assert.NotNull((await db.PestRuleConfigs.SingleAsync()).LastEvaluatedAt);
				var service = new NotificationRuleService(db, engine, TimeProvider.System);
				await service.UpdateRuleAsync(ruleId, "u1", new NotificationRuleRequestDto
				{
					RuleName = "疫情", RuleType = "Event", SourceTable = "PlantEpidemic", IsActive = true,
					FilterCity = "臺中市"
				});
				Assert.Null((await db.PestRuleConfigs.SingleAsync()).LastEvaluatedAt);
			}
			await engine.EvaluateAsync(null, CancellationToken.None);

			await using (var db = openDb())
				Assert.Contains("臺中的舊警報", (await db.UserNotifications.SingleAsync()).Message);
			GC.KeepAlive(provider);
		}

		[Fact]
		public async Task 從事件型改成數值型時水位清掉()
		{
			// 兩種型態的水位是不同資料表的落地時刻，沿用的話數值型會拿警報的時刻去比觀測
			var (engine, openDb, _) = CreateEngine(nameof(從事件型改成數值型時水位清掉));
			int ruleId;
			await using (var db = openDb())
			{
				var rule = EventRule();
				rule.LastEvaluatedAt = T0;
				db.PestRuleConfigs.Add(rule);
				await db.SaveChangesAsync();
				ruleId = rule.Id;
			}

			await using (var db = openDb())
			{
				var service = new NotificationRuleService(db, engine, TimeProvider.System);
				await service.UpdateRuleAsync(ruleId, "u1", new NotificationRuleRequestDto
				{
					RuleName = "高溫", RuleType = "Numeric", SourceTable = "WeatherObservation", IsActive = true,
					MetricName = "Temperature", Comparison = "GreaterThan", Threshold = 30m
				});
				Assert.Null((await db.PestRuleConfigs.SingleAsync()).LastEvaluatedAt);
			}
		}
	}
}
