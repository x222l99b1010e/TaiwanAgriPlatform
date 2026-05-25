using Microsoft.AspNetCore.Mvc;
using TaiwanAgri.Core.Helpers;
using TaiwanAgri.Modules.Weather.Services;

namespace TaiwanAgri.Web.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	public class WeatherController : ControllerBase
	{
		private readonly IWeatherService _weatherService;
		public WeatherController(IWeatherService weatherService)
		{
			_weatherService = weatherService;
		}
		[HttpGet("rainfall")]
		public async Task<IActionResult> GetRainfallByCity(
			[FromQuery] string cityName,
			[FromQuery] string? startDate,
			[FromQuery] string? endDate)
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

			var result = await _weatherService.GetRainfallByCityAsync(cityName, start, end);

			return Ok(result);
		}
		[HttpGet("stations")]
		public async Task<IActionResult> GetStationsByCity(
			[FromQuery] string cityName)
		{
			if (string.IsNullOrWhiteSpace(cityName))
			{
				return BadRequest("城市名稱為必填!");
			}

			var result = await _weatherService.GetStationsByCityAsync(cityName);
			return Ok(result);
		}
	}
}
