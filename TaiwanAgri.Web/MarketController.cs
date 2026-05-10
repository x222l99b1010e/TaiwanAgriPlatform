using Microsoft.AspNetCore.Mvc;
using TaiwanAgri.Core.Helpers;
using TaiwanAgri.Modules.Market.Services;

namespace TaiwanAgri.Web
{
	[Route("api/market")]
	[ApiController]
	public class MarketController : ControllerBase
	{		
		private readonly IMarketService _marketService;
		public MarketController(IMarketService marketService)
		{
			_marketService = marketService;
		}
		[HttpGet("restDays")]
		public async Task<IActionResult> GetRestDays(
			[FromQuery] string marketCode,
			[FromQuery] string startDate,
			[FromQuery] string endDate)
		{
			var start = DateHelper.ParseIsoDate(startDate);
			var end = DateHelper.ParseIsoDate(endDate);

			if (start == null) return BadRequest("startDate 格式錯誤，請使用 yyyy-MM-dd");
			if (end == null) return BadRequest("endDate 格式錯誤，請使用 yyyy-MM-dd");

			var result = await _marketService.GetRestDaysAsync(marketCode, start.Value, end.Value);
			return Ok(result);
		}
		[HttpGet("markets")]
		public async Task<IActionResult> GetMarkets([FromQuery] string marketType)
		{
			var result = await _marketService.GetMarketsAsync(marketType);
			return Ok(result);
		}
		[HttpGet("crops")]
		public async Task<IActionResult> GetCrops([FromQuery] string marketType)
		{
			var result = await _marketService.GetCropsAsync(marketType);
			return Ok(result);
		}
		[HttpGet("disasters")]
		public async Task<IActionResult> GetDisasters(
			[FromQuery] string[] counties, 
			[FromQuery] string startDate, 
			[FromQuery] string endDate)
		{
			var start = DateHelper.ParseIsoDate(startDate);
			var end = DateHelper.ParseIsoDate(endDate);

			if (start == null) return BadRequest("startDate 格式錯誤，請使用 yyyy-MM-dd");
			if (end == null) return BadRequest("endDate 格式錯誤，請使用 yyyy-MM-dd");
			var result = await _marketService.GetDisastersAsync(counties, start.Value, end.Value);
			return Ok(result);
		}

		[HttpGet("prices")]
		public async Task<IActionResult> GetPrices(
			[FromQuery] string marketType,
			[FromQuery] string[] cropCodes,
			[FromQuery] string? marketCode = null,
			[FromQuery] string? startDate = null,
			[FromQuery] string? endDate = null)
		{
			var start = DateHelper.ParseIsoDate(startDate);
			var end = DateHelper.ParseIsoDate(endDate);

			if (startDate != null && start == null) return BadRequest("startDate 格式錯誤，請使用 yyyy-MM-dd");
			if (endDate != null && end == null) return BadRequest("endDate 格式錯誤，請使用 yyyy-MM-dd");

			var result = await _marketService.GetPricesAsync(marketType, cropCodes, marketCode, start, end);
			return Ok(result);
		}
	}
}
