using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
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
		public async Task GetPricesAsync_CacheHit_ReturnsCachedData()
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
				TimeProvider.System);

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
		public async Task GetPricesAsync_CacheMiss_QueriesDbAndCachesResult()
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
				TimeProvider.System);

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
	}
}
