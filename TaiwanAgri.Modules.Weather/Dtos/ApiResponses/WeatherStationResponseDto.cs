namespace TaiwanAgri.Modules.Weather.Dtos.ApiResponses
{
	public class WeatherStationResponseDto
	{
		public string StationName { get; set; } = string.Empty;
		public string CityName { get; set; } = string.Empty;
		public string? TownName { get; set; }
		public DateTime ObservedAt { get; set; }
		public decimal? Temperature { get; set; }
		public decimal? DailyMaxTemp { get; set; }
		public decimal? DailyMinTemp { get; set; }
		public decimal? Humidity { get; set; }
		public decimal? WindSpeed { get; set; }
		public string? WindDirection { get; set; }
		public decimal? MaxGust { get; set; }
		public decimal? Rainfall24h { get; set; }
		public decimal? SunshineHours { get; set; }
		public decimal? Pressure { get; set; }
	}
}
