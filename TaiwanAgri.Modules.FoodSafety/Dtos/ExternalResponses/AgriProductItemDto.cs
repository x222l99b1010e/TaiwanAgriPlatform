using System.Text.Json.Serialization;

namespace TaiwanAgri.Modules.FoodSafety.Dtos.ExternalResponses
{
	public class AgriProductApiResponse
	{
		[JsonPropertyName("RS")] public string RS { get; set; } = string.Empty;
		[JsonPropertyName("Data")] public List<AgriProductItemDto> Data { get; set; } = new();
		[JsonPropertyName("Next")] public bool Next { get; set; }
	}

	public class AgriProductItemDto
	{
		[JsonPropertyName("TraceCode")] public string TraceCode { get; set; } = string.Empty;
		[JsonPropertyName("Product")] public string Product { get; set; } = string.Empty;
		[JsonPropertyName("Place")] public string Place { get; set; } = string.Empty;
		[JsonPropertyName("Mark")] public string Mark { get; set; } = string.Empty;
	}
}