using System.Text.Json.Serialization;

namespace TaiwanAgri.Modules.Weather.Dtos.WorkerResponses
{
	public class RainfallStationDto
	{
		[JsonPropertyName("Station_ID")]
		public string StationId { get; set; } = string.Empty;

		[JsonPropertyName("Station_name")]
		public string StationName { get; set; } = string.Empty;

		[JsonPropertyName("CITY")]
		public string CityName { get; set; } = string.Empty;

		[JsonPropertyName("CITY_SN")]
		public string CityCode { get; set; } = string.Empty;

		[JsonPropertyName("TOWN")]
		public string? TownName { get; set; }

		[JsonPropertyName("TOWN_SN")]
		public string? TownCode { get; set; }
	}
}
