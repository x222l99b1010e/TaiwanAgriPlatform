namespace TaiwanAgri.Modules.Weather.Constants
{
	/// <summary>
	/// 通知規則四個字串欄位的合法值，以及規則數與通知壽命的界限。
	///
	/// 為什麼收成常數而不是各處寫字面字串：同一組值同時被 API 的請求驗證與規則引擎的分支判斷使用，
	/// 兩邊各寫一份時，改了一邊而漏掉另一邊的後果是「規則建得出來、卻永遠不觸發」——
	/// 引擎認不得那個值就記一行 log 跳過，使用者看到的是通知一直沒來，沒有任何錯誤訊號。
	///
	/// 兩個曾經合法、現已排除的來源，理由完全不同，不要合併理解：
	///   "PestDecade"（病蟲害旬報）——要比對的旬平均值上游未提供值，實測我方 136 筆與上游單頁
	///     500 筆全部為 null，而 SQL 裡 NULL 與任何數字比較的結果都不成立，任何門檻都回 0 筆。
	///   "TreePest"（樹木病蟲害）——沒有時間戳（無法判斷「新出現」）也沒有唯一識別欄位
	///     （無法去重），且語意是歷史診斷案例而非即時警報。
	/// 兩者在建立規則時都回 400；引擎仍各留一個跳過分支，處理資料庫裡可能存在的歷史列。
	/// </summary>
	public static class NotificationRule
	{
		/// <summary>規則型態。決定哪一組篩選欄位有意義，以及比對哪一個資料源。</summary>
		public static class Types
		{
			/// <summary>數值門檻：比對自動氣象站觀測的量測值</summary>
			public const string Numeric = "Numeric";

			/// <summary>事件：比對植物疫情警報</summary>
			public const string Event = "Event";

			public static readonly string[] All = [Numeric, Event];
		}

		/// <summary>
		/// 資料來源。與 <see cref="Types"/> 是一對一的，之所以仍然是獨立欄位而不是由型態推導，
		/// 是因為它是資料庫裡的既有欄位、且引擎用它做深度防禦（繞過 API 直接寫進來的值要擋得住）。
		/// </summary>
		public static class SourceTables
		{
			/// <summary>植物疫情警報，事件型專用</summary>
			public const string PlantEpidemic = "PlantEpidemic";

			/// <summary>自動氣象站觀測，數值型專用</summary>
			public const string WeatherObservation = "WeatherObservation";

			public static readonly string[] All = [PlantEpidemic, WeatherObservation];

			/// <summary>
			/// 該型態唯一合法的來源。呼叫前 ruleType 必須已經驗過是合法值——
			/// 未知型態會拿到事件型的答案，而那個答案沒有意義。
			/// </summary>
			public static string For(string ruleType)
				=> ruleType == Types.Numeric ? WeatherObservation : PlantEpidemic;
		}

		/// <summary>
		/// 數值型要比對的觀測項目。值直接對應 WeatherObservation 的屬性名，
		/// 引擎的 switch 與這裡的驗證因此看得出是同一件事。
		/// 只開兩項而不是把該表的九個數值欄位全開：多開一項就要多一組單位、多一組訊息文字與測試，
		/// 而氣溫與 24 小時雨量已經涵蓋農業情境真正會設門檻的兩件事（高低溫與豪雨）。
		/// </summary>
		public static class Metrics
		{
			public const string Temperature = "Temperature";
			public const string Rainfall24h = "Rainfall24h";

			public static readonly string[] All = [Temperature, Rainfall24h];
		}

		/// <summary>
		/// 門檻的比較方向。用文字不用符號，比照專案既有的列舉一律以字串進出 API 的慣例。
		/// 兩個方向都要有：高溫與豪雨用 GreaterThan，低溫寒害用 LessThan。
		/// </summary>
		public static class Comparisons
		{
			public const string GreaterThan = "GreaterThan";
			public const string LessThan = "LessThan";

			public static readonly string[] All = [GreaterThan, LessThan];
		}

		/// <summary>規則數與通知壽命的界限。</summary>
		public static class Limits
		{
			/// <summary>
			/// 一個使用者能擁有的規則數，算總數而不是算啟用中的數量。
			/// 推導：3–4 種作物 × 1 個產地的疫情規則，加上氣溫與雨量兩條氣象規則，約 5–6 條，取 7 留一格餘裕。
			/// 算總數的理由有兩條：一個數字比「啟用中 7 / 總數 20」兩個數字好解釋，
			/// 而且只算啟用中的話，使用者可以建無限多條停用的規則，儲存就沒有上界了。
			/// </summary>
			public const int MaxRulesPerUser = 7;

			/// <summary>通知保留天數的下限。0 代表通知一產生就過期，下一輪評估會立刻把它刪掉。</summary>
			public const int MinExpiryDays = 1;

			/// <summary>事件型的通知保留天數預設值</summary>
			public const int EventDefaultExpiryDays = 30;

			/// <summary>
			/// 事件型的通知保留天數上限。
			/// 硬性理由是溢位：ExpireAt 由 DateTime.AddDays 算出，超過 DateTime.MaxValue 會拋例外，
			/// 而那個例外會離開整支評估方法——一個使用者填的數字會讓當天全系統所有人的規則都不被評估。
			/// 取 180 而不是一個寬鬆到不必辯護的大數，是因為半年是疫情防治建議行動價值的量級。
			/// </summary>
			public const int EventMaxExpiryDays = 180;

			/// <summary>
			/// 數值型的通知保留天數預設值。一則「臺中市 32 度」的通知三天後就沒有行動價值了，
			/// 所以比事件型短。
			/// </summary>
			public const int NumericDefaultExpiryDays = 7;

			/// <summary>
			/// 數值型的通知保留天數上限。硬性理由同事件型（溢位）；取 30 是「再往上就明顯不合理」的位置。
			/// 不壓到 10 是因為預設值不該貼著上限——那樣等於用建議用法限制使用者，
			/// 而且會擋掉「兩週才看一次通知」這個合理用法。
			/// 注意：這個 30 與氣象觀測資料本身保留 30 天是兩件無關的事，改動其中一個時不必一起改。
			/// </summary>
			public const int NumericMaxExpiryDays = 30;

			/// <summary>門檻值的下限。高山測站可能出現負溫。</summary>
			public const decimal ThresholdMin = -999.9m;

			/// <summary>門檻值的上限。24 小時雨量可以到數百 mm。</summary>
			public const decimal ThresholdMax = 999.9m;

			/// <summary>
			/// 門檻值收到小數點後幾位。與資料庫欄位精度 decimal(4,1) 一致——
			/// 不驗的話 SQL Server 會自己捨入且不報錯，使用者填 32.55、系統實際用 32.6 比對，
			/// 而他無從發現。
			/// </summary>
			public const int ThresholdDecimals = 1;

			/// <summary>
			/// 該型態的通知保留天數預設值。呼叫前 ruleType 必須已經驗過是合法值。
			/// </summary>
			public static int DefaultExpiryDays(string ruleType)
				=> ruleType == Types.Numeric ? NumericDefaultExpiryDays : EventDefaultExpiryDays;

			/// <summary>
			/// 該型態的通知保留天數上限。呼叫前 ruleType 必須已經驗過是合法值。
			/// </summary>
			public static int MaxExpiryDays(string ruleType)
				=> ruleType == Types.Numeric ? NumericMaxExpiryDays : EventMaxExpiryDays;
		}
	}
}
