using Microsoft.AspNetCore.Mvc;
using TaiwanAgri.Core.Helpers;
using TaiwanAgri.Modules.Weather.Dtos.Queries;
using TaiwanAgri.Modules.Weather.Services;

namespace TaiwanAgri.Web.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	public class WeatherController : ControllerBase
	{
		/// <summary>關鍵字長度上限。上游是 contains 比對，超長字串不可能命中，先擋下省一次外部呼叫。</summary>
		private const int MaxKeywordLength = 50;

		private readonly IWeatherService _weatherService;
		private readonly IPesticideService _pesticideService;
		public WeatherController(IWeatherService weatherService, IPesticideService pesticideService)
		{
			_weatherService = weatherService;
			_pesticideService = pesticideService;
		}
		[HttpGet("rainfall")]
		public async Task<IActionResult> GetRainfallByCity(
			[FromQuery] string cityName,
			[FromQuery] string? startDate,
			[FromQuery] string? endDate, CancellationToken cancellationToken = default)
		{
			if (string.IsNullOrWhiteSpace(cityName))
			{
				return BadRequest("城市名稱為必填!");
			}

			var start = DateHelper.ParseIsoDate(startDate);
			var end = DateHelper.ParseIsoDate(endDate);

			if (startDate != null && start == null)
			{
				return BadRequest("startDate 格式錯誤，請使用 yyyy-MM-dd");
			}

			if (endDate != null && end == null)
			{
				return BadRequest("endDate 格式錯誤，請使用 yyyy-MM-dd");
			}

			var result = await _weatherService.GetRainfallByCityAsync(cityName, start, end, cancellationToken);

			return Ok(result);
		}
		[HttpGet("stations")]
		public async Task<IActionResult> GetStationsByCity(
			[FromQuery] string cityName, CancellationToken cancellationToken = default)
		{
			if (string.IsNullOrWhiteSpace(cityName))
			{
				return BadRequest("城市名稱為必填!");
			}

			var result = await _weatherService.GetStationsByCityAsync(cityName, cancellationToken);
			return Ok(result);
		}

		/// <summary>
		/// 農藥查詢（模組 2 F05）：輸入有效成分俗名，回傳許可證與核准用途。
		/// 即時打農業部 API、不落地 DB。
		/// </summary>
		[HttpGet("pesticides")]
		public async Task<IActionResult> SearchPesticides(
			[FromQuery] PesticideSearchQueryDto queryDto,
			CancellationToken cancellationToken = default)
		{
			var keyword = queryDto.Keyword?.Trim() ?? string.Empty;
			var englishName = queryDto.EnglishName?.Trim() ?? string.Empty;

			if (keyword.Length == 0 && englishName.Length == 0)
			{
				return BadRequest("請至少填寫中文或英文的農藥成分名稱");
			}

			if (keyword.Length > MaxKeywordLength || englishName.Length > MaxKeywordLength)
			{
				return BadRequest($"農藥名稱長度不可超過 {MaxKeywordLength} 個字");
			}

			// 英文名欄位走字元白名單，不是「偵測到中文／全形就擋」的黑名單——
			// 黑名單只擋得住列舉得出來的東西，全形英數、CJK、假名、emoji、零寬字元、
			// 各種 Unicode 空白，漏一類就破功；白名單則是「沒明確允許的一律擋下」。
			if (englishName.Length > 0 && !PesticideSearchQueryDto.IsValidEnglishName(englishName))
			{
				return BadRequest("英文成分名只能輸入英文字母、數字與 + - , . ' ( ) / 等符號，且需包含至少一個英文字母");
			}

			var outcome = await _pesticideService.SearchAsync(
				new PesticideSearchQueryDto
				{
					Keyword = keyword,
					EnglishName = englishName,
					IncludeRevoked = queryDto.IncludeRevoked
				},
				cancellationToken);

			// 上游一頁 500 筆且未帶 api_key 拿不到第二頁，過於寬鬆的關鍵字（如單字「滅」）
			// 會被安靜截斷。回傳那 500 筆等於給一份殘缺、而且是照許可證號排序（與相關性無關）
			// 的結果，比查不到更容易誤導，因此改為要求使用者收斂關鍵字。
			if (outcome.KeywordTooBroad)
			{
				return BadRequest("查詢結果過多，請輸入更完整的農藥名稱（例如「亞滅培」而非「滅」）");
			}

			// 查無資料回空集合而非 404：查得到「沒有這支農藥」本身就是有效的查詢結果
			return Ok(outcome.Response);
		}
	}
}
