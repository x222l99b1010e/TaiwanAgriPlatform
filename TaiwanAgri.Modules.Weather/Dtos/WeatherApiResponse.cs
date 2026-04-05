using System.Text.Json.Serialization;

namespace TaiwanAgri.Modules.Weather.Dtos
{
	// 對應最外層 {"RS":"OK","Data":[...]}
	public class WeatherApiResponse
	{
		[JsonPropertyName("RS")]
		public string RS { get; set; } = string.Empty;

		[JsonPropertyName("Data")]
		public List<WeatherStationDto> Data { get; set; } = new();

		[JsonPropertyName("Next")]
		public bool Next { get; set; }
	}	
}
