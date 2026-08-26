using System.ComponentModel.DataAnnotations;
using TaiwanAgri.Modules.Market.Entities.Enums;

namespace TaiwanAgri.Modules.Market.Entities
{
	/// <summary>
	/// 家禽交易行情實體 (Poultry Transaction Market Data)。
	/// 長表設計：一列＝某天某一個指標(MetricCode)的一筆價格，而非 PorkTrans 那種
	/// 每個品項各佔一欄的寬表——四支來源 API 欄位集不同，長表讓日後加第五支 API
	/// 不需要異動 Schema，只需在 Constants/PoultryMetrics.cs 增加對應的 MetricCode。
	/// </summary>
	public class PoultryTrans
	{
		[Key]
		public int Id { get; set; }

		/// <summary>
		/// 交易日期 (TransDate)
		/// </summary>
		public DateOnly TransDate { get; set; }

		/// <summary>
		/// 指標代碼，對應 Constants/PoultryMetrics.cs 的常數（如 BoiledChicken_2_0KgUp）。
		/// 單一真相來源在 PoultryMetrics.cs，這裡只負責存值。
		/// </summary>
		[Required]
		[MaxLength(50)]
		public string MetricCode { get; set; } = string.Empty;

		/// <summary>
		/// 解析後的價格；PriceStatus 非 Normal 時一律為 null
		/// </summary>
		public decimal? Price { get; set; }

		/// <summary>
		/// 原始價格字串的解析結果分類（見 PriceStatus 說明）
		/// </summary>
		public PriceStatus PriceStatus { get; set; }

		/// <summary>
		/// 原始價格字串。不變式：PriceStatus = Normal 時為 null，其餘狀態一律保留原文。
		/// 這樣任何解析不出數字的值都不會遺失——RangeQuote 的「41-42」本身帶有資訊，
		/// Unrecognized 的未知寫法則要靠它判斷該不該收編成新狀態
		/// </summary>
		[MaxLength(50)]
		public string? RawValue { get; set; }

		/// <summary>
		/// 系統紀錄：這筆資料寫入的時間
		/// </summary>
		public DateTime SyncedAt { get; set; } = DateTime.UtcNow;
	}
}
