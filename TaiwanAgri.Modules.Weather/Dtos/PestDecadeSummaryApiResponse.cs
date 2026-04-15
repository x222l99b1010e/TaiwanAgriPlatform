using System.Text.Json.Serialization;

namespace TaiwanAgri.Modules.Weather.Dtos
{
	public class PestDecadeSummaryApiResponse
	{
		[JsonPropertyName("RS")]
		public string RS { get; set; } = string.Empty;
		[JsonPropertyName("Data")]
		public List<PestDecadeSummaryDto> Data { get; set; } = new();
		[JsonPropertyName("Next")]
		public bool Next { get; set; }
	}
}
