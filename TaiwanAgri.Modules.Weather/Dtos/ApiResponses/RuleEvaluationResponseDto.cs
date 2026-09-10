namespace TaiwanAgri.Modules.Weather.Dtos.ApiResponses
{
	/// <summary>
	/// 手動觸發一次評估的結果。
	///
	/// <para>
	/// 這份回應存在的理由是「沒有新通知」有兩種成因，而使用者需要分得出來：
	/// 條件沒有命中（資料是新的，只是沒有一筆超過門檻），
	/// 或是資料不夠新（同步 Worker 沒在跑，最新的觀測是好幾天前的）。
	/// 少了 <see cref="LatestObservedAt"/> 與 <see cref="HasFreshObservation"/>，
	/// 兩種情況在畫面上長得一模一樣，使用者只能猜是不是壞了。
	/// </para>
	/// </summary>
	public class RuleEvaluationResponseDto
	{
		/// <summary>實際被評估的規則數。停用中的規則不算在內。</summary>
		public int RulesEvaluated { get; set; }

		/// <summary>這一次評估新產生的通知則數</summary>
		public int NotificationsCreated { get; set; }

		/// <summary>
		/// 氣象觀測表裡最新一筆的觀測時刻；null 代表表裡沒有任何資料。
		/// 用觀測時刻而不是落地時刻：前者是使用者在通知訊息上看得到、也對得起來的那個時間。
		/// </summary>
		public DateTime? LatestObservedAt { get; set; }

		/// <summary>
		/// 最新的觀測是否落在新鮮度門檻之內。false 代表數值型規則這一輪不可能產生通知，
		/// 不論門檻設多少。
		/// 由後端回答而不是讓前端拿 <see cref="LatestObservedAt"/> 自己算，
		/// 是因為門檻天數只寫在引擎裡；前端要自己算就得再抄一份，而兩份遲早會不一樣。
		/// </summary>
		public bool HasFreshObservation { get; set; }
	}
}
