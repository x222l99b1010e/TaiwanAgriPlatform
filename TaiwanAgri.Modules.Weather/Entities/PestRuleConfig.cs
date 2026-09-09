using System.ComponentModel.DataAnnotations;

namespace TaiwanAgri.Modules.Weather.Entities
{
	/// <summary>
	/// 使用者自訂的通知規則。欄位分成三組：共用、事件型專用、數值型專用——
	/// 由 RuleType 決定哪一組有意義，另一組維持 null。
	/// 兩種型態的篩選條件不同，是因為兩個資料源的形狀不同（事件型比疫情警報的縣市與作物，
	/// 數值型比氣象觀測的量測值），硬塞進同一組欄位只會讓每個欄位都要附帶「這個型態才有用」的說明。
	/// 命名注意：Pest 前綴是歷史命名，數值型的來源已改為自動氣象站觀測，
	/// 判斷規則實際涵蓋範圍請看 RuleType 與 SourceTable，不要依前綴推論。
	/// </summary>
	public class PestRuleConfig
	{
		[Key]
		public int Id { get; set; } //PK
		[Required, MaxLength(450)]
		public string UserId { get; set; } = string.Empty; //FK → AspNetUsers
		[Required, MaxLength(100)]
		public string RuleName { get; set; } = string.Empty; //使用者自訂規則名稱
		[Required, MaxLength(20)]
		public string RuleType { get; set; } = string.Empty; //"Numeric" / "Event"
		[Required, MaxLength(50)]
		public string SourceTable { get; set; } = string.Empty; //"PlantEpidemic"（事件型）/ "WeatherObservation"（數值型）
		public bool IsActive { get; set; } //規則是否啟用；只有啟用中的規則會被引擎評估
		public int ExpiryDays { get; set; } //通知產生後幾天自動刪除；兩種型態的預設值與上限不同
		public DateTime CreatedAt { get; set; } //規則建立時間

		// ── 共用篩選條件 ──────────────────────────────────────────────
		/// <summary>
		/// 縣市。兩種型態都用，但比對對象不同（事件型比 PestAlertCity.CityName、
		/// 數值型比 WeatherObservation.CityName）。
		/// 注意：兩張來源表的用字不同（疫情側是「台」、氣象側是「臺」），
		/// 比對時兩種寫法都要試，否則會靜默比不到任何一筆。
		/// </summary>
		[MaxLength(20)]
		public string? FilterCity { get; set; }

		// ── 事件型專用 ────────────────────────────────────────────────
		[MaxLength(50)]
		public string? FilterPlantName { get; set; } //作物名稱，比 PestAlertCrop.CropName
		/// <summary>
		/// 只通知這一天（含）之後發布的疫情。存日期不存天數：表單是日期選擇器、
		/// 引擎比 PestAlert.PubDate，三層之間沒有換算，使用者也能拿通知上的發布日自己驗證。
		/// 只有起日沒有迄日——「到某天就不再通知」用 IsActive 停用規則表達，
		/// 同一個意圖有兩種表達方式是日後行為對不起來的來源。
		/// </summary>
		public DateOnly? FilterDateFrom { get; set; }

		// ── 數值型專用 ────────────────────────────────────────────────
		[MaxLength(20)]
		public string? MetricName { get; set; } //要比對的觀測項目，對應 WeatherObservation 的屬性名："Temperature" / "Rainfall24h"
		[MaxLength(20)]
		public string? Comparison { get; set; } //比較方向："GreaterThan" / "LessThan"；低溫寒害需要後者
		[Range(-999.9, 999.9)]
		public decimal? Threshold { get; set; } //門檻值，收到小數點後一位（氣溫 32.5、雨量 100.0）
		/// <summary>
		/// 上次評估到哪個落地時刻為止（水位）。每次評估只看比它更晚落地的觀測，
		/// 評估完往前推。沒有它的話，一條新規則第一次評估會把整張表的歷史一次全變成通知
		/// ——去重擋得住重複，擋不住第一次。
		/// 比 SyncedAt（落地時刻）而不是 ObservedAt（觀測時刻）：上游若補傳一筆觀測時刻較舊、
		/// 今天才落地的資料，用觀測時刻當水位會直接漏掉它。
		/// null 代表尚未評估過，第一次評估時設為「表裡最新落地時刻減 15 分鐘」。
		/// </summary>
		public DateTime? LastEvaluatedAt { get; set; }
	}
}
