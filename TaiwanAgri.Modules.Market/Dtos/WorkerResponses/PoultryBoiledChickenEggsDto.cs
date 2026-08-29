using System.Text.Json.Serialization;

namespace TaiwanAgri.Modules.Market.Dtos.WorkerResponses
{
	/// <summary>
	/// PoultryTransType_BoiledChicken_Eggs（白肉雞／雞蛋）單日回應。
	/// 價格欄位一律宣告為 string 而非 decimal?：來源實測會出現空字串、「休市」、
	/// 「議價」等非數值寫法，若宣告成數值型別，System.Text.Json 反序列化當場就會拋例外，
	/// 連進入容錯邏輯的機會都沒有——先原樣接成字串，再交給 PoultryPriceParser 分類。
	/// LunarCalendar（農曆）刻意不接：不落地，前端也不需要
	/// </summary>
	public class PoultryBoiledChickenEggsDto
	{
		[JsonPropertyName("TransDate")]
		public string TransDate { get; set; } = string.Empty;

		[JsonPropertyName("TaijinPrice_2.0kgup")]
		public string? BoiledChicken2_0KgUp { get; set; }

		[JsonPropertyName("TaijinPrice_1.75kg_1.95kg")]
		public string? BoiledChicken1_75To1_95Kg { get; set; }

		[JsonPropertyName("Store_KP_TaijinPrice")]
		public string? StoreKaoPing { get; set; }

		[JsonPropertyName("egg_Price")]
		public string? EggTransport { get; set; }

		[JsonPropertyName("egg_Producer_Price")]
		public string? EggProducer { get; set; }
	}
}
