using Microsoft.AspNetCore.Mvc;
using TaiwanAgri.Core.Helpers;
using TaiwanAgri.Modules.Market.Services;

namespace TaiwanAgri.Web.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	public class MarketController : ControllerBase
	{		
		private readonly IMarketService _marketService;
		public MarketController(IMarketService marketService)
		{
			_marketService = marketService;
		}
		[HttpGet("Pork")]
		public async Task<IActionResult> GetPork(
			[FromQuery] string? marketName = null,
			[FromQuery] string? startDate = null,
			[FromQuery] string? endDate = null)
		{
			var start = DateHelper.ParseIsoDate(startDate);
			var end = DateHelper.ParseIsoDate(endDate);
			if (startDate != null && start == null) return BadRequest("開始日期 格式錯誤，請使用 yyyy-MM-dd");
			if (endDate != null && end == null) return BadRequest("結束日期 格式錯誤，請使用 yyyy-MM-dd");
			var result = await _marketService.GetPorkAsync(marketName, start, end);
			return Ok(result);
		}
		[HttpGet("restDays")]
		public async Task<IActionResult> GetRestDays(
			[FromQuery] string marketCode,
			[FromQuery] string startDate,
			[FromQuery] string endDate)
		{
			if (string.IsNullOrWhiteSpace(marketCode)) 
			{
				return BadRequest("marketCode 為必填");
			}
			var start = DateHelper.ParseIsoDate(startDate);
			var end = DateHelper.ParseIsoDate(endDate);

			if (start == null) return BadRequest("開始日期 格式錯誤，請使用 yyyy-MM-dd");
			if (end == null) return BadRequest("結束日期 格式錯誤，請使用 yyyy-MM-dd");

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
			[FromQuery] string endDate)   // ← 移除 alertDate 參數和驗證
		{
			var start = DateHelper.ParseIsoDate(startDate);
			var end = DateHelper.ParseIsoDate(endDate);

			if (start == null) return BadRequest("開始日期 格式錯誤，請使用 yyyy-MM-dd");
			if (end == null) return BadRequest("結束日期 格式錯誤，請使用 yyyy-MM-dd");

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
			if (cropCodes == null || cropCodes.Length == 0)
			{
				// 填入 BadRequest
				return BadRequest("cropCodes 為必填，至少需傳入一個作物代碼");
			}
			else if (cropCodes.Length > 5)
			{
				// 填入 BadRequest
				return BadRequest("cropCodes 最多只能傳入 5 個");
			}
			var start = DateHelper.ParseIsoDate(startDate);
			var end = DateHelper.ParseIsoDate(endDate);

			if (startDate != null && start == null) return BadRequest("開始日期 格式錯誤，請使用 yyyy-MM-dd");
			if (endDate != null && end == null) return BadRequest("結束日期 格式錯誤，請使用 yyyy-MM-dd");

			var result = await _marketService.GetPricesAsync(marketType, cropCodes, marketCode, start, end);
			return Ok(result);
		}
	}
}
