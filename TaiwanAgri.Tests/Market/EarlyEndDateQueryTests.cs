using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TaiwanAgri.Modules.Market.Constants;
using TaiwanAgri.Modules.Market.Data;
using TaiwanAgri.Modules.Market.Services;

namespace TaiwanAgri.Tests.Market
{
	/// <summary>
	/// 行情價格、毛豬、家禽三支查詢：只帶迄日、而且迄日很早（0001-01-01）時，沒帶的起日停在日期下限、回空清單。
	/// 日期驗證只檢查區間長度、不擋年份，沒帶起日時也不檢查，所以這組參數進得來；
	/// 原本服務層直接把迄日往前推 365 天，推過日期下限丟 ArgumentOutOfRangeException、整支查詢回 500
	/// （2026-11 總檢查修正時另外查到）。推算本身的界限在 DateHelperTests，這裡釘的是三個呼叫處都改用它
	/// </summary>
	public class EarlyEndDateQueryTests
	{
		private static MarketDbContext CreateDb(string name) =>
			new(new DbContextOptionsBuilder<MarketDbContext>().UseInMemoryDatabase(name).Options);

		private static MarketService CreateService(MarketDbContext db)
		{
			// 快取一律回「沒有」，每次都查資料庫
			var cache = new Mock<IDistributedCache>();
			cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((byte[]?)null);
			return new MarketService(db, cache.Object,
				Microsoft.Extensions.Options.Options.Create(new MarketQueryOptions()),
				TimeProvider.System, NullLogger<MarketService>.Instance);
		}

		[Fact]
		public async Task 行情價格只帶很早的迄日時不丟例外()
		{
			using var db = CreateDb(nameof(行情價格只帶很早的迄日時不丟例外));

			var result = await CreateService(db).GetPricesAsync("Veg", ["A1"], endDate: DateOnly.MinValue);

			Assert.Empty(result);
		}

		[Fact]
		public async Task 毛豬只帶很早的迄日時不丟例外()
		{
			using var db = CreateDb(nameof(毛豬只帶很早的迄日時不丟例外));

			var result = await CreateService(db).GetPorkAsync(endDate: DateOnly.MinValue);

			Assert.Empty(result);
		}

		[Fact]
		public async Task 家禽只帶很早的迄日時不丟例外()
		{
			using var db = CreateDb(nameof(家禽只帶很早的迄日時不丟例外));

			var result = await CreateService(db).GetPoultryAsync(endDate: DateOnly.MinValue);

			Assert.Empty(result);
		}
	}
}
