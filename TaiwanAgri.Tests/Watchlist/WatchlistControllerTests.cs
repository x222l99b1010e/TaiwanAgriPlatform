using Moq;
using System.Security.Claims;
using TaiwanAgri.Modules.Market.Dtos.ApiResponses;
using TaiwanAgri.Modules.Market.Services;
using TaiwanAgri.Modules.User.Dtos.ApiResponses;
using TaiwanAgri.Modules.User.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TaiwanAgri.Web.Controllers;

namespace TaiwanAgri.Tests.Watchlist
{
	public class WatchlistControllerTests
	{
		[Fact]
		public async Task GetWatchlistItems_EmptyList_ReturnsEmptyResult()
		{
			// ── Arrange ──────────────────────────────────────────
			// 目標：監看清單是空的，應該直接回傳空清單，不呼叫 MarketService

			// 1. Mock 兩個 Service（Pattern C 的兩個依賴）
			//    這是跟前兩個測試最大的不同：
			//    前兩個測試用 InMemory DB 隔離，這裡用 Mock 隔離 Service 介面
			//    因為我們只測 Controller 的組合邏輯，不需要真實 Service 的實作
			var mockUserWatchlistService = new Mock<IUserWatchlistService>();
			var mockMarketService = new Mock<IMarketService>();

			// 2. 設定假 UserWatchlistService：回傳空清單
			//    模擬「這個使用者沒有任何監看項目」的情境
			mockUserWatchlistService
				.Setup(s => s.GetUserWatchlistItemsAsync(It.IsAny<string>()))
				.ReturnsAsync(new List<WatchlistItemDto>());

			// 3. 建立 Controller，注入兩個假 Service
			var controller = new WatchlistController(
				mockUserWatchlistService.Object,
				mockMarketService.Object);

			// 4. 設定假的 HttpContext，讓 User.FindFirstValue 能回傳假的 userId
			//    不設定這個的話，User 是 null，程式會直接炸掉
			//    這是 Controller 單元測試特有的步驟，Service 測試不需要
			var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "user-001") };
			var identity = new ClaimsIdentity(claims);
			controller.ControllerContext = new ControllerContext
			{
				HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
			};

			// ── Act ──────────────────────────────────────────────
			var actionResult = await controller.GetWatchlistItems();

			// ── Assert ───────────────────────────────────────────

			// 5. 驗證回傳是 OkObjectResult（HTTP 200）
			var okResult = Assert.IsType<OkObjectResult>(actionResult);

			// 6. 驗證回傳內容是空清單
			var items = Assert.IsAssignableFrom<IEnumerable<WatchlistEnrichedItemDto>>(okResult.Value);
			Assert.Empty(items);

			// 7. 驗證 MarketService 完全沒被呼叫
			//    清單是空的，不應該去查任何價格
			mockMarketService.Verify(
				s => s.GetPricesAsync(
					It.IsAny<string>(),
					It.IsAny<string[]>(),
					It.IsAny<string?>(),
					It.IsAny<DateOnly?>(),
					It.IsAny<DateOnly?>()),
				Times.Never());
		}

		[Fact]
		public async Task GetWatchlistItems_WithOneItem_ReturnsEnrichedDto()
		{
			// ── Arrange ──────────────────────────────────────────
			// 目標：監看清單有一筆，應該去查價格並組合出完整的 WatchlistEnrichedItemDto

			// 1. Mock 兩個 Service
			var mockUserWatchlistService = new Mock<IUserWatchlistService>();
			var mockMarketService = new Mock<IMarketService>();

			// 2. 設定假 UserWatchlistService：回傳一筆假監看項目
			var fakeWatchlistItem = new WatchlistItemDto
			{
				Id = 1,
				CropCode = "A001",
				CropName = "高麗菜",
				MarketCode = "M001",
				MarketName = "台北果菜",
				MarketType = "Veg"
			};
			mockUserWatchlistService
				.Setup(s => s.GetUserWatchlistItemsAsync(It.IsAny<string>()))
				.ReturnsAsync(new List<WatchlistItemDto> { fakeWatchlistItem });

			// 3. 設定假 MarketService：回傳一筆假價格
			//    Controller 會取 OrderByDescending(TransDate).FirstOrDefault()
			//    所以只需要準備一筆，它就會是最新的那筆
			var fakePrices = new List<PriceResponseDto>
			{
				new PriceResponseDto
				{
					CropCode = "A001",
					CropName = "高麗菜",
					AvgPrice = 12.5m,
					TransDate = new DateOnly(2026, 1, 1)
				}
			};
			mockMarketService
				.Setup(s => s.GetPricesAsync(
					It.IsAny<string>(),
					It.IsAny<string[]>(),
					It.IsAny<string?>(),
					It.IsAny<DateOnly?>(),
					It.IsAny<DateOnly?>()))
				.ReturnsAsync(fakePrices);

			// 4. 建立 Controller，注入兩個假 Service
			var controller = new WatchlistController(
				mockUserWatchlistService.Object,
				mockMarketService.Object);

			// 5. 設定假的 HttpContext
			var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "user-001") };
			var identity = new ClaimsIdentity(claims);
			controller.ControllerContext = new ControllerContext
			{
				HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
			};

			// ── Act ──────────────────────────────────────────────
			var actionResult = await controller.GetWatchlistItems();

			// ── Assert ───────────────────────────────────────────

			// 6. 驗證回傳是 OkObjectResult（HTTP 200）
			var okResult = Assert.IsType<OkObjectResult>(actionResult);

			// 7. 驗證回傳內容是一筆 WatchlistEnrichedItemDto 的清單
			var items = Assert.IsAssignableFrom<IEnumerable<WatchlistEnrichedItemDto>>(okResult.Value);
			var itemList = items.ToList();
			Assert.Single(itemList);

			// 8. 驗證 DTO 欄位正確組合
			//    Controller 把 UserWatchlistItem 和 PriceResponseDto 組合在一起
			//    這就是 Pattern C 的核心：跨模組資料在 Controller 層聚合
			var dto = itemList[0];
			Assert.Equal("A001", dto.CropCode);
			Assert.Equal("高麗菜", dto.CropName);
			Assert.Equal(12.5m, dto.AvgPrice);
			Assert.Equal(new DateOnly(2026, 1, 1), dto.TransDate);

			// 9. 驗證 MarketService 被呼叫了恰好一次
			//    foreach 裡有一筆監看項目，所以應該查了一次價格
			mockMarketService.Verify(
				s => s.GetPricesAsync(
					It.IsAny<string>(),
					It.IsAny<string[]>(),
					It.IsAny<string?>(),
					It.IsAny<DateOnly?>(),
					It.IsAny<DateOnly?>()),
				Times.Once());
		}
	}
}
