using System.Text.Json.Serialization;

namespace TaiwanAgri.Modules.Weather.Dtos.WorkerResponses
{
	public class PestAlertDto
	{
		[JsonPropertyName("Subject")]
		public string Subject { get; set; } = string.Empty;
		[JsonPropertyName("Body")]
		public string Body { get; set; } = string.Empty;
		[JsonPropertyName("Prescription")]
		public string Prescription { get; set; } = string.Empty;
		[JsonPropertyName("City")]
		public string City { get; set; } = string.Empty;
		[JsonPropertyName("PlantName")]
		public string PlantName { get; set; } = string.Empty;
		[JsonPropertyName("PubDate")]
		public string PubDate { get; set; } = string.Empty;
		[JsonPropertyName("Issue")]
		public string Issue	{ get; set; } = string.Empty;


	}
}
