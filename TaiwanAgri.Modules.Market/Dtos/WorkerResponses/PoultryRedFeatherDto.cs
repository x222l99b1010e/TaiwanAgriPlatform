using System.Text.Json.Serialization;

namespace TaiwanAgri.Modules.Market.Dtos.WorkerResponses
{
	/// <summary>
	/// PoultryTransType_RedFeather（紅羽土雞）單日回應：北/中/南 × 公/母 共 6 個價格。
	/// 原始 key 的 N/C/S＝North/Central/South，M/F＝Male/Female。
	/// 這支是四支中非數值寫法最多的來源（「-」未報價 3353 次、區間報價 70 次皆出於此）
	/// </summary>
	public class PoultryRedFeatherDto
	{
		[JsonPropertyName("TransDate")]
		public string TransDate { get; set; } = string.Empty;

		[JsonPropertyName("RedFeather_N_M")]
		public string? NorthMale { get; set; }

		[JsonPropertyName("RedFeather_N_F")]
		public string? NorthFemale { get; set; }

		[JsonPropertyName("RedFeather_C_M")]
		public string? CentralMale { get; set; }

		[JsonPropertyName("RedFeather_C_F")]
		public string? CentralFemale { get; set; }

		[JsonPropertyName("RedFeather_S_M")]
		public string? SouthMale { get; set; }

		[JsonPropertyName("RedFeather_S_F")]
		public string? SouthFemale { get; set; }
	}
}
