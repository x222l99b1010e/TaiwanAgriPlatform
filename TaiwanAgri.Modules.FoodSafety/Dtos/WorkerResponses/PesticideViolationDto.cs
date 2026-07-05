using System.Text.Json.Serialization;

namespace TaiwanAgri.Modules.FoodSafety.Dtos.WorkerResponses
{
	public class PesticideViolationDto
	{
		[JsonPropertyName("Number")]
		public string Number { get; set; } = string.Empty;

		[JsonPropertyName("SamplingDate")]
		public string SamplingDate { get; set; } = string.Empty;

		[JsonPropertyName("ProductName")]
		public string ProductName { get; set; } = string.Empty;

		[JsonPropertyName("ProductID")]
		public string ProductId { get; set; } = string.Empty;

		[JsonPropertyName("ProducerName")]
		public string ProducerName { get; set; } = string.Empty;

		[JsonPropertyName("SamplingLocation")]
		public string SamplingLocation { get; set; } = string.Empty;

		[JsonPropertyName("InspectResult")]
		public string InspectResult { get; set; } = string.Empty;

		[JsonPropertyName("Note")]
		public string Note { get; set; } = string.Empty;
	}
}