using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Moq;
using TaiwanAgri.Modules.Market.Data;
using TaiwanAgri.Modules.Market.Entities;
using TaiwanAgri.Modules.Market.Services;

namespace TaiwanAgri.Tests.Market
{
	/// <summary>
	/// GetLatestPricesAsync 的批次查詢行為測試。
	/// 重點在「監看項目未指定市場」這個情形——它會讓 MarketCode 是 null，
	/// 而 SQL 的 IN 永遠不匹配 NULL，不特別處理就會安靜地查不到任何價格
	/// </summary>
	public class LatestPriceQueryTests
	{
		private static MarketDbContext CreateDb(string dbName)
		{
			var options = new DbContextOptionsBuilder<MarketDbContext>()
				.UseInMemoryDatabase(dbName)
				.Options;
			return new MarketDbContext(options);
		}

		private static MarketService CreateService(MarketDbContext db)
			=> new(db, new Mock<IDistributedCache>().Object, new Mock<IConfiguration>().Object, TimeProvider.System);

		private static AgriProductsTrans Row(string cropCode, string marketCode, DateOnly date, decimal avgPrice)
			=> new()
			{
				CropCode = cropCode,
				MarketCode = marketCode,
				TransDate = date,
				AvgPrice = avgPrice,
				UpperPrice = avgPrice,
				MiddlePrice = avgPrice,
				LowerPrice = avgPrice,
				TransQuantity = 100m,
				TcType = "V"
			};

		[Fact]
		public async Task 指定市場時回傳該市場最新一筆()
		{
			var db = CreateDb(nameof(指定市場時回傳該市場最新一筆));
			db.AgriProductsTrans.AddRange(
				Row("C1", "M1", new DateOnly(2026, 8, 1), 10m),
				Row("C1", "M1", new DateOnly(2026, 8, 3), 30m),   // 最新
				Row("C1", "M2", new DateOnly(2026, 8, 5), 99m));  // 別的市場，不該被選到
			await db.SaveChangesAsync();

			var result = await CreateService(db).GetLatestPricesAsync(
				new[] { ("C1", (string?)"M1") });

			var item = Assert.Single(result);
			Assert.Equal("M1", item.MarketCode);
			Assert.Equal(new DateOnly(2026, 8, 3), item.TransDate);
			Assert.Equal(30m, item.AvgPrice);
		}

		[Fact]
		public async Task 未指定市場時回傳最新交易日的跨市場均價()
		{
			// 監看清單允許只選作物不選市場。舊實作把 null 丟進 marketCodes.Contains(...)，
			// SQL 的 IN 不匹配 NULL，這種項目永遠拿不到價格且沒有任何錯誤訊號
			var db = CreateDb(nameof(未指定市場時回傳最新交易日的跨市場均價));
			db.AgriProductsTrans.AddRange(
				Row("C1", "M1", new DateOnly(2026, 8, 3), 20m),   // 最新日，兩市場
				Row("C1", "M2", new DateOnly(2026, 8, 3), 40m),
				Row("C1", "M1", new DateOnly(2026, 8, 1), 99m));  // 舊資料，不該進平均
			await db.SaveChangesAsync();

			var result = await CreateService(db).GetLatestPricesAsync(
				new[] { ("C1", (string?)null) });

			var item = Assert.Single(result);
			Assert.Null(item.MarketCode);
			Assert.Equal(new DateOnly(2026, 8, 3), item.TransDate);
			Assert.Equal(30m, item.AvgPrice);   // (20 + 40) / 2
		}

		[Fact]
		public async Task 指定與未指定市場的鍵可以混在同一次查詢()
		{
			var db = CreateDb(nameof(指定與未指定市場的鍵可以混在同一次查詢));
			db.AgriProductsTrans.AddRange(
				Row("C1", "M1", new DateOnly(2026, 8, 3), 20m),
				Row("C1", "M2", new DateOnly(2026, 8, 3), 40m),
				Row("C2", "M1", new DateOnly(2026, 8, 2), 50m));
			await db.SaveChangesAsync();

			var result = await CreateService(db).GetLatestPricesAsync(
				new[] { ("C1", (string?)null), ("C2", (string?)"M1") });

			Assert.Equal(2, result.Count);
			Assert.Equal(30m, Assert.Single(result, r => r.MarketCode == null).AvgPrice);
			Assert.Equal(50m, Assert.Single(result, r => r.MarketCode == "M1").AvgPrice);
		}

		[Fact]
		public async Task 查無資料的鍵不回傳也不丟例外()
		{
			var db = CreateDb(nameof(查無資料的鍵不回傳也不丟例外));
			await db.SaveChangesAsync();

			var result = await CreateService(db).GetLatestPricesAsync(
				new[] { ("NOPE", (string?)null), ("NOPE2", (string?)"M9") });

			Assert.Empty(result);
		}
	}
}
