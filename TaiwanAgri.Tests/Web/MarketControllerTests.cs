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

		// ── 日期區間界限 ─────────────────────────────────────────────────────

		/// <summary>
		/// 五支帶日期區間的端點都要擋下顛倒的區間。逐支各測一次是因為這個檢查
		/// 是逐支呼叫的——漏掉其中一支不會讓任何既有測試變紅，那支端點只是安靜地沒有防護
		/// </summary>
		[Fact]
		public async Task 五支端點的顛倒日期區間都要回400()
		{
			var service = new Mock<IMarketService>();
			var controller = CreateController(service);
			const string 晚 = "2026-09-10";
			const string 早 = "2026-09-01";

			Assert.IsType<BadRequestObjectResult>(await controller.GetPork(startDate: 晚, endDate: 早));
			Assert.IsType<BadRequestObjectResult>(await controller.GetPoultry(startDate: 晚, endDate: 早));
			Assert.IsType<BadRequestObjectResult>(await controller.GetRestDays("M1", 晚, 早));
			Assert.IsType<BadRequestObjectResult>(await controller.GetDisasters([], 晚, 早));
			Assert.IsType<BadRequestObjectResult>(await controller.GetPrices("Veg", ["C1"], startDate: 晚, endDate: 早));
		}

		/// <summary>
		/// 區間超過上限時擋在控制器，不讓查詢送進資料庫。
		/// 這是這組界限真正防的東西——一組一九〇〇到二一〇〇的參數就是一次全表掃描，
		/// 而它在存取紀錄裡跟正常查詢長得一模一樣
		/// </summary>
		[Fact]
		public async Task 區間超過上限時不呼叫服務層()
		{
			var service = new Mock<IMarketService>();
			var controller = CreateController(service);

			var result = await controller.GetPrices("Veg", ["C1"], startDate: "1900-01-01", endDate: "2100-01-01");

			Assert.IsType<BadRequestObjectResult>(result);
			service.Verify(s => s.GetPricesAsync(
				It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<string?>(),
				It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()), Times.Never);
		}

		/// <summary>
		/// 上限可以由設定覆寫，而覆寫之後真的會生效。
		/// 選項讀進來卻沒接上使用點是這類設定最容易出的錯——設定檔改了沒反應，
		/// 而且不會有任何錯誤訊息
		/// </summary>
		[Fact]
		public async Task 設定的區間上限要真的生效()
		{
			var service = new Mock<IMarketService>();
			var controller = new MarketController(
				service.Object,
				Options.Create(new MarketQueryOptions { CropCodesMaxCount = 5, MaxQueryRangeDays = 7 }))
			{
				ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
			};

			// 七天剛好通過、八天超過
			Assert.IsNotType<BadRequestObjectResult>(
				await controller.GetPork(startDate: "2026-09-01", endDate: "2026-09-07"));
			Assert.IsType<BadRequestObjectResult>(
				await controller.GetPork(startDate: "2026-09-01", endDate: "2026-09-08"));
		}

		/// <summary>
		/// 日期是選填的端點，不帶日期時不受區間檢查影響——
		/// 界限不該把「查全部」這個既有行為擋掉
		/// </summary>
		[Fact]
		public async Task 不帶日期參數時不受區間檢查影響()
		{
			var service = new Mock<IMarketService>();
			service.Setup(s => s.GetPorkAsync(It.IsAny<string?>(), null, null, It.IsAny<CancellationToken>()))
				.ReturnsAsync([]);
			var controller = CreateController(service);

			Assert.IsType<OkObjectResult>(await controller.GetPork());
		}
	}
}
