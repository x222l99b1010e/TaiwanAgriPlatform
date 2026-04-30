using System.Text.Json.Serialization;

namespace TaiwanAgri.Modules.Market.Dtos
{
	public class AgriProductsTransTypeApiResponse
	{
		[JsonPropertyName("RS")]
		public string RS { get; set; } = string.Empty;
		[JsonPropertyName("Data")]
		public List<AgriProductsTransTypeDto> Data { get; set; } = new();
		[JsonPropertyName("Next")]
		public bool Next { get; set; }
	}
}
