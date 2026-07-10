using Microsoft.AspNetCore.Mvc;
using TaiwanAgri.Modules.FoodSafety.Dtos.Queries;
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
		private readonly string _todayVegMarketCode;
		private readonly string[] _todayVegCropCodes;

		// 設定外化（同 MarketQueryLimits 慣例）：appsettings 可覆寫，未設定時用預設值
		// 預設值：台北一（109）固定 10 種民生蔬菜
		private static readonly string[] DefaultVegCropCodes = new[]
		{
			"LA2", "SE1", "SB1", "LH1", "FJ1",
			"FI1", "FB1", "LF1", "LD1", "SP1"
		};
		public FoodSafetyController(IMarketService marketService, IFoodSafetyService foodSafetyService, IConfiguration configuration)
		{
			_marketService = marketService;
			_foodSafetyService = foodSafetyService;
			_todayVegMarketCode = configuration.GetValue<string>("FoodSafety:TodayVeg:MarketCode") ?? "109";
			_todayVegCropCodes = configuration.GetSection("FoodSafety:TodayVeg:CropCodes").Get<string[]>()
				?? DefaultVegCropCodes;
		}

		[HttpGet("today-veg-prices")]
		public async Task<IActionResult> GetTodayVegPrices()
		{
			// 取 DB 裡最新的交易日（不是今天，是實際有資料的最近一天）
			var latestDate = await _marketService.GetLatestTransDateAsync(_todayVegMarketCode);
			if (latestDate == null)
				return Ok(new List<PriceResponseDto>());

			var result = await _marketService.GetPricesAsync("Veg", _todayVegCropCodes, _todayVegMarketCode, latestDate.Value, latestDate.Value);
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

		[HttpGet("violations")]
		public async Task<IActionResult> GetViolations([FromQuery] int days = 90, [FromQuery] string? inspectResult = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
		{
			if (days <= 0)
				return BadRequest("天數必須大於 0");
			if (days > 3650)
				return BadRequest("天數不可超過 3650");
			if (page <= 0)
				return BadRequest("頁碼必須大於 0");
			if (pageSize <= 0 || pageSize > 100)
				return BadRequest("每頁筆數必須大於 0 且小於等於 100");
			var result = await _foodSafetyService.GetViolationsAsync(days, inspectResult, page, pageSize);
			return Ok(result);
		}

		[HttpGet("organic-certifications")]
		public async Task<IActionResult> GetOrganicCertifications([FromQuery] OrganicCertificationQueryDto queryDto)
		{
			if (queryDto.Page <= 0)
				return BadRequest("頁碼必須大於 0");
			if (queryDto.PageSize <= 0 || queryDto.PageSize > 100)
				return BadRequest("每頁筆數必須大於 0 且小於等於 100");

			var result = await _foodSafetyService.GetOrganicCertificationsAsync(queryDto);
			return Ok(result);
		}
	}
}
