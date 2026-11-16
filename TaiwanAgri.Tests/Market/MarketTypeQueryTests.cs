using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TaiwanAgri.Modules.Market.Constants;
using TaiwanAgri.Modules.Market.Data;
using TaiwanAgri.Modules.Market.Entities;
using TaiwanAgri.Modules.Market.Services;

namespace TaiwanAgri.Tests.Market
{
	/// <summary>
	/// 行情、市場清單、休市日、天災的「類別」怎麼判斷（2026-11 總檢查 3-A2、3-A3、天災分組）。
	/// <para>
	/// 這幾條 bug 測試都沒抓到，原因是測試資料照著當時的查詢條件造：市場全是蔬菜、一個市場只有一種類別。
	/// 真實資料的形狀是「同一個市場代碼底下有好幾種類別」——市場主檔只記得其中一種、
	/// 休市表每種類別各一份、同一場豪雨同時有兩種警戒。這裡的種子資料刻意造成那個形狀
	/// </para>
	/// </summary>
	public class MarketTypeQueryTests
	{
		private static readonly DateOnly Today = new(2026, 10, 13);

		private sealed class FixedTimeProvider : TimeProvider
		{
			public override DateTimeOffset GetUtcNow() => new(2026, 10, 13, 4, 0, 0, TimeSpan.Zero); // 台灣 12:00
		}

		private static MarketDbContext CreateDb(string name) =>
			new(new DbContextOptionsBuilder<MarketDbContext>().UseInMemoryDatabase(name).Options);

		private static MarketService CreateService(MarketDbContext db)
		{
			// 快取一律回「沒有」，每次都查資料庫
			var cache = new Mock<IDistributedCache>();
			cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((byte[]?)null);
			return new MarketService(db, cache.Object,
				Microsoft.Extensions.Options.Options.Create(new MarketQueryOptions()),
				new FixedTimeProvider(), NullLogger<MarketService>.Instance);
		}

		private static AgriProductsTrans Trade(string tcType, string marketCode, string cropCode, DateOnly date, decimal avg, decimal qty) => new()
		{
			TcType = tcType, MarketCode = marketCode, CropCode = cropCode, TransDate = date,
			AvgPrice = avg, UpperPrice = avg, MiddlePrice = avg, LowerPrice = avg, TransQuantity = qty
		};

		/// <summary>
		/// 109 台北一同時賣蔬菜與水果，但市場主檔只記成蔬菜（主檔以代碼＋名稱為鍵，先同步的蔬菜佔走了）；
		/// 423 東勢鎮只賣水果，主檔記成水果
		/// </summary>
		private static async Task SeedMixedMarketAsync(MarketDbContext db)
		{
			db.CropInfos.Add(new CropInfo { CropCode = "A1", CropName = "香蕉" });
			db.MarketInfos.AddRange(
				new MarketInfo { MarketCode = "109", MarketName = "台北一", MarketType = "Veg" },
				new MarketInfo { MarketCode = "423", MarketName = "東勢鎮", MarketType = "Fruit" });
			db.AgriProductsTrans.AddRange(
				Trade("N05", "109", "A1", Today, 30m, 1000m),
				Trade("N05", "423", "A1", Today, 20m, 100m),
				Trade("N04", "109", "LA1", Today, 15m, 500m));
			await db.SaveChangesAsync();
		}

		[Fact]
		public async Task 水果行情算得到主檔記成蔬菜的市場()
		{
			using var db = CreateDb(nameof(水果行情算得到主檔記成蔬菜的市場));
			await SeedMixedMarketAsync(db);

			var result = await CreateService(db).GetPricesAsync("Fruit", ["A1"], startDate: Today, endDate: Today);

			// 全台均價：兩個市場都要算進去（原本只算到 423，數量 100）
			var row = Assert.Single(result);
			Assert.Equal(1100m, row.TransQuantity);
			Assert.Equal(25m, row.AvgPrice);
		}

		[Fact]
		public async Task 水果的市場清單列得出主檔記成蔬菜的市場()
		{
			using var db = CreateDb(nameof(水果的市場清單列得出主檔記成蔬菜的市場));
			await SeedMixedMarketAsync(db);

			var markets = await CreateService(db).GetMarketsAsync("Fruit");

			Assert.Equal(new[] { "109", "423" }, markets.Select(m => m.MarketCode));
		}

		[Fact]
		public async Task 市場清單不列一年以上沒有交易的舊市場()
		{
			using var db = CreateDb(nameof(市場清單不列一年以上沒有交易的舊市場));
			db.MarketInfos.AddRange(
				new MarketInfo { MarketCode = "540", MarketName = "舊市場", MarketType = "Veg" },
				new MarketInfo { MarketCode = "109", MarketName = "台北一", MarketType = "Veg" });
			db.AgriProductsTrans.AddRange(
				Trade("N04", "540", "LA1", Today.AddDays(-400), 10m, 1m),
				Trade("N04", "109", "LA1", Today.AddDays(-10), 10m, 1m));
			await db.SaveChangesAsync();

			var markets = await CreateService(db).GetMarketsAsync("Veg");

			Assert.Equal(new[] { "109" }, markets.Select(m => m.MarketCode));
		}

		[Fact]
		public async Task 共用代碼的市場優先取同類別的名稱()
		{
			// 400 在主檔有兩筆：蔬菜的「台中市」與花卉的「台中市場」（兩個市場共用代碼）
			using var db = CreateDb(nameof(共用代碼的市場優先取同類別的名稱));
			db.MarketInfos.AddRange(
				new MarketInfo { MarketCode = "400", MarketName = "台中市", MarketType = "Veg" },
				new MarketInfo { MarketCode = "400", MarketName = "台中市場", MarketType = "Flower" });
			db.AgriProductsTrans.AddRange(
				Trade("N06", "400", "F1", Today, 10m, 1m),
				Trade("N05", "400", "A1", Today, 10m, 1m));
			await db.SaveChangesAsync();
			var service = CreateService(db);

			Assert.Equal("台中市場", Assert.Single(await service.GetMarketsAsync("Flower")).MarketName);
			// 水果沒有同類別的名稱，取蔬菜那一筆
			Assert.Equal("台中市", Assert.Single(await service.GetMarketsAsync("Fruit")).MarketName);
		}

		[Fact]
		public async Task 休市日只取蔬菜那一份且不重複()
		{
			// 同一個市場代碼底下，蔬菜與水果各一份、同一天；花卉另一天。原本三筆全回，統計卡算成 3 天
			using var db = CreateDb(nameof(休市日只取蔬菜那一份且不重複));
			db.MarketRestDays.AddRange(
				new MarketRestDay { MarketCode = "109", MarketName = "台北一", MarketType = "V", Year = 115, Month = 10, RestDay = 5 },
				new MarketRestDay { MarketCode = "109", MarketName = "台北一", MarketType = "F", Year = 115, Month = 10, RestDay = 5 },
				new MarketRestDay { MarketCode = "109", MarketName = "台北一", MarketType = "L", Year = 115, Month = 10, RestDay = 9 },
				new MarketRestDay { MarketCode = "109", MarketName = "台北一", MarketType = "V", Year = 115, Month = 10, RestDay = 1 });
			await db.SaveChangesAsync();

			var days = await CreateService(db).GetRestDaysAsync("109", new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));

			Assert.Equal(new[] { new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 5) }, days.Select(d => d.RestDate));
		}

		[Fact]
		public async Task 天災同一事件同一天的兩種警戒分開回傳()
		{
			using var db = CreateDb(nameof(天災同一事件同一天的兩種警戒分開回傳));
			var at = new DateTime(2026, 8, 24, 10, 0, 0);
			db.DebrisAlertRecords.AddRange(
				new DebrisAlertRecord { DisasterName = "0822豪雨", AlertType = "D", County = "高雄市", LastUpdateDate = at, ReportID = "r1", DebrisNo = "d1" },
				new DebrisAlertRecord { DisasterName = "0822豪雨", AlertType = "L", County = "臺南市", LastUpdateDate = at, ReportID = "r1", LandslideID = "l1" });
			await db.SaveChangesAsync();

			var (items, _) = await CreateService(db).GetDisastersAsync([], new DateOnly(2026, 8, 24), new DateOnly(2026, 8, 24));

			Assert.Equal(2, items.Count);
			Assert.Equal(new[] { "高雄市" }, items.Single(i => i.AlertType == "D").AffectedCounties);
			Assert.Equal(new[] { "臺南市" }, items.Single(i => i.AlertType == "L").AffectedCounties);
		}
	}
}
