using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TaiwanAgri.Modules.Market.Data;
using TaiwanAgri.Modules.Market.Entities;
using TaiwanAgri.Modules.Weather.Data;
using TaiwanAgri.Modules.Weather.Entities;

namespace TaiwanAgri.IntegrationTests
{
	/// <summary>
	/// 單元測試抓不到、2026-11 總檢查實際查到的幾種錯（G-B2），在真的 SQL Server 與完整的 HTTP 管線上各釘一條：
	/// <list type="bullet">
	/// <item>EF InMemory 讀回來的時間保留寫入時的 UTC 標記，SQL Server 的 datetime2 不存時區、讀回來沒有標記（3-A1）</item>
	/// <item>LINQ 在 InMemory 照 C# 執行，在 SQL Server 要翻成 SQL：排序、分頁、分組的結果以資料庫為準（3-A7、R5-21）</item>
	/// <item>單元測試的種子資料照查詢條件造，真實資料的形狀不一樣（3-A2、1-B2）</item>
	/// <item>參數從網址進來、經過模型繫結與驗證，才到控制器（3-A6）</item>
	/// </list>
	/// 每條測試用自己的資料（不同的縣市、作物代碼），共用同一個資料庫也互不影響。
	/// </summary>
	[Collection(SqlServerCollection.Name)]
	public class RealSqlServerTests(SqlServerAppFixture fixture)
	{
		private static readonly TimeSpan ClockTolerance = TimeSpan.FromMinutes(5);

		/// <summary>台灣的今天（伺服器時鐘是 UTC）</summary>
		private static DateOnly TaiwanToday => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(8));

		[SqlServerFact]
		public async Task 回應裡的時間從資料庫讀回來仍標成UTC()
		{
			var client = await SignedInClientAsync();

			var created = await ReadJsonAsync(await client.PostAsJsonAsync("/api/pet/lost-pet-posts",
				new { title = "整合測試啟事", description = "d", county = "臺南市", phone = "0900000000", email = "", photoUrl = "" }));
			var post = await ReadJsonAsync(await client.GetAsync($"/api/pet/lost-pet-posts/{created.GetProperty("id").GetInt32()}"));
			AssertUtcNow(post, "createdAt");
			AssertUtcNow(post, "updatedAt");

			var saved = await client.PutAsJsonAsync("/api/profile/farm", new { farmCity = "臺南市", farmType = "蔬菜", crops = Array.Empty<object>() });
			Assert.Equal(HttpStatusCode.NoContent, saved.StatusCode);
			var farm = await ReadJsonAsync(await client.GetAsync("/api/profile/farm"));
			AssertUtcNow(farm, "createdAt");
			AssertUtcNow(farm, "updatedAt");

			await ReadJsonAsync(await client.PostAsJsonAsync("/api/NotificationRule",
				new { ruleName = "整合測試規則", ruleType = "Event", sourceTable = "PlantEpidemic", isActive = true, filterCity = "澎湖縣" }));
			var rules = await ReadJsonAsync(await client.GetAsync("/api/NotificationRule"));
			AssertUtcNow(rules.GetProperty("items")[0], "createdAt");
		}

		[SqlServerFact]
		public async Task 疫情規則用臺比得到存台的警報_第二次檢查不重發_通知時間標成UTC()
		{
			// 2026-11 之前同步進來的警報存的是「台」（1-B2），規則選單用的是「臺」
			await SeedAsync<WeatherDbContext>(db => db.PestAlerts.Add(Alert("整合測試：南部疫情", TaiwanToday, "台南市")));
			var client = await SignedInClientAsync();
			await ReadJsonAsync(await client.PostAsJsonAsync("/api/NotificationRule",
				new { ruleName = "南部疫情", ruleType = "Event", sourceTable = "PlantEpidemic", isActive = true, filterCity = "臺南市" }));

			var first = await ReadJsonAsync(await client.PostAsync("/api/NotificationRule/evaluate", null));
			var second = await ReadJsonAsync(await client.PostAsync("/api/NotificationRule/evaluate", null));

			Assert.Equal(1, first.GetProperty("notificationsCreated").GetInt32());
			Assert.Equal(0, second.GetProperty("notificationsCreated").GetInt32()); // 評估水位（3-B1）
			var notifications = await ReadJsonAsync(await client.GetAsync("/api/Notification/list"));
			var item = Assert.Single(notifications.GetProperty("items").EnumerateArray());
			Assert.Contains("南部疫情", item.GetProperty("message").GetString());
			AssertUtcNow(item, "triggeredAt");
		}

		[SqlServerFact]
		public async Task 病蟲害警報_同一天的幾則翻頁不重複也不漏_縣市計數合併兩種寫法_每頁上限擋得住()
		{
			var sameDay = TaiwanToday.AddDays(-30);
			await SeedAsync<WeatherDbContext>(db =>
			{
				for (var i = 0; i < 5; i++)
					db.PestAlerts.Add(Alert($"整合測試：東部疫情{i}", sameDay, "台東縣"));
			});
			var client = fixture.Factory.CreateClient();
			var city = Uri.EscapeDataString("臺東縣");

			var seen = new List<int>();
			for (var page = 1; page <= 3; page++)
			{
				var result = await ReadJsonAsync(await client.GetAsync($"/api/Pest/alerts?cityName={city}&page={page}&pageSize=2"));
				Assert.Equal(5, result.GetProperty("totalCount").GetInt32());
				foreach (var alert in result.GetProperty("items").EnumerateArray())
				{
					seen.Add(alert.GetProperty("id").GetInt32());
					Assert.Contains("臺東縣", alert.GetProperty("cities").EnumerateArray().Select(c => c.GetString())); // 回傳換成正式寫法
				}
			}
			// 只比發布日的話，同一天的幾則先後由 SQL Server 的執行計畫決定，翻頁可能重複或漏掉（3-A7）
			Assert.Equal(5, seen.Distinct().Count());
			Assert.Equal(seen.OrderByDescending(id => id), seen);

			var counts = await ReadJsonAsync(await client.GetAsync("/api/Pest/alerts/city-counts"));
			var east = Assert.Single(counts.EnumerateArray(), c => c.GetProperty("cityName").GetString() == "臺東縣");
			Assert.Equal(5, east.GetProperty("count").GetInt32());

			var tooLarge = await client.GetAsync("/api/Pest/alerts?page=1&pageSize=500");
			Assert.Equal(HttpStatusCode.BadRequest, tooLarge.StatusCode);
		}

		[SqlServerFact]
		public async Task 水果行情算得到主檔記成蔬菜的市場()
		{
			// 真實資料的形狀（3-A2）：市場主檔把 109 台北一記成蔬菜市場，但它也有水果交易（TcType N05）。
			// 原本的查詢拿主檔的市場類別篩，水果行情就漏掉這個市場
			var day = TaiwanToday.AddDays(-3);
			await SeedAsync<MarketDbContext>(db =>
			{
				db.CropInfos.Add(new CropInfo { CropCode = "ITA1", CropName = "整合測試香蕉" });
				db.MarketInfos.AddRange(
					new MarketInfo { MarketCode = "109", MarketName = "台北一", MarketType = "Veg" },
					new MarketInfo { MarketCode = "423", MarketName = "東勢鎮", MarketType = "Fruit" });
				db.AgriProductsTrans.AddRange(
					Trade("N05", "109", "ITA1", day, 30m, 1000m),
					Trade("N05", "423", "ITA1", day, 20m, 100m));
			});
			var client = fixture.Factory.CreateClient();

			var rows = await ReadJsonAsync(await client.GetAsync(
				$"/api/Market/prices?marketType=Fruit&cropCodes=ITA1&startDate={day:yyyy-MM-dd}&endDate={day:yyyy-MM-dd}"));

			var row = Assert.Single(rows.EnumerateArray());
			Assert.Equal(1100m, row.GetProperty("transQuantity").GetDecimal());
			Assert.Equal(25m, row.GetProperty("avgPrice").GetDecimal()); // 全台均價＝各市場均價的平均
		}

		[SqlServerFact]
		public async Task 只帶起日也擋得住超過上限的查詢區間()
		{
			// 沒帶迄日時原本不檢查區間長度，從 2000 年查到今天會一路查下去（3-A6）
			var client = fixture.Factory.CreateClient();

			var response = await client.GetAsync("/api/Market/prices?marketType=Veg&cropCodes=LA1&startDate=2000-01-01");

			Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
			Assert.Contains("不得超過", await response.Content.ReadAsStringAsync());
		}

		// ── 共用 ────────────────────────────────────────────────

		/// <summary>註冊一個新帳號（帳密每次隨機、只活在這個測試裡），回傳帶著登入憑證的 client</summary>
		private async Task<HttpClient> SignedInClientAsync()
		{
			var client = fixture.Factory.CreateClient();
			var registered = await ReadJsonAsync(await client.PostAsJsonAsync("/api/Auth/register", new
			{
				email = $"it-{Guid.NewGuid():N}@example.test",
				password = $"Aa1!{Guid.NewGuid():N}",
				userType = "Farmer",
			}));
			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", registered.GetProperty("token").GetString());
			return client;
		}

		private async Task SeedAsync<TContext>(Action<TContext> add) where TContext : Microsoft.EntityFrameworkCore.DbContext
		{
			using var scope = fixture.Factory.Services.CreateScope();
			var db = scope.ServiceProvider.GetRequiredService<TContext>();
			add(db);
			await db.SaveChangesAsync();
		}

		private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
		{
			var body = await response.Content.ReadAsStringAsync();
			Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode} {response.RequestMessage?.RequestUri}：{body}");
			return JsonDocument.Parse(body).RootElement.Clone();
		}

		/// <summary>
		/// 時間字串要明講是 UTC（結尾 Z），而且換算回來跟現在差不到幾分鐘。
		/// 沒標的話瀏覽器當成台灣時間，畫面就差 8 小時（3-A1：鈴鐺上剛產生的通知顯示「8 小時前」）
		/// </summary>
		private static void AssertUtcNow(JsonElement owner, string property)
		{
			var text = owner.GetProperty(property).GetString();
			Assert.NotNull(text);
			Assert.EndsWith("Z", text);
			var value = DateTimeOffset.Parse(text, CultureInfo.InvariantCulture);
			Assert.InRange(value, DateTimeOffset.UtcNow - ClockTolerance, DateTimeOffset.UtcNow + ClockTolerance);
		}

		private static PestAlert Alert(string subject, DateOnly pubDate, string city) => new()
		{
			Subject = subject, Body = "b", Prescription = "p", PubDate = pubDate, Issue = "i",
			SourceHash = Guid.NewGuid().ToString("N"), SyncedAt = DateTime.UtcNow,
			Cities = [new PestAlertCity { CityName = city }],
		};

		private static AgriProductsTrans Trade(string tcType, string marketCode, string cropCode, DateOnly date, decimal avg, decimal qty) => new()
		{
			TcType = tcType, MarketCode = marketCode, CropCode = cropCode, TransDate = date,
			AvgPrice = avg, UpperPrice = avg, MiddlePrice = avg, LowerPrice = avg, TransQuantity = qty,
		};
	}
}
