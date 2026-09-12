using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text;
using System.Text.Json;
using TaiwanAgri.Modules.Market.Data;
using TaiwanAgri.Modules.Market.Dtos.ApiResponses;
using TaiwanAgri.Modules.Market.Entities;
using TaiwanAgri.Modules.Market.Services;

namespace TaiwanAgri.Tests.Market
{
	public class MarketServiceCacheTests
	{
		[Fact]
		public async Task 快取命中時直接回傳快取內容且不寫入快取()
		{
			// ══════════════════════════════════════════════
			// Arrange：準備舞台
			// 目標：模擬「Redis 已有快取」的情境
			// ══════════════════════════════════════════════

			// 1. 建立假資料
			//    這筆資料代表「之前某次查詢已存進 Redis 的結果」
			//    內容隨便，只要 Assert 時能認出「這就是我放進去的那筆」就好
			var fakeData = new List<PriceResponseDto>
			{
				new PriceResponseDto
				{
					CropCode = "A001",
					CropName = "高麗菜",
					AvgPrice = 12.5m,
					TransDate = new DateOnly(2026, 1, 1)
				}
			};

			// 2. 把假資料「裝進信封」
			//    Redis 只認識 byte[]，不認識 C# 物件
			//    所以必須：C# 物件 → JSON 字串 → byte[]
			//    byte[] 就是「文字的數字版本」，每個字元對應一個數值
			var json = JsonSerializer.Serialize(fakeData);
			var bytes = Encoding.UTF8.GetBytes(json);

			// 3. 建立假 Redis，登記規則
			//    重點：mockCache 是「動詞」不是「名詞」
			//    它不儲存資料，而是登記「當某件事發生時，要怎麼反應」
			//
			//    .Setup()        → 登記規則（此刻不執行）
			//    It.IsAny<T>()  → 不管傳進來的參數是什麼，都觸發這個規則
			//    .ReturnsAsync() → 規定執行時回傳什麼台詞
			//
			//    注意：Setup 的是 GetAsync 而不是 GetStringAsync
			//    因為 GetStringAsync 是 Extension Method（靜態方法），無法被 Mock 攔截
			//    Mock 只能攔截介面上真實定義的方法
			//    GetStringAsync 底層實際呼叫的是 GetAsync，所以 Setup GetAsync 就夠了
			var mockCache = new Mock<IDistributedCache>();
			mockCache
				.Setup(c => c.GetAsync(
					It.IsAny<string>(),
					It.IsAny<CancellationToken>()))
				.ReturnsAsync(bytes);

			// 4. 建立假 DB（InMemory，空的）
			//    Cache Hit 測試的重點是「DB 不應該被碰」
			//    給空的 InMemory DB：如果程式碼跑去查 DB，會回傳空清單，Assert 就會失敗
			//    這樣就能抓到「Cache Hit 邏輯寫錯、跑去查 DB」的 bug
			var options = new DbContextOptionsBuilder<MarketDbContext>()
				.UseInMemoryDatabase("TestDb_CacheHit")
				.Options;
			var dbContext = new MarketDbContext(options);

			// 建立查詢上限選項（GetPricesAsync 用不到，但建構式需要）
			//    所以給一個空的 Mock 就好，不需要 Setup 任何規則

			// 6. 建立被測對象
			//    注意：注入的是 mockCache.Object，不是 mockCache 本身
			//    mockCache        → Mock<IDistributedCache>，是「演員的說明書」
			//    mockCache.Object → 真正的假物件，是「演員本人」，可以被注入
			//
			//    我們注入介面（IDistributedCache）而不是具體 Redis 類別
			//    目的：MarketService 只依賴抽象合約，不在乎背後是真 Redis 還是假 Mock
			//    這就是依賴反轉原則：「我只在乎你合乎規格，不關心你怎麼實作」
			var service = new MarketService(
				dbContext,
				mockCache.Object,
				Microsoft.Extensions.Options.Options.Create(new TaiwanAgri.Modules.Market.Constants.MarketQueryOptions()),
				TimeProvider.System,
				NullLogger<MarketService>.Instance);

			// ══════════════════════════════════════════════
			// Act：開演
			// 呼叫被測方法，讓 MarketService 去問假 Redis
			// ══════════════════════════════════════════════

			var result = await service.GetPricesAsync(
				marketType: "Veg",
				cropCodes: new[] { "A001" }  // ← string[]，不是單一字串
			);

			// ══════════════════════════════════════════════
			// Assert：檢查結果
			// 驗證兩件事：回傳資料正確、DB 完全沒被碰
			// ══════════════════════════════════════════════

			// 8. 驗證回傳筆數正確（只有一筆）
			//    Assert.Single() 是 xUnit 專門驗證「清單只有一筆」的方法
			Assert.Single(result);

			// 9. 驗證回傳的第一筆資料內容正確
			//    這筆資料應該跟 fakeData 一模一樣（從假 Redis 還原回來的）
			Assert.Equal("A001", result[0].CropCode);
			Assert.Equal("高麗菜", result[0].CropName);

			// 10. 驗證 GetAsync 被呼叫了恰好一次
			//     證明程式確實有去問 Redis
			mockCache.Verify(
				c => c.GetAsync(
					It.IsAny<string>(),
					It.IsAny<CancellationToken>()),
				Times.Once());

			// 11. 驗證 SetAsync 完全沒被呼叫
			//     Cache Hit 不應該寫入 Redis（那是 Cache Miss 才做的事）
			//     如果 SetAsync 被呼叫了，代表程式碼跑進了 Cache Miss 路徑，邏輯有誤
			mockCache.Verify(
				c => c.SetAsync(
					It.IsAny<string>(),
					It.IsAny<byte[]>(),
					It.IsAny<DistributedCacheEntryOptions>(),
					It.IsAny<CancellationToken>()),
				Times.Never());
		}

		[Fact]
		public async Task 快取未命中時查資料庫並把結果寫回快取()
		{
			// ══════════════════════════════════════════════
			// Arrange：準備舞台
			// 目標：模擬「Redis 沒有快取」的情境
			// 程式應該去查 DB，然後把結果寫進 Redis
			// ══════════════════════════════════════════════

			// 1. 建立假 Redis，登記規則
			//    回傳 null → 代表「快取不存在」→ 觸發 Cache Miss 路徑
			//    注意：Setup 的是 GetAsync 而不是 GetStringAsync
			//    因為 GetStringAsync 是 Extension Method（靜態方法），無法被 Mock 攔截
			//    Mock 只能攔截介面上真實定義的方法
			var mockCache = new Mock<IDistributedCache>();
			mockCache
				.Setup(c => c.GetAsync(
					It.IsAny<string>(),
					It.IsAny<CancellationToken>()))
				.ReturnsAsync((byte[]?)null);

			// 2. 建立 InMemory DB 並塞入假資料
			//    Cache Miss 時程式會做三表 JOIN，三張表都必須有對應資料
			//    key 必須能對上：
			//      AgriProductsTrans.CropCode   ←→  CropInfo.CropCode
			//      AgriProductsTrans.MarketCode ←→  MarketInfo.MarketCode
			//    where 條件也必須符合：
			//      MarketInfo.MarketType == "Veg"
			//      AgriProductsTrans.CropCode 在 cropCodes 裡
			//      TransDate 在 finalStart ~ finalEnd 範圍內
			var options = new DbContextOptionsBuilder<MarketDbContext>()
				.UseInMemoryDatabase("TestDb_CacheMiss")
				.Options;
			var dbContext = new MarketDbContext(options);

			dbContext.CropInfos.Add(new CropInfo
			{
				CropCode = "A001",
				CropName = "高麗菜"
			});

			dbContext.MarketInfos.Add(new MarketInfo
			{
				MarketCode = "M001",
				MarketName = "台北果菜",
				MarketType = "Veg"   // ← 必須符合 where 條件
			});

			dbContext.AgriProductsTrans.Add(new AgriProductsTrans
			{
				CropCode = "A001",       // ← 對應 CropInfo.CropCode
				MarketCode = "M001",     // ← 對應 MarketInfo.MarketCode
				TransDate = DateOnly.FromDateTime(DateTime.Today),  // ← 落在預設日期範圍內
				UpperPrice = 15.0m,
				MiddlePrice = 13.0m,
				LowerPrice = 11.0m,
				AvgPrice = 13.0m,
				TransQuantity = 100m,
				TcType = "A"
			});

			await dbContext.SaveChangesAsync();

			// 建立查詢上限選項（GetPricesAsync 用不到，但建構式需要）
			//    所以給一個空的 Mock 就好，不需要 Setup 任何規則

			// 4. 建立被測對象
			//    mockCache.Object → 假 Redis（會回傳 null，觸發 Cache Miss）
			//    dbContext        → InMemory DB（有真實假資料，JOIN 得到結果）
			var service = new MarketService(
				dbContext,
				mockCache.Object,
				Microsoft.Extensions.Options.Options.Create(new TaiwanAgri.Modules.Market.Constants.MarketQueryOptions()),
				TimeProvider.System,
				NullLogger<MarketService>.Instance);

			// ══════════════════════════════════════════════
			// Act：開演
			// 呼叫被測方法，Redis 回 null → 去查 DB → 結果寫進 Redis
			// ══════════════════════════════════════════════

			var result = await service.GetPricesAsync(
				marketType: "Veg",
				cropCodes: new[] { "A001" }
			);

			// ══════════════════════════════════════════════
			// Assert：檢查結果
			// 驗證三件事：DB 查到資料、結果正確、結果有被寫進 Redis
			// ══════════════════════════════════════════════

			// 5. 驗證回傳筆數正確（只有一筆，因為 GroupBy 後只有一個日期 x 一個作物）
			Assert.Single(result);

			// 6. 驗證回傳的第一筆資料內容正確
			//    CropCode 和 CropName 來自 JOIN 後的聚合結果
			Assert.Equal("A001", result[0].CropCode);
			Assert.Equal("高麗菜", result[0].CropName);

			// 7. 驗證 GetAsync 被呼叫了恰好一次
			//    Cache Miss 還是會先問 Redis，只是答案是 null
			mockCache.Verify(
				c => c.GetAsync(
					It.IsAny<string>(),
					It.IsAny<CancellationToken>()),
				Times.Once());

			// 8. 驗證 SetAsync 被呼叫了恰好一次
			//    Cache Miss 路徑：查完 DB 之後，結果應該被寫進 Redis（TTL 25 小時）
			//    如果 SetAsync 沒被呼叫，代表寫快取的邏輯沒有執行，是個 bug
			mockCache.Verify(
				c => c.SetAsync(
					It.IsAny<string>(),
					It.IsAny<byte[]>(),
					It.IsAny<DistributedCacheEntryOptions>(),
					It.IsAny<CancellationToken>()),
				Times.Once());
		}

		/// <summary>
		/// 建一個能讓 GetPricesAsync 的三表 JOIN 命中的最小資料集。
		/// 上面兩則測試各自把這段寫在測試本體裡——那是它們要說明的東西之一，所以原樣保留；
		/// 下面三則降級測試要說的是別的事，佈景因此抽出來
		/// </summary>
		private static async Task<MarketDbContext> SeedOneRowAsync(string dbName)
		{
			var options = new DbContextOptionsBuilder<MarketDbContext>()
				.UseInMemoryDatabase(dbName)
				.Options;
			var dbContext = new MarketDbContext(options);

			dbContext.CropInfos.Add(new CropInfo { CropCode = "A001", CropName = "高麗菜" });

			dbContext.MarketInfos.Add(new MarketInfo
			{
				MarketCode = "M001",
				MarketName = "台北果菜",
				MarketType = "Veg"
			});

			dbContext.AgriProductsTrans.Add(new AgriProductsTrans
			{
				CropCode = "A001",
				MarketCode = "M001",
				TransDate = DateOnly.FromDateTime(DateTime.Today),
				UpperPrice = 15.0m,
				MiddlePrice = 13.0m,
				LowerPrice = 11.0m,
				AvgPrice = 13.0m,
				TransQuantity = 100m,
				TcType = "A"
			});

			await dbContext.SaveChangesAsync();
			return dbContext;
		}

		/// <summary>
		/// 寫成 <c>new MarketService(...)</c> 而不是目標型別推斷的 <c>new(...)</c>：
		/// 本輪替這支服務加建構式參數時，同檔另外兩處寫成 <c>new(...)</c> 的呼叫點
		/// 用 grep 完全找不到，只有編譯器報錯才發現
		/// </summary>
		private static MarketService CreateService(MarketDbContext dbContext, IDistributedCache cache) =>
			new MarketService(
				dbContext,
				cache,
				Microsoft.Extensions.Options.Options.Create(new TaiwanAgri.Modules.Market.Constants.MarketQueryOptions()),
				TimeProvider.System,
				NullLogger<MarketService>.Instance);

		private static Mock<IDistributedCache> CacheThatMisses()
		{
			var mockCache = new Mock<IDistributedCache>();
			mockCache
				.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync((byte[]?)null);
			return mockCache;
		}

		private static void VerifyWroteCacheOnce(Mock<IDistributedCache> mockCache) =>
			mockCache.Verify(
				c => c.SetAsync(
					It.IsAny<string>(),
					It.IsAny<byte[]>(),
					It.IsAny<DistributedCacheEntryOptions>(),
					It.IsAny<CancellationToken>()),
				Times.Once());

		/// <summary>
		/// 快取連不上時的正確行為是「慢一點」而不是「壞掉」——快取是加速層、資料庫才是真相。
		/// 實測過的反面：讀取的例外沒人接時，GET /api/market/prices 會先卡 6.5 秒
		/// （StackExchange.Redis 把指令排進 backlog 等到逾時）再回 500，
		/// 而同一時間資料庫從頭到尾都是好的、沒有人去拿
		/// </summary>
		[Fact]
		public async Task 快取讀取拋例外時改查資料庫而不是讓查詢失敗()
		{
			var mockCache = new Mock<IDistributedCache>();
			mockCache
				.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ThrowsAsync(new InvalidOperationException("模擬快取連線失敗"));

			var dbContext = await SeedOneRowAsync("TestDb_CacheReadThrows");
			var service = CreateService(dbContext, mockCache.Object);

			var result = await service.GetPricesAsync(marketType: "Veg", cropCodes: ["A001"]);

			Assert.Single(result);
			Assert.Equal("高麗菜", result[0].CropName);
		}

		/// <summary>
		/// 寫入失敗比讀取失敗更不能往上拋：結果已經查到手上了，
		/// 為了「沒能存進快取」而讓整支查詢失敗，是拿加速層的問題去砸掉一次已經成功的查詢
		/// </summary>
		[Fact]
		public async Task 快取寫入拋例外時查詢結果照樣回傳()
		{
			var mockCache = CacheThatMisses();
			mockCache
				.Setup(c => c.SetAsync(
					It.IsAny<string>(),
					It.IsAny<byte[]>(),
					It.IsAny<DistributedCacheEntryOptions>(),
					It.IsAny<CancellationToken>()))
				.ThrowsAsync(new InvalidOperationException("模擬快取連線失敗"));

			var dbContext = await SeedOneRowAsync("TestDb_CacheWriteThrows");
			var service = CreateService(dbContext, mockCache.Object);

			var result = await service.GetPricesAsync(marketType: "Veg", cropCodes: ["A001"]);

			Assert.Single(result);
		}

		/// <summary>
		/// 壞值與連不上走同一條降級路徑，但壞值多一個要求：必須覆寫那一筆快取。
		/// 不覆寫的話，壞值會被 25 小時 TTL 鎖住，期間每個請求都重走一次 DB
		/// </summary>
		[Fact]
		public async Task 快取內容壞掉時落回資料庫並覆寫該筆快取()
		{
			var mockCache = new Mock<IDistributedCache>();
			mockCache
				.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
				.ReturnsAsync(Encoding.UTF8.GetBytes("{ 這不是合法的 JSON 陣列 }"));

			var dbContext = await SeedOneRowAsync("TestDb_CacheCorruptValue");
			var service = CreateService(dbContext, mockCache.Object);

			var result = await service.GetPricesAsync(marketType: "Veg", cropCodes: ["A001"]);

			Assert.Single(result);
			VerifyWroteCacheOnce(mockCache);
		}
	}
}
