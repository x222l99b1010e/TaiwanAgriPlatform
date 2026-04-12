using System.Text.Json.Serialization;

namespace TaiwanAgri.Modules.Weather.Dtos
{
	public class RainfallStationApiResponse
	{
		[JsonPropertyName("RS")]
		public string RS { get; set; } = string.Empty;
		[JsonPropertyName("Data")]
		public List<RainfallStationDto> Data { get; set; } = new();
		[JsonPropertyName("Next")]
		public bool Next { get; set; }
	}
}
