using System.Text.Json.Serialization;

namespace TaiwanAgri.Modules.Market.Dtos.WorkerResponses
{
	/// <summary>
	/// PoultryTransType_Goose_Duck_Duckegg（肉鵝／番鴨／鴨蛋）單日回應。
	/// 原始 key 的 WR＝White Roman（白羅曼鵝）、M＝Male（正番鴨公）、
	/// 75D＝75 Days（土番鴨 75 天）、TNN＝台南（鴨蛋新蛋產地）。
	/// 「正番鴨公」長年為「休市」（單一欄位就佔全部休市紀錄 2496 次），屬來源常態非異常
	/// </summary>
	public class PoultryGooseDuckDuckeggDto
	{
		[JsonPropertyName("TransDate")]
		public string TransDate { get; set; } = string.Empty;

		[JsonPropertyName("Goose_WR_TaijinPrice")]
		public string? GooseWhiteRoman { get; set; }

		[JsonPropertyName("Duck_M_TaijinPrice")]
		public string? DuckMale { get; set; }

		[JsonPropertyName("Duck_75D_TaijinPrice")]
		public string? Duck75Days { get; set; }

		[JsonPropertyName("Duckegg_TNN_TaijinPrice")]
		public string? DuckeggTainan { get; set; }
	}
}
