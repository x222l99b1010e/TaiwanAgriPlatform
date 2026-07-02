using System.Text.Json.Serialization;

namespace TaiwanAgri.Modules.FoodSafety.Dtos.WorkerResponses
{
	public class PesticideViolationApiResponse
	{
		[JsonPropertyName("RS")]
		public string RS { get; set; } = string.Empty;

		[JsonPropertyName("Data")]
		public List<PesticideViolationDto> Data { get; set; } = new();

		[JsonPropertyName("Next")]
		public bool Next { get; set; }
	}
}