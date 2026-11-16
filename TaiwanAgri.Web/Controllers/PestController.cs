using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TaiwanAgri.Modules.Weather.Dtos.Queries;
using TaiwanAgri.Modules.Weather.Services;
using TaiwanAgri.Web.Extensions;

namespace TaiwanAgri.Web.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	// 免登入的查詢：掛公開查詢限流（掛在類別上，新增端點自動涵蓋；見 RateLimitingExtensions）
	[EnableRateLimiting(RateLimitingExtensions.PublicQueryPolicy)]
	public class PestController : ControllerBase
	{
		private readonly IPestService _pestService;
		public PestController(IPestService pestService)
		{
			_pestService = pestService;
		}
		/// <summary>
		/// 分頁參數走共用的 PagedQueryDto：[ApiController] 在進入動作前就擋下頁碼 0、每頁 0 或超過 100，回 400。
		/// 原本收兩個沒有界限的整數，這支又免登入，帶一個參數就回 500 或一次撈走整張表（2026-11 總檢查 3-A7）
		/// </summary>
		[HttpGet("alerts")]
		public async Task<IActionResult> GetPestAlertsByCity([FromQuery] PestAlertQueryDto query, CancellationToken cancellationToken = default)
		{
			var pestAlerts = await _pestService.GetPestAlertsByCityAsync(query.CityName, query.Page, query.PageSize, cancellationToken);
			return Ok(pestAlerts);
		}

		/// <summary>各縣市被幾則警報影響（病蟲害警報頁的縣市燈號用）</summary>
		[HttpGet("alerts/city-counts")]
		public async Task<IActionResult> GetPestAlertCountsByCity(CancellationToken cancellationToken = default)
		{
			var counts = await _pestService.GetPestAlertCountsByCityAsync(cancellationToken);
			return Ok(counts);
		}

		[HttpGet("decade-density")]
		public async Task<IActionResult> GetPestDecadeDensityByPestName([FromQuery] string pestName, CancellationToken cancellationToken = default)
		{
			if (string.IsNullOrWhiteSpace(pestName))
			{
				return BadRequest("害蟲名稱為必填!");
			}
			var pestDecadeSummaries = await _pestService.GetPestDecadeDensityByPestNameAsync(pestName, cancellationToken);
			return Ok(pestDecadeSummaries);
		}
		[HttpGet("pest-names")]
		public async Task<IActionResult> GetAllPestName(CancellationToken cancellationToken = default)
		{
			var pestNames = await _pestService.GetAllPestNamesAsync(cancellationToken);
			return Ok(pestNames);
		}
	}
}