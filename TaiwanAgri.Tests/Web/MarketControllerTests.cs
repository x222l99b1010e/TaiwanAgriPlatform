using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TaiwanAgri.Modules.Market.Constants;
using Moq;
using TaiwanAgri.Modules.Market.Dtos.ApiResponses;
using TaiwanAgri.Modules.Market.Services;
using TaiwanAgri.Web.Controllers;

namespace TaiwanAgri.Tests.Web
{
	/// <summary>
	/// MarketController 的參數驗證與回應標頭。
	/// 這些端點全部公開、參數全部來自使用者，驗證邏輯卻一條測試都沒有
	/// </summary>
	public class MarketControllerTests
	{
		private static MarketController CreateController(Mock<IMarketService> service)
		{
			// 上限改走強型別選項後，測試不必再組一份假設定，直接給值即可
			var options = Options.Create(new MarketQueryOptions { CropCodesMaxCount = 5 });
			return new MarketController(service.Object, options)
			{
				ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
			};
		}

		[Fact]
		public async Task 家禽指標代碼打錯字要回400而不是空陣列()
		{
			// 安靜回空是最難查的錯誤——打錯字跟「這段期間真的沒資料」看起來一模一樣
			var service = new Mock<IMarketService>();
			var controller = CreateController(service);

			var result = await controller.GetPoultry(new[] { "Egg_Producer", "NotARealCode" });

			var bad = Assert.IsType<BadRequestObjectResult>(result);
			Assert.Contains("NotARealCode", bad.Value?.ToString());
			service.Verify(s => s.GetPoultryAsync(It.IsAny<string[]>(), It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>()),
				Times.Never());
		}

		[Fact]
		public async Task 家禽不帶指標代表全部指標()
		{
			var service = new Mock<IMarketService>();
			service.Setup(s => s.GetPoultryAsync(It.IsAny<string[]?>(), It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>()))
				.ReturnsAsync(new List<PoultryResponseDto>());
			var controller = CreateController(service);

			Assert.IsType<OkObjectResult>(await controller.GetPoultry());
		}

		[Theory]
		[InlineData("2026-13-01")]
		[InlineData("not-a-date")]
		[InlineData("2026/09/05")]
		public async Task 日期格式錯誤要回400(string badDate)
		{
			var service = new Mock<IMarketService>();
			var controller = CreateController(service);

			Assert.IsType<BadRequestObjectResult>(await controller.GetPork(startDate: badDate));
		}

		[Fact]
		public async Task 作物代碼超過上限要回400()
		{
			var service = new Mock<IMarketService>();
			var controller = CreateController(service);

			var result = await controller.GetPrices("Veg", new[] { "C1", "C2", "C3", "C4", "C5", "C6" });

			Assert.IsType<BadRequestObjectResult>(result);
		}

		[Theory]
		[InlineData("Meat")]
		[InlineData("")]
		public async Task 市場類型不在白名單要回400(string marketType)
		{
			var service = new Mock<IMarketService>();
			var controller = CreateController(service);

			Assert.IsType<BadRequestObjectResult>(await controller.GetMarkets(marketType));
		}

		[Fact]
		public async Task 天災結果被截斷時要加上截斷標頭()
		{
			// 截斷的清單看起來完整、實際上 AffectedCounties 會少縣市，
			// 沒有訊號的話呼叫端無從察覺
			var service = new Mock<IMarketService>();
			service.Setup(s => s.GetDisastersAsync(It.IsAny<string[]>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
				.ReturnsAsync((new List<DisasterResponseDto>(), true));
			var controller = CreateController(service);

			await controller.GetDisasters(Array.Empty<string>(), "2026-01-01", "2026-12-31");

			Assert.Equal("true", controller.Response.Headers["X-Result-Truncated"]);
		}

		[Fact]
		public async Task 天災結果沒被截斷時不加標頭()
		{
			var service = new Mock<IMarketService>();
			service.Setup(s => s.GetDisastersAsync(It.IsAny<string[]>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>()))
				.ReturnsAsync((new List<DisasterResponseDto>(), false));
			var controller = CreateController(service);

			await controller.GetDisasters(Array.Empty<string>(), "2026-01-01", "2026-12-31");

			Assert.False(controller.Response.Headers.ContainsKey("X-Result-Truncated"));
		}
	}
}
