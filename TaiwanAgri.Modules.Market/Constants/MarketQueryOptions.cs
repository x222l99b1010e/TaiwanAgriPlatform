namespace TaiwanAgri.Modules.Market.Constants
{
	/// <summary>
	/// Market 模組查詢層的數量上限設定。
	/// <para>
	/// 用強型別選項而不是讓 Service／Controller 各自注入 IConfiguration 再 GetValue：
	/// 後者讓業務層依賴整個設定系統、每次請求重查一次、拼錯 key 不會有任何錯誤訊號，
	/// 而且同類的上限有的走設定、有的直接寫死在程式裡（監看清單的刪除上限原本就是寫死的）。
	/// </para>
	/// </summary>
	public class MarketQueryOptions
	{
		public const string SectionName = "MarketQueryLimits";

		/// <summary>天災查詢單次最多撈幾筆原始記錄。超過會截斷並在回應加上截斷標頭</summary>
		public int DisasterRecordLimit { get; set; } = 5000;

		/// <summary>行情查詢單次最多幾個作物代碼</summary>
		public int CropCodesMaxCount { get; set; } = 5;

		/// <summary>監看清單單次最多刪幾筆</summary>
		public int WatchlistDeleteMaxCount { get; set; } = 50;
	}
}
