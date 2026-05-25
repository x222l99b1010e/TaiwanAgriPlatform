using System.Text.Json.Serialization;

namespace TaiwanAgri.Modules.Weather.Dtos.WorkerResponses
{
	public class PestDecadeSummaryDto
	{
		[JsonPropertyName("PestName")]
		public string PestName { get; set; }
		[JsonPropertyName("Year")]
		public string Year { get; set; }
		[JsonPropertyName("Month")]

		public string Month { get; set; }
		[JsonPropertyName("TenDays")]
		public string Decade { get; set; }
		[JsonPropertyName("City")]
		public string City { get; set; }
		[JsonPropertyName("Town")]
		public string Town { get; set; }
		[JsonPropertyName("Average")]
		public string Average { get; set; }
		[JsonPropertyName("Proportion_Island")]
		public string ProportionIsland { get; set; }
	}
}