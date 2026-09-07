using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Moq;
using TaiwanAgri.Modules.Market.Constants;
using TaiwanAgri.Modules.Market.Data;
using TaiwanAgri.Modules.Market.Entities;
using TaiwanAgri.Modules.Market.Entities.Enums;
using TaiwanAgri.Modules.Market.Services;

namespace TaiwanAgri.Tests.Market
{
	/// <summary>
	/// GetPoultryAsync 的查詢行為測試。
	/// 重點在驗證「不過濾非 Normal 資料點」這個核心設計決策不會被日後的修改破壞
	/// ——那是整個 PriceStatus 設計的價值所在。
	/// </summary>
	public class PoultryQueryTests
	{
		/// <summary>每個測試用獨立的 InMemory DB 名稱，避免互相污染</summary>
		private static MarketDbContext CreateDb(string dbName)
		{
			var options = new DbContextOptionsBuilder<MarketDbContext>()
				.UseInMemoryDatabase(dbName)
				.Options;
			return new MarketDbContext(options);
		}

		private static MarketService CreateService(MarketDbContext db)
			=> new(db, new Mock<IDistributedCache>().Object, Microsoft.Extensions.Options.Options.Create(new TaiwanAgri.Modules.Market.Constants.MarketQueryOptions()), TimeProvider.System);

		private static PoultryTrans Row(DateOnly date, string metricCode, decimal? price,
			PriceStatus status = PriceStatus.Normal, string? rawValue = null)
			=> new()
			{
				TransDate = date,
				MetricCode = metricCode,
				Price = price,
				PriceStatus = status,
				RawValue = rawValue,
				SyncedAt = DateTime.UtcNow
			};

		[Fact]
		public async Task GetPoultryAsync_非Normal的資料點也要回傳()
		{
			// 這是查詢層最重要的一條規則：休市/未報價/議價的日子不能被濾掉，
			// 否則前端只會看到莫名其妙的缺口，無法分辨「本來就少報價」與「同步壞了」
			var db = CreateDb(nameof(GetPoultryAsync_非Normal的資料點也要回傳));
			var day = new DateOnly(2026, 8, 20);
			db.PoultryTrans.AddRange(
				Row(day, PoultryMetrics.Goose_WhiteRoman, 67.0m),
				Row(day, PoultryMetrics.Duck_Male, null, PriceStatus.Closed, "休市"),
				Row(day, PoultryMetrics.RedFeather_South_Male, null, PriceStatus.NotQuoted, "-"),
				Row(day, PoultryMetrics.Egg_Transport, null, PriceStatus.Negotiated, "議價"),
				Row(day, PoultryMetrics.Egg_Producer, null, PriceStatus.Empty, ""),
				Row(day, PoultryMetrics.RedFeather_Central_Male, null, PriceStatus.RangeQuote, "41-42"));
			await db.SaveChangesAsync();

			var result = await CreateService(db).GetPoultryAsync(
				null, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31));

			Assert.Equal(6, result.Count);
			Assert.Equal(5, result.Count(r => r.Price == null));
		}

		[Fact]
		public async Task GetPoultryAsync_PriceStatus回傳字串而非數字()
		{
			// 比照模組 3 既有慣例：所有 enum 欄位 API 回傳都是字串
			var db = CreateDb(nameof(GetPoultryAsync_PriceStatus回傳字串而非數字));
			db.PoultryTrans.Add(Row(new DateOnly(2026, 8, 20),
				PoultryMetrics.Duck_Male, null, PriceStatus.Closed, "休市"));
			await db.SaveChangesAsync();

			var result = await CreateService(db).GetPoultryAsync(
				null, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31));

			Assert.Equal("Closed", result[0].PriceStatus);
			Assert.Equal("休市", result[0].RawValue);
		}

		[Fact]
		public async Task GetPoultryAsync_帶入metricCodes只回傳指定指標()
		{
			var db = CreateDb(nameof(GetPoultryAsync_帶入metricCodes只回傳指定指標));
			var day = new DateOnly(2026, 8, 20);
			db.PoultryTrans.AddRange(
				Row(day, PoultryMetrics.Goose_WhiteRoman, 67.0m),
				Row(day, PoultryMetrics.Duck_75Days, 52.8m),
				Row(day, PoultryMetrics.Egg_Transport, 42.5m));
			await db.SaveChangesAsync();

			var result = await CreateService(db).GetPoultryAsync(
				new[] { PoultryMetrics.Goose_WhiteRoman, PoultryMetrics.Egg_Transport },
				new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31));

			Assert.Equal(2, result.Count);
			Assert.DoesNotContain(result, r => r.MetricCode == PoultryMetrics.Duck_75Days);
		}

		[Fact]
		public async Task GetPoultryAsync_metricCodes為空陣列時視為不篩選()
		{
			var db = CreateDb(nameof(GetPoultryAsync_metricCodes為空陣列時視為不篩選));
			var day = new DateOnly(2026, 8, 20);
			db.PoultryTrans.AddRange(
				Row(day, PoultryMetrics.Goose_WhiteRoman, 67.0m),
				Row(day, PoultryMetrics.Duck_75Days, 52.8m));
			await db.SaveChangesAsync();

			var result = await CreateService(db).GetPoultryAsync(
				Array.Empty<string>(), new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31));

			Assert.Equal(2, result.Count);
		}

		[Fact]
		public async Task GetPoultryAsync_日期區間為含頭含尾()
		{
			var db = CreateDb(nameof(GetPoultryAsync_日期區間為含頭含尾));
			db.PoultryTrans.AddRange(
				Row(new DateOnly(2026, 8, 9), PoultryMetrics.Goose_WhiteRoman, 60m),   // 區間外
				Row(new DateOnly(2026, 8, 10), PoultryMetrics.Goose_WhiteRoman, 61m),  // 邊界（含）
				Row(new DateOnly(2026, 8, 15), PoultryMetrics.Goose_WhiteRoman, 62m),
				Row(new DateOnly(2026, 8, 20), PoultryMetrics.Goose_WhiteRoman, 63m),  // 邊界（含）
				Row(new DateOnly(2026, 8, 21), PoultryMetrics.Goose_WhiteRoman, 64m)); // 區間外
			await db.SaveChangesAsync();

			var result = await CreateService(db).GetPoultryAsync(
				null, new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 20));

			Assert.Equal(3, result.Count);
			Assert.All(result, r => Assert.InRange(r.TransDate,
				new DateOnly(2026, 8, 10), new DateOnly(2026, 8, 20)));
		}

		[Fact]
		public async Task GetPoultryAsync_排序為指標優先再依日期遞增()
		{
			// 前端多線折線圖依賴這個順序直接切線，不必自己重排
			var db = CreateDb(nameof(GetPoultryAsync_排序為指標優先再依日期遞增));
			db.PoultryTrans.AddRange(
				Row(new DateOnly(2026, 8, 20), PoultryMetrics.Goose_WhiteRoman, 63m),
				Row(new DateOnly(2026, 8, 10), PoultryMetrics.Goose_WhiteRoman, 61m),
				Row(new DateOnly(2026, 8, 20), PoultryMetrics.Duck_75Days, 52m),
				Row(new DateOnly(2026, 8, 10), PoultryMetrics.Duck_75Days, 51m));
			await db.SaveChangesAsync();

			var result = await CreateService(db).GetPoultryAsync(
				null, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31));

			// Duck_75Days 排在 Goose_WhiteRoman 之前（字串序），各自內部日期遞增
			Assert.Equal(PoultryMetrics.Duck_75Days, result[0].MetricCode);
			Assert.Equal(new DateOnly(2026, 8, 10), result[0].TransDate);
			Assert.Equal(new DateOnly(2026, 8, 20), result[1].TransDate);
			Assert.Equal(PoultryMetrics.Goose_WhiteRoman, result[2].MetricCode);
			Assert.Equal(new DateOnly(2026, 8, 10), result[2].TransDate);
		}

		[Fact]
		public async Task GetPoultryAsync_DisplayName由後端帶出()
		{
			// 前端不必自備對照表，避免與 PoultryMetrics.cs 分岔
			var db = CreateDb(nameof(GetPoultryAsync_DisplayName由後端帶出));
			db.PoultryTrans.Add(Row(new DateOnly(2026, 8, 20), PoultryMetrics.Goose_WhiteRoman, 67m));
			await db.SaveChangesAsync();

			var result = await CreateService(db).GetPoultryAsync(
				null, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31));

			Assert.Equal("肉鵝白羅曼", result[0].DisplayName);
		}
	}
}
