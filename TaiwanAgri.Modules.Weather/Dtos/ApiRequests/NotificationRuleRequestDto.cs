using System.ComponentModel.DataAnnotations;
using TaiwanAgri.Modules.Weather.Constants;

namespace TaiwanAgri.Modules.Weather.Dtos.ApiRequests
{
	/// <summary>
	/// 建立與更新規則共用同一份請求。兩者的欄位完全相同——更新沒有多出任何欄位、
	/// 也沒有少任何欄位，拆成兩個一模一樣的類別只是把同一份規則寫兩次。
	///
	/// <para>
	/// 驗證分兩層寫在同一個地方：能用單一屬性表達的（必填、長度）掛 DataAnnotations，
	/// 取決於另一個欄位的值的（哪一組欄位必填、保留天數的上限是 180 還是 30）走
	/// <see cref="IValidatableObject"/>。兩者都由 [ApiController] 在進入動作之前收集、一次回 400，
	/// 所以 Controller 裡不需要任何驗證程式碼。
	/// 之所以不照「跨欄位規則放 Controller」的舊寫法：CRUD 有建立與更新兩支端點，
	/// 同一段規則要抄兩次，而日後新增第三支端點時漏抄的那一支不會有任何訊號——
	/// 它看起來就跟有驗證的端點一模一樣。掛在型別上則是「用了這個 DTO 就自動有驗證」。
	/// </para>
	///
	/// <para>
	/// 刻意不驗的一件事：另一個型態專用的欄位若被填了值，不回 400 而是由服務層清成 null。
	/// 那些欄位對該型態沒有任何作用，為它們回 400 只是讓契約更難用；
	/// 而清空能保證「另一組欄位恆為 null」這個不變式由伺服器維護，不靠呼叫端記得先清表單。
	/// </para>
	/// </summary>
	public class NotificationRuleRequestDto : IValidatableObject
	{
		[Required(ErrorMessage = "規則名稱必填")]
		[MaxLength(100, ErrorMessage = "規則名稱不得超過 100 個字")]
		public string RuleName { get; set; } = string.Empty;

		[Required(ErrorMessage = "規則型態必填")]
		[MaxLength(20)]
		public string RuleType { get; set; } = string.Empty;

		[Required(ErrorMessage = "資料來源必填")]
		[MaxLength(50)]
		public string SourceTable { get; set; } = string.Empty;

		/// <summary>預設啟用：使用者建立規則的意圖就是要它開始運作，沒帶值時不該是停用的</summary>
		public bool IsActive { get; set; } = true;

		/// <summary>
		/// 通知產生後幾天自動刪除。可為 null——沒帶值時由服務層依型態填入預設值
		/// （事件型 30、數值型 7）。預設值放後端而不是只放前端表單，是因為漏帶時的後果沒有訊號：
		/// 型別若是不可為 null 的 int，漏帶會變成 0，通知一產生就過期、下一輪評估直接刪掉。
		/// </summary>
		public int? ExpiryDays { get; set; }

		// ── 共用篩選條件 ──────────────────────────────────────────────

		/// <summary>縣市。兩種型態都用，比對時台／臺兩種寫法都會試，所以存哪一種都比對得到。</summary>
		[MaxLength(20)]
		public string? FilterCity { get; set; }

		// ── 事件型專用 ────────────────────────────────────────────────

		[MaxLength(50)]
		public string? FilterPlantName { get; set; }

		/// <summary>只通知這一天（含）之後發布的疫情。null 代表不限起日。</summary>
		public DateOnly? FilterDateFrom { get; set; }

		// ── 數值型專用 ────────────────────────────────────────────────

		[MaxLength(20)]
		public string? MetricName { get; set; }

		[MaxLength(20)]
		public string? Comparison { get; set; }

		public decimal? Threshold { get; set; }

		/// <summary>
		/// 取決於其他欄位的值、單一屬性表達不了的規則。
		/// 注意執行順序：屬性層級的驗證全部通過之後才會走到這裡，
		/// 所以這裡可以假設必填與長度都已經成立。
		/// </summary>
		public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
		{
			// [Required] 擋得住 null 與空字串，擋不住 "   "——那會建出一條在列表上看不見名字的規則
			if (RuleName.Length > 0 && string.IsNullOrWhiteSpace(RuleName))
				yield return new ValidationResult("規則名稱不能只有空白", [nameof(RuleName)]);

			if (!NotificationRule.Types.All.Contains(RuleType))
			{
				yield return new ValidationResult(
					$"規則型態只接受 {string.Join("、", NotificationRule.Types.All)}", [nameof(RuleType)]);

				// 底下每一條規則都要先知道型態是哪一種，型態不對就無從判斷，
				// 繼續往下只會產生一串以錯誤前提推出來的訊息
				yield break;
			}

			if (!NotificationRule.SourceTables.All.Contains(SourceTable))
			{
				yield return new ValidationResult(
					$"資料來源只接受 {string.Join("、", NotificationRule.SourceTables.All)}", [nameof(SourceTable)]);
			}
			else if (SourceTable != NotificationRule.SourceTables.For(RuleType))
			{
				// 值本身合法、但配錯型態。不擋的話規則建得起來，引擎每輪跑到它就跳過，
				// 使用者永遠收不到通知也看不到任何錯誤
				yield return new ValidationResult(
					$"規則型態 {RuleType} 的資料來源必須是 {NotificationRule.SourceTables.For(RuleType)}",
					[nameof(SourceTable)]);
			}

			if (ExpiryDays is { } expiryDays)
			{
				var maxExpiryDays = NotificationRule.Limits.MaxExpiryDays(RuleType);
				if (expiryDays < NotificationRule.Limits.MinExpiryDays || expiryDays > maxExpiryDays)
					yield return new ValidationResult(
						$"通知保留天數必須介於 {NotificationRule.Limits.MinExpiryDays} 與 {maxExpiryDays} 天之間",
						[nameof(ExpiryDays)]);
			}

			if (RuleType != NotificationRule.Types.Numeric)
				yield break;

			if (string.IsNullOrEmpty(MetricName) || !NotificationRule.Metrics.All.Contains(MetricName))
				yield return new ValidationResult(
					$"數值門檻規則的觀測項目只接受 {string.Join("、", NotificationRule.Metrics.All)}",
					[nameof(MetricName)]);

			if (string.IsNullOrEmpty(Comparison) || !NotificationRule.Comparisons.All.Contains(Comparison))
				yield return new ValidationResult(
					$"數值門檻規則的比較方向只接受 {string.Join("、", NotificationRule.Comparisons.All)}",
					[nameof(Comparison)]);

			// 門檻的三條規則寫在一起：少了它，引擎會因為 Threshold 為 null 而跳過整條規則
			if (Threshold is not { } threshold)
			{
				yield return new ValidationResult("數值門檻規則必須填門檻值", [nameof(Threshold)]);
			}
			else if (threshold < NotificationRule.Limits.ThresholdMin || threshold > NotificationRule.Limits.ThresholdMax)
			{
				yield return new ValidationResult(
					$"門檻值必須介於 {NotificationRule.Limits.ThresholdMin} 與 {NotificationRule.Limits.ThresholdMax} 之間",
					[nameof(Threshold)]);
			}
			else if (decimal.Round(threshold, NotificationRule.Limits.ThresholdDecimals) != threshold)
			{
				yield return new ValidationResult(
					$"門檻值只收到小數點後 {NotificationRule.Limits.ThresholdDecimals} 位", [nameof(Threshold)]);
			}
		}
	}
}
