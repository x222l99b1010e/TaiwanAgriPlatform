namespace TaiwanAgri.Modules.Market.Dtos.ApiResponses
{
	/// <summary>
	/// 家禽行情單一資料點（長表一列對應一個）。
	/// 刻意連 Price 為 null 的列也一併回傳，不在查詢層過濾：
	/// 非數值價格佔全歷史 14%（紅羽南區兩條線超過三分之一是「未報價」、
	/// 雞蛋產地價 94% 是空值），濾掉的話前端只會看到莫名其妙的缺口，
	/// 無法分辨「這個指標本來就很少報價」與「同步壞掉了」。
	/// 要不要顯示、每種狀態怎麼畫，是前端的決定；查詢層只負責忠實搬運事實。
	/// </summary>
	public class PoultryResponseDto
	{
		public DateOnly TransDate { get; set; }

		/// <summary>指標代碼，對照表見 Constants/PoultryMetrics.cs</summary>
		public string MetricCode { get; set; } = string.Empty;

		/// <summary>中文顯示名，由 PoultryMetrics.DisplayNames 帶出，前端不需自備對照表</summary>
		public string DisplayName { get; set; } = string.Empty;

		/// <summary>PriceStatus 非 Normal 時為 null</summary>
		public decimal? Price { get; set; }

		/// <summary>
		/// 價格狀態字串（Normal／Empty／Closed／NotQuoted／Negotiated／RangeQuote／Unrecognized）。
		/// 回傳字串而非數字：比照模組 3 既有慣例（所有 enum 欄位 API 回傳都是字串）
		/// </summary>
		public string PriceStatus { get; set; } = string.Empty;

		/// <summary>原始價格字串；Normal 時為 null。RangeQuote 的區間值靠這欄呈現</summary>
		public string? RawValue { get; set; }
	}
}
