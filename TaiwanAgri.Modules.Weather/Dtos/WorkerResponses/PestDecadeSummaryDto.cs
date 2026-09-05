using System.Text.Json.Serialization;

namespace TaiwanAgri.Modules.Weather.Dtos.WorkerResponses
{
	/// <summary>
	/// 農業部旬報 API 的原始回應。所有欄位宣告為可空是刻意的——來源是外部 JSON，
	/// 任一欄都可能缺漏或為空字串，由 MapToEntity 負責驗證後才轉成 entity
	/// </summary>
	public class PestDecadeSummaryDto
	{
		[JsonPropertyName("PestName")]
		public string? PestName { get; set; }
		[JsonPropertyName("Year")]
		public string? Year { get; set; }
		[JsonPropertyName("Month")]

		public string? Month { get; set; }
		[JsonPropertyName("TenDays")]
		public string? Decade { get; set; }
		[JsonPropertyName("City")]
		public string? City { get; set; }
		[JsonPropertyName("Town")]
		public string? Town { get; set; }
		[JsonPropertyName("Average")]
		public string? Average { get; set; }
		[JsonPropertyName("Proportion_Island")]
		public string? ProportionIsland { get; set; }
	}
}