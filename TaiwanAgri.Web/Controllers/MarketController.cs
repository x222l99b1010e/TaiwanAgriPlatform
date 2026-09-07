using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TaiwanAgri.Core.Helpers;
using TaiwanAgri.Modules.Market.Constants;
using TaiwanAgri.Modules.Market.Services;

namespace TaiwanAgri.Web.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	public class MarketController : ControllerBase
	{
		private const string InvalidMarketTypeMessage = "marketType 必須為 Veg、Fruit 或 Flower";
		private readonly IMarketService _marketService;
		private readonly MarketQueryOptions _options;
		public MarketController(IMarketService marketService, IOptions<MarketQueryOptions> options)
		{
			_marketService = marketService;
			_options = options.Value;
		}
		[HttpGet("pork")]
		public async Task<IActionResult> GetPork(
			[FromQuery] string? marketName = null,
			[FromQuery] string? startDate = null,
			[FromQuery] string? endDate = null, CancellationToken cancellationToken = default)
		{
			var start = DateHelper.ParseIsoDate(startDate);
			var end = DateHelper.ParseIsoDate(endDate);
			if (startDate != null && start == null) return BadRequest("開始日期 格式錯誤，請使用 yyyy-MM-dd");
			if (endDate != null && end == null) return BadRequest("結束日期 格式錯誤，請使用 yyyy-MM-dd");
			var result = await _marketService.GetPorkAsync(marketName, start, end, cancellationToken);
			return Ok(result);
		}
		[HttpGet("poultry")]
		public async Task<IActionResult> GetPoultry(
			[FromQuery] string[]? metricCodes = null,
			[FromQuery] string? startDate = null,
			[FromQuery] string? endDate = null, CancellationToken cancellationToken = default)
		{
			// 白名單驗證：不合法的代碼直接擋下，不讓它安靜地回空陣列
			// （安靜回空是最難查的錯誤——打錯字跟「這段期間真的沒資料」看起來一樣）
			if (metricCodes is { Length: > 0 })
			{
				var invalid = metricCodes.Where(c => !PoultryMetrics.IsValid(c)).ToArray();
				if (invalid.Length > 0)
					return BadRequest($"不支援的 metricCodes：{string.Join(", ", invalid)}");
			}

			var start = DateHelper.ParseIsoDate(startDate);
			var end = DateHelper.ParseIsoDate(endDate);
			if (startDate != null && start == null) return BadRequest("開始日期 格式錯誤，請使用 yyyy-MM-dd");
			if (endDate != null && end == null) return BadRequest("結束日期 格式錯誤，請使用 yyyy-MM-dd");

			var result = await _marketService.GetPoultryAsync(metricCodes, start, end, cancellationToken);
			return Ok(result);
		}

		[HttpGet("poultry/metrics")]
		public IActionResult GetPoultryMetrics()
		{
			// 指標清單與中文顯示名的單一真相來源在後端，前端不必自備一份對照表
			// （前端硬編一份會與 PoultryMetrics.cs 分岔，日後加指標時漏改其中一邊）
			var metrics = PoultryMetrics.DisplayNames
				.Select(kv => new { MetricCode = kv.Key, DisplayName = kv.Value })
				.ToList();

			return Ok(metrics);
		}

		[HttpGet("rest-days")]
		public async Task<IActionResult> GetRestDays(
			[FromQuery] string marketCode,
			[FromQuery] string startDate,
			[FromQuery] string endDate, CancellationToken cancellationToken = default)
		{
			if (string.IsNullOrWhiteSpace(marketCode)) 
			{
				return BadRequest("marketCode 為必填");
			}
			var start = DateHelper.ParseIsoDate(startDate);
			var end = DateHelper.ParseIsoDate(endDate);

			if (start == null) return BadRequest("開始日期 格式錯誤，請使用 yyyy-MM-dd");
			if (end == null) return BadRequest("結束日期 格式錯誤，請使用 yyyy-MM-dd");

			var result = await _marketService.GetRestDaysAsync(marketCode, start.Value, end.Value, cancellationToken);
			return Ok(result);
		}
		[HttpGet("markets")]
		public async Task<IActionResult> GetMarkets([FromQuery] string marketType, CancellationToken cancellationToken = default)
		{
			// 驗證 marketType 是否為 "Veg"、"Fruit" 或 "Flower"
			if (!MarketTypeMapping.IsValidMarketType(marketType))
				return BadRequest(InvalidMarketTypeMessage);

			var result = await _marketService.GetMarketsAsync(marketType, cancellationToken);
			return Ok(result);
		}
		[HttpGet("crops")]
		public async Task<IActionResult> GetCrops([FromQuery] string marketType, CancellationToken cancellationToken = default)
		{
			// 驗證 marketType 是否為 "Veg"、"Fruit" 或 "Flower"
			if (!MarketTypeMapping.IsValidMarketType(marketType))
				return BadRequest(InvalidMarketTypeMessage);

			var result = await _marketService.GetCropsAsync(marketType, cancellationToken);
			return Ok(result);
		}
		[HttpGet("disasters")]
		public async Task<IActionResult> GetDisasters(
			[FromQuery] string[] counties,
			[FromQuery] string startDate,
			[FromQuery] string endDate, CancellationToken cancellationToken = default)
		{
			var start = DateHelper.ParseIsoDate(startDate);
			var end = DateHelper.ParseIsoDate(endDate);

			if (start == null) return BadRequest("開始日期 格式錯誤，請使用 yyyy-MM-dd");
			if (end == null) return BadRequest("結束日期 格式錯誤，請使用 yyyy-MM-dd");

			var (items, isTruncated) = await _marketService.GetDisastersAsync(counties, start.Value, end.Value, cancellationToken);

			// 結果被上限截斷時要讓呼叫端知道：截斷的清單看起來完整、實際殘缺
			// （同一天災的 AffectedCounties 會少縣市），沒有訊號就無從察覺
			if (isTruncated)
				Response.Headers["X-Result-Truncated"] = "true";

			return Ok(items);
		}

		[HttpGet("prices")]
		public async Task<IActionResult> GetPrices(
			[FromQuery] string marketType,
			[FromQuery] string[] cropCodes,
			[FromQuery] string? marketCode = null,
			[FromQuery] string? startDate = null,
			[FromQuery] string? endDate = null, CancellationToken cancellationToken = default)
		{
			// 驗證 marketType 是否為 "Veg"、"Fruit" 或 "Flower"
			if (!MarketTypeMapping.IsValidMarketType(marketType))
				return BadRequest(InvalidMarketTypeMessage);

			if (cropCodes == null || cropCodes.Length == 0)
			{
				return BadRequest("cropCodes 為必填，至少需傳入一個作物代碼");
			}
			else if (cropCodes.Length > _options.CropCodesMaxCount)
			{
				return BadRequest($"cropCodes 最多只能傳入 {_options.CropCodesMaxCount} 個");
			}
			var start = DateHelper.ParseIsoDate(startDate);
			var end = DateHelper.ParseIsoDate(endDate);

			if (startDate != null && start == null) return BadRequest("開始日期 格式錯誤，請使用 yyyy-MM-dd");
			if (endDate != null && end == null) return BadRequest("結束日期 格式錯誤，請使用 yyyy-MM-dd");

			var result = await _marketService.GetPricesAsync(marketType, cropCodes, marketCode, start, end, cancellationToken);
			return Ok(result);
		}
	}
}
