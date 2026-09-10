namespace TaiwanAgri.Modules.Weather.Dtos.ApiResponses
{
	/// <summary>
	/// 手動觸發一次評估的結果。
	///
	/// <para>
	/// 這份回應存在的理由是「沒有新通知」有三種成因，而使用者需要分得出來：
	/// 條件沒有命中（資料是新的，只是沒有一筆超過門檻）、
	/// 資料不夠新（同步 Worker 沒在跑，最新的觀測是好幾天前的）、
	/// 以及上次檢查之後根本沒有新的觀測落地（掃描範圍是空的，門檻怎麼改都是 0 則）。
	/// 少了 <see cref="LatestObservedAt"/>、<see cref="HasFreshObservation"/> 與
	/// <see cref="NumericRulesWithNewObservations"/>，三種情況在畫面上長得一模一樣，
	/// 使用者只能猜是不是壞了。
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

		/// <summary>
		/// 實際跑過比對的數值型規則數。缺門檻或來源不合法而被跳過的不算在內。
		/// </summary>
		public int NumericRulesEvaluated { get; set; }

		/// <summary>
		/// 上述規則裡，這一輪真的有新觀測可以比對的規則數。
		/// 數值型規則只看「上次檢查之後才落地」的觀測，所以連續按兩次檢查、
		/// 或改完條件立刻檢查時，這個數字會是 0——不是條件沒命中，是掃描範圍本來就空的。
		/// 兩個數字相減即可判斷要不要改口說「還沒有新的觀測進來」。
		/// </summary>
		public int NumericRulesWithNewObservations { get; set; }
	}
}
