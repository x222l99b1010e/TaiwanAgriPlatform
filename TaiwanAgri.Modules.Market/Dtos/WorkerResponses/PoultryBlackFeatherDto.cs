using System.Text.Json.Serialization;

namespace TaiwanAgri.Modules.Market.Dtos.WorkerResponses
{
	/// <summary>
	/// PoultryTransType_BlackFeather（黑羽土雞）單日回應：只有舍飼南區公/母兩個價格，
	/// 是四支來源中欄位最少的一支——這也正是長表設計的動機之一：
	/// 四支欄位數 5/6/2/4 各不相同，寬表得為最寬的那支開欄位，其餘來源大量留空
	/// </summary>
	public class PoultryBlackFeatherDto
	{
		[JsonPropertyName("TransDate")]
		public string TransDate { get; set; } = string.Empty;

		[JsonPropertyName("BlackFeather_S_M")]
		public string? SouthMale { get; set; }

		[JsonPropertyName("BlackFeather_S_F")]
		public string? SouthFemale { get; set; }
	}
}
