using Microsoft.EntityFrameworkCore;
using TaiwanAgri.Modules.Weather.Data;
using TaiwanAgri.Modules.Weather.Entities;
using TaiwanAgri.Modules.Weather.Services;

namespace TaiwanAgri.Tests.Weather
{
	/// <summary>
	/// WeatherService 兩支方法。唯一依賴是 WeatherDbContext，所以直接用 InMemory 資料庫。
	/// 重點在兩處看不出錯的地方：雨量查詢是 Join 兩張表（測站沒建檔的觀測會整筆消失），
	/// 以及測站查詢的兩段式策略——第二段用兩個彼此獨立的 IN 條件，
	/// 會撈到「甲站的代號配上乙站的時間」這種本來不存在的組合，靠第三段的記憶體 GroupBy 收掉
	/// </summary>
	public class WeatherServiceTests
	{
		/// <summary>
		/// InMemory 資料庫依名稱共用，所以每個測試各給一個唯一名稱避免互相污染
		/// </summary>
		private static WeatherDbContext CreateDbContext(string databaseName) =>
			new(new DbContextOptionsBuilder<WeatherDbContext>()
				.UseInMemoryDatabase(databaseName)
				.Options);

		private static RainfallStation Station(string stationId, string cityName, string? stationName = null) => new()
		{
			StationId = stationId,
			StationName = stationName ?? $"{stationId} 站",
			CityName = cityName,
			CityCode = "00"
		};

		private static RainfallObservation Rainfall(string stationId, DateTime observedAt, decimal hour24 = 1m) => new()
		{
			StationId = stationId,
			ObservedAt = observedAt,
			Hour24 = hour24
		};

		private static WeatherObservation Observation(
			string stationId, string cityName, DateTime observedAt, decimal temperature) => new()
		{
			StationId = stationId,
			StationName = $"{stationId} 站",
			CityName = cityName,
			CityCode = "00",
			ObservedAt = observedAt,
			Temperature = temperature
		};

		// ── GetRainfallByCityAsync ───────────────────────────────────────────

		/// <summary>
		/// 雨量觀測本身沒有縣市欄位，縣市是從 RainfallStations join 過來的。
		/// 所以「查台北」實際上是「查所有隸屬台北的站台的觀測」，這條確認 join 條件沒有接錯邊
		/// </summary>
		[Fact]
		public async Task 只回傳隸屬指定縣市的測站觀測()
		{
			using var db = CreateDbContext(nameof(只回傳隸屬指定縣市的測站觀測));
			db.RainfallStations.AddRange(Station("A01", "臺北市"), Station("B01", "臺中市"));
			db.RainfallObservations.AddRange(
				Rainfall("A01", new DateTime(2026, 9, 1), 10m),
				Rainfall("B01", new DateTime(2026, 9, 1), 20m));
			await db.SaveChangesAsync();

			var result = await new WeatherService(db).GetRainfallByCityAsync(
				"臺北市", new DateOnly(2026, 8, 1), new DateOnly(2026, 9, 30));

			Assert.Single(result);
			Assert.Equal("臺北市", result[0].CityName);
			Assert.Equal(10m, result[0].Hour24);
		}

		/// <summary>
		/// 區間兩端都是閉區間（&gt;= start 且 &lt;= end）。開閉區間寫錯的症狀是「查一整天只少最後一筆」，
		/// 資料看起來還是有，所以不會有人回報
		/// </summary>
		[Fact]
		public async Task 日期區間的兩個端點都要包含在結果裡()
		{
			using var db = CreateDbContext(nameof(日期區間的兩個端點都要包含在結果裡));
			db.RainfallStations.Add(Station("A01", "臺北市"));
			db.RainfallObservations.AddRange(
				Rainfall("A01", new DateTime(2026, 8, 31), 1m),   // 區間前一天
				Rainfall("A01", new DateTime(2026, 9, 1), 2m),    // 起點當天
				Rainfall("A01", new DateTime(2026, 9, 3), 3m),    // 終點當天
				Rainfall("A01", new DateTime(2026, 9, 4), 4m));   // 區間後一天
			await db.SaveChangesAsync();

			var result = await new WeatherService(db).GetRainfallByCityAsync(
				"臺北市", new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 3));

			Assert.Equal(2, result.Count);
			Assert.Contains(result, r => r.Hour24 == 2m);
			Assert.Contains(result, r => r.Hour24 == 3m);
		}

		/// <summary>
		/// 不傳日期時的預設區間是「往前十四天到今天」。這裡刻意用相對於當下的日期種資料，
		/// 而不是寫死日期——因為這支方法的預設值取自呼叫當下的系統時間，寫死的日期會讓測試在某天突然變紅
		/// </summary>
		[Fact]
		public async Task 不指定日期時預設查最近十四天()
		{
			using var db = CreateDbContext(nameof(不指定日期時預設查最近十四天));
			db.RainfallStations.Add(Station("A01", "臺北市"));
			db.RainfallObservations.AddRange(
				Rainfall("A01", DateTime.Now.AddDays(-20), 99m),  // 十四天之外
				Rainfall("A01", DateTime.Now.AddDays(-5), 5m));   // 十四天之內
			await db.SaveChangesAsync();

			var result = await new WeatherService(db).GetRainfallByCityAsync("臺北市");

			Assert.Single(result);
			Assert.Equal(5m, result[0].Hour24);
		}

		/// <summary>
		/// Join 是內連接，所以觀測資料若指向一個測站主檔裡沒有的代號，這筆會整個消失——
		/// 不丟例外、也不會變成沒有縣市的一列，就是不見。這與 NotificationService 的
		/// 必要導覽屬性是同一件事的兩種寫法，兩者都只在資料不完整時才現形
		/// </summary>
		[Fact]
		public async Task 觀測指向不存在的測站時該筆不會出現在結果裡()
		{
			using var db = CreateDbContext(nameof(觀測指向不存在的測站時該筆不會出現在結果裡));
			db.RainfallStations.Add(Station("A01", "臺北市"));
			db.RainfallObservations.AddRange(
				Rainfall("A01", new DateTime(2026, 9, 1), 10m),
				Rainfall("Z99", new DateTime(2026, 9, 1), 20m));  // 主檔裡沒有 Z99
			await db.SaveChangesAsync();

			var result = await new WeatherService(db).GetRainfallByCityAsync(
				"臺北市", new DateOnly(2026, 8, 1), new DateOnly(2026, 9, 30));

			Assert.Single(result);
			Assert.Equal(10m, result[0].Hour24);
		}

		// ── GetStationsByCityAsync ───────────────────────────────────────────

		/// <summary>
		/// 每個站只回最新一筆，這是這支方法的基本契約
		/// </summary>
		[Fact]
		public async Task 每個測站只回傳最新一筆觀測()
		{
			using var db = CreateDbContext(nameof(每個測站只回傳最新一筆觀測));
			db.WeatherObservations.AddRange(
				Observation("A01", "臺北市", new DateTime(2026, 9, 1, 8, 0, 0), 20m),
				Observation("A01", "臺北市", new DateTime(2026, 9, 1, 14, 0, 0), 30m));
			await db.SaveChangesAsync();

			var result = await new WeatherService(db).GetStationsByCityAsync("臺北市");

			Assert.Single(result);
			Assert.Equal(30m, result[0].Temperature);
		}

		/// <summary>
		/// 這條是這支方法真正的風險點。第二段查詢用的是兩個彼此獨立的 IN：
		/// 「代號在這批裡」而且「時間在這批裡」——它沒有要求兩者來自同一列。
		/// 所以甲站的一筆舊觀測，只要時間剛好等於乙站的最新時間，就會被第二段撈出來。
		/// 第三段的記憶體 GroupBy 是唯一擋下它的地方；拿掉第三段這條會紅，其餘測試不會
		/// </summary>
		[Fact]
		public async Task 甲站的舊觀測時間與乙站最新時間相同時不得混進結果()
		{
			using var db = CreateDbContext(nameof(甲站的舊觀測時間與乙站最新時間相同時不得混進結果));
			var 早上 = new DateTime(2026, 9, 1, 8, 0, 0);
			var 下午 = new DateTime(2026, 9, 1, 14, 0, 0);
			db.WeatherObservations.AddRange(
				Observation("A01", "臺北市", 下午, 30m),   // A 站最新
				Observation("A01", "臺北市", 早上, 20m),   // A 站的舊資料，時間與 B 站最新相同
				Observation("B01", "臺北市", 早上, 25m));  // B 站最新
			await db.SaveChangesAsync();

			var result = await new WeatherService(db).GetStationsByCityAsync("臺北市");

			Assert.Equal(2, result.Count);
			Assert.Equal(30m, Assert.Single(result, r => r.StationName == "A01 站").Temperature);
			Assert.Equal(25m, Assert.Single(result, r => r.StationName == "B01 站").Temperature);
		}

		/// <summary>
		/// 縣市條件在第一段與第二段都有下。少下任何一次的症狀是「查台北跑出台中的站」，
		/// 而不是查不到東西
		/// </summary>
		[Fact]
		public async Task 不回傳其他縣市的測站()
		{
			using var db = CreateDbContext(nameof(不回傳其他縣市的測站));
			db.WeatherObservations.AddRange(
				Observation("A01", "臺北市", new DateTime(2026, 9, 1), 20m),
				Observation("B01", "臺中市", new DateTime(2026, 9, 1), 28m));
			await db.SaveChangesAsync();

			var result = await new WeatherService(db).GetStationsByCityAsync("臺北市");

			Assert.Single(result);
			Assert.Equal("臺北市", result[0].CityName);
		}

		/// <summary>
		/// 查無資料回空清單而不是 null，呼叫端才能直接 foreach
		/// </summary>
		[Fact]
		public async Task 查無資料時回傳空清單()
		{
			using var db = CreateDbContext(nameof(查無資料時回傳空清單));

			var service = new WeatherService(db);

			Assert.Empty(await service.GetStationsByCityAsync("臺北市"));
			Assert.Empty(await service.GetRainfallByCityAsync("臺北市"));
		}
	}
}
