using TaiwanAgri.Modules.Weather.Data;
using TaiwanAgri.Modules.Weather.Dtos.ApiResponses;
using Microsoft.EntityFrameworkCore;


namespace TaiwanAgri.Modules.Weather.Services
{
	public class WeatherService : IWeatherService
	{
		private readonly WeatherDbContext _context;
		public WeatherService(WeatherDbContext context)
		{
			_context = context;
		}
		public async Task<List<RainfallResponseDto>> GetRainfallByCityAsync(string cityName, DateOnly? startDate = null, DateOnly? endDate = null)
		{
			DateOnly finalStart = startDate ?? DateOnly.FromDateTime(DateTime.Now.AddDays(-14));
			DateOnly finalEnd = endDate ?? DateOnly.FromDateTime(DateTime.Now);

			var result = await _context.RainfallObservations
				.Join(_context.RainfallStations,
					obs => obs.StationId,
					sta => sta.StationId,
					(obs, sta) => new { obs, sta })
				.Where(x => x.sta.CityName == cityName
						 && DateOnly.FromDateTime(x.obs.ObservedAt) >= finalStart
						 && DateOnly.FromDateTime(x.obs.ObservedAt) <= finalEnd)
				.Select(x => new RainfallResponseDto
				{
					StationName = x.sta.StationName,
					CityName = x.sta.CityName,
					ObservedAt = x.obs.ObservedAt,
					Hour3 = x.obs.Hour3,
					Hour6 = x.obs.Hour6,
					Hour12 = x.obs.Hour12,
					Hour24 = x.obs.Hour24
				})
				.ToListAsync();

			return result;
		}

		public async Task<List<WeatherStationResponseDto>> GetStationsByCityAsync(string cityName)
		{
			var raw = await _context.WeatherObservations
				.Where(s => s.CityName == cityName)
				.ToListAsync();

			var result = raw
				.GroupBy(s => s.StationId)
				.Select(g => g.OrderByDescending(w => w.ObservedAt).First())
				.Select(s => new WeatherStationResponseDto
				{
					StationName = s.StationName,
					CityName = s.CityName,
					TownName = s.TownName,
					ObservedAt = s.ObservedAt,
					Temperature = s.Temperature,
					DailyMaxTemp = s.DailyMaxTemp,
					DailyMinTemp = s.DailyMinTemp,
					Humidity = s.Humidity,
					WindSpeed = s.WindSpeed,
					WindDirection = s.WindDirection,
					MaxGust = s.MaxGust,
					Rainfall24h = s.Rainfall24h,
					SunshineHours = s.SunshineHours,
					Pressure = s.Pressure
				})
				.ToList();
			return result;
		}
	}
}
