namespace TaiwanAgri.Modules.Weather.Dtos.ApiResponses
{
	/// <summary>
	/// 一條通知規則。
	/// 不含 UserId——公開 API 不外露內部使用者識別碼，而且這支端點回的本來就只有自己的規則。
	/// 也不含評估水位：那是系統為了知道「哪些資料我看過了」而記的落地時刻，
	/// 使用者無從驗證它對不對，給了他只會多一個看不懂的欄位。
	/// </summary>
	public class NotificationRuleResponseDto
	{
		public int Id { get; set; }
		public string RuleName { get; set; } = string.Empty;
		public string RuleType { get; set; } = string.Empty;
		public string SourceTable { get; set; } = string.Empty;
		public bool IsActive { get; set; }
		public int ExpiryDays { get; set; }
		public DateTime CreatedAt { get; set; }

		public string? FilterCity { get; set; }
		public string? FilterPlantName { get; set; }
		public DateOnly? FilterDateFrom { get; set; }

		public string? MetricName { get; set; }
		public string? Comparison { get; set; }
		public decimal? Threshold { get; set; }

		/// <summary>
		/// 這條規則已經產生了幾則通知。刪除規則會由資料庫的外鍵連帶刪除它們，
		/// 所以刪除前的確認對話框要講得出「會一併刪掉幾則」——
		/// 使用者知道後果才有辦法決定要不要刪。
		/// 隨列表一起回而不另開一支端點：一個使用者最多七條規則，多這一個計數子查詢的成本可以忽略，
		/// 而少一次往返代表確認對話框不會有「正在計算」的中間狀態。
		/// </summary>
		public int NotificationCount { get; set; }
	}
}
