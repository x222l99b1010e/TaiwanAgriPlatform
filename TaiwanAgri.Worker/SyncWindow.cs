namespace TaiwanAgri.Worker
{
	/// <summary>
	/// 日期游標型 Worker（農產品行情、毛豬、家禽、收容動物、走失啟事）每一輪要抓的日期區間。
	/// <para>
	/// 為什麼要往回重掃：農業部的資料會晚公布——實測週六的行情到週日上午還查不到，家禽也常晚一兩天。
	/// 只抓「上次同步的隔天」到昨天的話，還沒公布的那天會被當成「無資料」，游標照樣推進，
	/// 之後再也不會回頭抓，而且沒有任何訊號。所以每一輪都從「往回 <see cref="LookbackDays"/> 天」開始，
	/// 已經存在的資料靠各 Worker 的鍵去重濾掉，重抓不會重複寫入。
	/// </para>
	/// </summary>
	internal static class SyncWindow
	{
		/// <summary>
		/// 往回重掃幾天（含昨天）。7 天涵蓋週末加上隔天公布的延遲；
		/// 春節這類連假公布可能更晚，超過的部分還是會缺
		/// </summary>
		internal const int LookbackDays = 7;

		/// <summary>
		/// 這一輪從哪天開始抓：「上次同步的隔天」與「昨天往回 <see cref="LookbackDays"/> 天」取較早者。
		/// 前者用在回填（落後很多天），後者用在日常（已追平，但最近幾天可能有晚到的資料）
		/// </summary>
		internal static DateOnly StartDate(DateOnly lastSyncedDate, DateOnly yesterday)
		{
			var next = lastSyncedDate.AddDays(1);
			var lookbackStart = yesterday.AddDays(-(LookbackDays - 1));
			return next < lookbackStart ? next : lookbackStart;
		}

		/// <summary>
		/// 處理完 <paramref name="processedDate"/> 之後游標該停在哪：只往前、不往回。
		/// 重掃的日子比游標舊，照寫會把游標拉回去，中途失敗時下一輪就得從更早開始
		/// </summary>
		internal static DateOnly Advance(DateOnly lastSyncedDate, DateOnly processedDate)
			=> processedDate > lastSyncedDate ? processedDate : lastSyncedDate;
	}
}
