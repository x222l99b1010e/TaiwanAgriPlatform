using System.Text.Json.Serialization;

namespace TaiwanAgri.Modules.FoodSafety.Dtos.ExternalResponses
{
	public class AgriProducerApiResponse
	{
		[JsonPropertyName("RS")] public string RS { get; set; } = string.Empty;
		[JsonPropertyName("Data")] public List<AgriProducerDto> Data { get; set; } = new();
		[JsonPropertyName("Next")] public bool Next { get; set; }
	}

	public class AgriProducerDto
	{
		[JsonPropertyName("TraceCode")] public string TraceCode { get; set; } = string.Empty;
		[JsonPropertyName("Producer")] public string Producer { get; set; } = string.Empty;
		[JsonPropertyName("Address")] public string Address { get; set; } = string.Empty;
		[JsonPropertyName("Mark")] public string Mark { get; set; } = string.Empty;
		[JsonPropertyName("Url")] public string Url { get; set; } = string.Empty;
		[JsonPropertyName("Description")] public string Description { get; set; } = string.Empty;
		[JsonPropertyName("Status")] public string Status { get; set; } = string.Empty;
		[JsonPropertyName("ModifyDate")] public string ModifyDate { get; set; } = string.Empty;
	}
}