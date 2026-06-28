using Microsoft.AspNetCore.Mvc;
using TaiwanAgri.Modules.FoodSafety.Services;
using TaiwanAgri.Modules.Market.Dtos.ApiResponses;
using TaiwanAgri.Modules.Market.Services;

namespace TaiwanAgri.Web.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	public class FoodSafetyController : ControllerBase
	{
		private readonly IMarketService _marketService;
		private readonly IFoodSafetyService _foodSafetyService;
		private static readonly string[] DefaultVegCropCodes = new[]
		{
			"LA2", "SE1", "SB1", "LH1", "FJ1",
			"FI1", "FB1", "LF1", "LD1", "SP1"
		};
		public FoodSafetyController(IMarketService marketService, IFoodSafetyService foodSafetyService)
		{
			_marketService = marketService;
			_foodSafetyService = foodSafetyService;
		}

		[HttpGet("today-veg-prices")]
		public async Task<IActionResult> GetTodayVegPrices()
		{
			// 取 DB 裡最新的交易日（不是今天，是實際有資料的最近一天）
			var latestDate = await _marketService.GetLatestTransDateAsync("109");
			if (latestDate == null)
				return Ok(new List<PriceResponseDto>());

			var result = await _marketService.GetPricesAsync("Veg", DefaultVegCropCodes, "109", latestDate.Value, latestDate.Value);
			return Ok(result);

		}

		[HttpGet("traceability")]
		public async Task<IActionResult> SearchTraceability([FromQuery] string traceCode)
		{
			if (string.IsNullOrWhiteSpace(traceCode))
				return BadRequest("追溯碼為必填");

			var result = await _foodSafetyService.SearchTraceabilityAsync(traceCode.Trim());
			return Ok(result);
		}
	}
}
