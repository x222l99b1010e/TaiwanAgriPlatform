using Microsoft.EntityFrameworkCore;
using TaiwanAgri.Modules.Weather.Data;
using TaiwanAgri.Modules.Weather.Entities;
using TaiwanAgri.Modules.Weather.Services;

namespace TaiwanAgri.Tests.Weather
{
	/// <summary>
	/// PestService 三支方法。唯一依賴是 WeatherDbContext，所以直接用 InMemory 資料庫。
	/// 重點在三處：一筆警報可以同時屬於多個縣市與多種作物（篩選條件下在集合上而不是欄位上）、
	/// 總筆數要在分頁切割之前算（算在之後的話最後一頁永遠對不上）、
	/// 以及旬別排序有年月旬三層，任一層寫反都只會讓順序微妙地錯而不會報錯
	/// </summary>
	public class PestServiceTests
	{
		private static WeatherDbContext CreateDbContext(string databaseName) =>
			new(new DbContextOptionsBuilder<WeatherDbContext>()
				.UseInMemoryDatabase(databaseName)
				.Options);

		/// <summary>
		/// 建一筆警報，城市與作物都用可變長度的陣列——多對多的篩選只有在「一筆對多個」時才測得出來
		/// </summary>
		private static PestAlert Alert(
			string subject, DateOnly pubDate, string[] cities, string[]? crops = null) => new()
			{
				Subject = subject,
				Body = $"{subject} 內文",
				Prescription = "用藥建議",
				PubDate = pubDate,
				Issue = "第一期",
				Cities = [.. cities.Select(c => new PestAlertCity { CityName = c })],
				Crops = [.. (crops ?? ["水稻"]).Select(c => new PestAlertCrop { CropName = c })]
			};

		private static PestDecadeSummary Decade(
			string pestName, int year, int month, int tenDays, decimal average = 1m) => new()
			{
				PestName = pestName,
				Year = year,
				Month = month,
				TenDays = tenDays,
				City = "臺北市",
				Town = "中正區",
				Average = average
			};

		// ── GetPestAlertsByCityAsync ─────────────────────────────────────────

		/// <summary>
		/// 縣市參數是可選的，不傳代表不篩選。這個預設值若寫成「不傳就查不到東西」，
		/// 前端第一次進頁面會是空白，而空白看起來很像「目前沒有警報」
		/// </summary>
		[Fact]
		public async Task 不指定縣市時回傳全部警報()
		{
			using var db = CreateDbContext(nameof(不指定縣市時回傳全部警報));
			db.PestAlerts.AddRange(
				Alert("北部疫情", new DateOnly(2026, 9, 1), ["臺北市"]),
				Alert("中部疫情", new DateOnly(2026, 9, 2), ["臺中市"]));
			await db.SaveChangesAsync();

			var result = await new PestService(db).GetPestAlertsByCityAsync();

			Assert.Equal(2, result.TotalCount);
			Assert.Equal(2, result.Items.Count);
		}

		/// <summary>
		/// 一筆警報可以同時涵蓋多個縣市，所以篩選是「這筆的城市集合裡有沒有這個城市」，
		/// 不是「這筆的城市欄位等於這個城市」。寫成後者的症狀是跨縣市警報只有第一個縣市查得到
		/// </summary>
		[Fact]
		public async Task 跨縣市的警報在其中任一縣市都查得到()
		{
			using var db = CreateDbContext(nameof(跨縣市的警報在其中任一縣市都查得到));
			db.PestAlerts.Add(Alert("北北基疫情", new DateOnly(2026, 9, 1), ["臺北市", "新北市", "基隆市"]));
			await db.SaveChangesAsync();

			var service = new PestService(db);

			Assert.Single((await service.GetPestAlertsByCityAsync("臺北市")).Items);
			Assert.Single((await service.GetPestAlertsByCityAsync("新北市")).Items);
			Assert.Single((await service.GetPestAlertsByCityAsync("基隆市")).Items);
			Assert.Empty((await service.GetPestAlertsByCityAsync("臺中市")).Items);
		}

		/// <summary>
		/// 總筆數算在 Skip/Take 之前，所以第二頁回報的總數仍然是符合條件的全部筆數。
		/// 算在之後的話，前端拿到的總頁數會隨著使用者翻到第幾頁而改變
		/// </summary>
		[Fact]
		public async Task 第二頁回報的總筆數仍是全部筆數而不是該頁筆數()
		{
			using var db = CreateDbContext(nameof(第二頁回報的總筆數仍是全部筆數而不是該頁筆數));
			for (var i = 0; i < 5; i++)
				db.PestAlerts.Add(Alert($"疫情 {i}", new DateOnly(2026, 9, 1).AddDays(i), ["臺北市"]));
			await db.SaveChangesAsync();

			var result = await new PestService(db).GetPestAlertsByCityAsync("臺北市", page: 2, pageSize: 2);

			Assert.Equal(5, result.TotalCount);
			Assert.Equal(3, result.TotalPages);
			Assert.Equal(2, result.Items.Count);
		}

		/// <summary>
		/// 依發布日期由新到舊。這是使用者唯一感受得到的排序，反了不會報錯、只會讓最舊的疫情排在最前面
		/// </summary>
		[Fact]
		public async Task 警報依發布日期由新到舊排列()
		{
			using var db = CreateDbContext(nameof(警報依發布日期由新到舊排列));
			db.PestAlerts.AddRange(
				Alert("舊", new DateOnly(2026, 9, 1), ["臺北市"]),
				Alert("新", new DateOnly(2026, 9, 10), ["臺北市"]),
				Alert("中", new DateOnly(2026, 9, 5), ["臺北市"]));
			await db.SaveChangesAsync();

			var result = await new PestService(db).GetPestAlertsByCityAsync("臺北市");

			Assert.Equal(["新", "中", "舊"], result.Items.Select(i => i.Subject));
		}

		/// <summary>
		/// 城市與作物是投影出來的字串清單，不是導覽物件。前端直接顯示這兩個陣列，
		/// 漏投影的症狀是標籤區塊整片空白
		/// </summary>
		[Fact]
		public async Task 城市與作物清單要一起投影出來()
		{
			using var db = CreateDbContext(nameof(城市與作物清單要一起投影出來));
			db.PestAlerts.Add(Alert("疫情", new DateOnly(2026, 9, 1), ["臺北市", "新北市"], ["水稻", "玉米"]));
			await db.SaveChangesAsync();

			var item = Assert.Single((await new PestService(db).GetPestAlertsByCityAsync("臺北市")).Items);

			Assert.Equal(["臺北市", "新北市"], item.Cities);
			Assert.Equal(["水稻", "玉米"], item.Crops);
		}

		// ── GetPestDecadeDensityByPestNameAsync ──────────────────────────────

		/// <summary>
		/// 排序有年、月、旬三層，全部由新到舊。只比年會讓同一年的月份亂序、
		/// 只比到月會讓同一個月的三旬亂序——兩種都不會報錯，只會讓折線圖的點連錯順序
		/// </summary>
		[Fact]
		public async Task 旬別資料依年月旬三層由新到舊排列()
		{
			using var db = CreateDbContext(nameof(旬別資料依年月旬三層由新到舊排列));
			db.PestDecadeSummaries.AddRange(
				Decade("褐飛蝨", 2025, 12, 3),
				Decade("褐飛蝨", 2026, 3, 1),
				Decade("褐飛蝨", 2026, 3, 3),
				Decade("褐飛蝨", 2026, 1, 2),
				Decade("褐飛蝨", 2026, 3, 2));
			await db.SaveChangesAsync();

			var result = await new PestService(db).GetPestDecadeDensityByPestNameAsync("褐飛蝨");

			Assert.Equal(
				[(2026, 3, 3), (2026, 3, 2), (2026, 3, 1), (2026, 1, 2), (2025, 12, 3)],
				result.Select(r => (r.Year, r.Month, r.TenDays)));
		}

		/// <summary>
		/// 只回指定病蟲害的資料。名稱是查詢的唯一條件，比對放寬的症狀是圖表混進別種蟲的密度值
		/// </summary>
		[Fact]
		public async Task 只回傳指定病蟲害名稱的旬別資料()
		{
			using var db = CreateDbContext(nameof(只回傳指定病蟲害名稱的旬別資料));
			db.PestDecadeSummaries.AddRange(
				Decade("褐飛蝨", 2026, 3, 1, 10m),
				Decade("斜紋夜蛾", 2026, 3, 1, 20m));
			await db.SaveChangesAsync();

			var result = await new PestService(db).GetPestDecadeDensityByPestNameAsync("褐飛蝨");

			Assert.Equal(10m, Assert.Single(result).Average);
		}

		// ── GetAllPestNamesAsync ─────────────────────────────────────────────

		/// <summary>
		/// 名稱清單是給下拉選單用的，同一種蟲有幾百筆旬別資料，沒去重的話選單會有幾百個重複項目
		/// </summary>
		[Fact]
		public async Task 病蟲害名稱清單要去除重複()
		{
			using var db = CreateDbContext(nameof(病蟲害名稱清單要去除重複));
			db.PestDecadeSummaries.AddRange(
				Decade("褐飛蝨", 2026, 1, 1),
				Decade("褐飛蝨", 2026, 1, 2),
				Decade("褐飛蝨", 2026, 2, 1),
				Decade("斜紋夜蛾", 2026, 1, 1));
			await db.SaveChangesAsync();

			var result = await new PestService(db).GetAllPestNamesAsync();

			Assert.Equal(2, result.Count);
			Assert.Contains("褐飛蝨", result);
			Assert.Contains("斜紋夜蛾", result);
		}

		/// <summary>
		/// 三支方法在查無資料時都要回空集合而不是 null
		/// </summary>
		[Fact]
		public async Task 查無資料時三支方法都回傳空集合()
		{
			using var db = CreateDbContext(nameof(查無資料時三支方法都回傳空集合));
			var service = new PestService(db);

			var paged = await service.GetPestAlertsByCityAsync("臺北市");
			Assert.Empty(paged.Items);
			Assert.Equal(0, paged.TotalCount);
			Assert.Empty(await service.GetPestDecadeDensityByPestNameAsync("褐飛蝨"));
			Assert.Empty(await service.GetAllPestNamesAsync());
		}
	}
}
