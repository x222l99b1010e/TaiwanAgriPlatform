using System.Text.Json.Serialization;

namespace TaiwanAgri.Modules.Market.Dtos.WorkerResponses
{
	public class CropMarketTypeApiResponse
	{
		[JsonPropertyName("RS")]
		public string RS { get; set; } = string.Empty;
		[JsonPropertyName("Data")]
		public List<CropMarketTypeDto> Data { get; set; } = new();
		[JsonPropertyName("Next")]
		public bool Next { get; set; }
	}
}
