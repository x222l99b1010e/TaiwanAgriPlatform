using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using TaiwanAgri.Modules.Weather.Data;
using TaiwanAgri.Modules.Weather.Dtos.Queries;
using TaiwanAgri.Modules.Weather.Entities;
using TaiwanAgri.Modules.Weather.Services;
using TaiwanAgri.Worker.Weather;

namespace TaiwanAgri.Tests.Weather
{
	/// <summary>
	/// 病蟲害警報的縣市寫法（台／臺，2026-11 總檢查 1-B2）、分頁的界限與決勝鍵（3-A7）、
	/// 縣市計數端點，以及同步時的批次內去重（3-B8）
	/// </summary>
	public class PestAlertCityAndPagingTests
	{
		private static WeatherDbContext CreateDb(string name) =>
			new(new DbContextOptionsBuilder<WeatherDbContext>().UseInMemoryDatabase(name).Options);

		private static PestAlert Alert(string subject, DateOnly pubDate, params string[] cities) => new()
		{
			Subject = subject, Body = "b", Prescription = "p", PubDate = pubDate, Issue = "i",
			SourceHash = subject + pubDate,
			Cities = [.. cities.Select(c => new PestAlertCity { CityName = c })],
		};

		[Fact]
		public async Task 舊資料存台的警報用臺也查得到而且回傳正式寫法()
		{
			// 2026-11 之前同步進來的警報存的是「台」。原本精確比對，選臺南市永遠 0 筆
			using var db = CreateDb(nameof(舊資料存台的警報用臺也查得到而且回傳正式寫法));
			db.PestAlerts.Add(Alert("南部疫情", new DateOnly(2026, 9, 1), "台南市", "高雄市"));
			await db.SaveChangesAsync();

			var item = Assert.Single((await new PestService(db).GetPestAlertsByCityAsync("臺南市")).Items);

			Assert.Equal(new[] { "臺南市", "高雄市" }, item.Cities);
		}

		[Fact]
		public async Task 同一發布日的警報翻頁不重複也不漏()
		{
			// 只比發布日的話，同日的幾筆先後由資料庫決定，翻頁可能重複或漏掉；加了 Id 決勝鍵就固定
			using var db = CreateDb(nameof(同一發布日的警報翻頁不重複也不漏));
			var sameDay = new DateOnly(2026, 9, 1);
			for (var i = 0; i < 5; i++)
				db.PestAlerts.Add(Alert($"疫情{i}", sameDay, "臺北市"));
			await db.SaveChangesAsync();
			var service = new PestService(db);

			var seen = new List<int>();
			for (var page = 1; page <= 3; page++)
				seen.AddRange((await service.GetPestAlertsByCityAsync(null, page, 2)).Items.Select(i => i.Id));

			Assert.Equal(5, seen.Distinct().Count());
			Assert.Equal(seen.OrderByDescending(id => id), seen); // 同一天依 Id 由新到舊
		}

		[Fact]
		public async Task 縣市計數合併台臺兩種寫法而且同一則只算一次()
		{
			using var db = CreateDb(nameof(縣市計數合併台臺兩種寫法而且同一則只算一次));
			db.PestAlerts.AddRange(
				Alert("一", new DateOnly(2026, 9, 1), "台南市", "臺南市"), // 舊資料可能兩種寫法都有
				Alert("二", new DateOnly(2026, 9, 2), "臺南市"),
				Alert("三", new DateOnly(2026, 9, 3), "高雄市"));
			await db.SaveChangesAsync();

			var counts = await new PestService(db).GetPestAlertCountsByCityAsync();

			Assert.Equal(2, counts.Single(c => c.CityName == "臺南市").Count);
			Assert.Equal(1, counts.Single(c => c.CityName == "高雄市").Count);
			Assert.DoesNotContain(counts, c => c.CityName == "台南市");
		}

		[Fact]
		public async Task 害蟲名稱依名稱排序()
		{
			using var db = CreateDb(nameof(害蟲名稱依名稱排序));
			foreach (var name in new[] { "斜紋夜蛾", "二化螟", "斜紋夜蛾", "褐飛蝨" })
				db.PestDecadeSummaries.Add(new PestDecadeSummary { PestName = name, City = "臺北市", Town = "t", Year = 2026, Month = 9, TenDays = 1 });
			await db.SaveChangesAsync();

			var names = await new PestService(db).GetAllPestNamesAsync();

			Assert.Equal(names.Order(), names);
			Assert.Equal(3, names.Count);
		}

		[Theory]
		[InlineData(0, 20)]
		[InlineData(-1, 20)]
		[InlineData(1, 0)]
		[InlineData(1, -5)]
		[InlineData(1, 101)]
		public void 警報分頁參數超出界限時驗證失敗(int page, int pageSize)
		{
			// [ApiController] 用同一套 DataAnnotations 在進入動作前回 400；這裡直接驗 DTO 上的界限
			var dto = new PestAlertQueryDto { Page = page, PageSize = pageSize };
			Assert.False(Validator.TryValidateObject(dto, new ValidationContext(dto), null, validateAllProperties: true));
		}

		[Fact]
		public void 通知列表頁碼為零時驗證失敗()
		{
			var dto = new NotificationListQueryDto { Page = 0 };
			Assert.False(Validator.TryValidateObject(dto, new ValidationContext(dto), null, validateAllProperties: true));
		}

		[Fact]
		public void 同步時同一批裡標題與發布日相同的警報只留一筆()
		{
			var incoming = new[]
			{
				new PestAlert { Subject = "a", SourceHash = "h1" },
				new PestAlert { Subject = "a 重複", SourceHash = "h1" },
				new PestAlert { Subject = "b", SourceHash = "h2" },
				new PestAlert { Subject = "已存在", SourceHash = "h0" },
			};

			var result = PestAlertSyncWorker.SelectNewAlerts(incoming, new HashSet<string> { "h0" });

			Assert.Equal(new[] { "a", "b" }, result.Select(p => p.Subject));
		}

		[Fact]
		public void 同步時縣市換成正式寫法並去掉重複與空白項()
		{
			var cities = PestAlertSyncWorker.ParseCities("台南市, 高雄市,台南市,");

			Assert.Equal(new[] { "臺南市", "高雄市" }, cities.Select(c => c.CityName));
		}
	}
}
