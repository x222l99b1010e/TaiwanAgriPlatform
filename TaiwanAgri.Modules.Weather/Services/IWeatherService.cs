using TaiwanAgri.Modules.Weather.Dtos.ApiResponses;

namespace TaiwanAgri.Modules.Weather.Services
{
	public interface IWeatherService
	{
		Task<List<WeatherStationResponseDto>>GetStationsByCityAsync(string cityName);

		Task<List<RainfallResponseDto>> GetRainfallByCityAsync(string cityName, DateOnly? startDate = null, DateOnly? endDate = null);
	}
}
