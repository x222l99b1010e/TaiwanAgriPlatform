using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TaiwanAgri.Modules.Weather.Services;

namespace TaiwanAgri.Web.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	public class PestController : ControllerBase
	{
		private readonly IPestService _pestService;
		public PestController(IPestService pestService)
		{
			_pestService = pestService;
		}
		[HttpGet("alerts")]
		public async Task<IActionResult> GetPestAlertsByCity([FromQuery] string? cityName = null, [FromQuery] int page = 1)
		{
			var pestAlerts = await _pestService.GetPestAlertsByCityAsync(cityName, page);
			return Ok(pestAlerts);
		}

		[HttpGet("decade-density")]
		public async Task<IActionResult> GetPestDecadeDensityByPestName([FromQuery] string pestName)
		{
			if (string.IsNullOrWhiteSpace(pestName))
			{
				return BadRequest("害蟲名稱為必填!");
			}
			var pestDecadeSummaries = await _pestService.GetPestDecadeDensityByPestNameAsync(pestName);
			return Ok(pestDecadeSummaries);
		}
		[HttpGet("pest-names")]
		public async Task<IActionResult> GetAllPestName()
		{
			var pestNames = await _pestService.GetAllPestNamesAsync();
			return Ok(pestNames);
		}
	}
}