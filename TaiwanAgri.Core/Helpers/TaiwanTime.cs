namespace TaiwanAgri.Core.Helpers
{
	/// <summary>
	/// 台灣時區的日界判定。全案「近 N 天」與「查詢預設區間」的慣例＝以台灣日曆日為準：
	/// 使用者與農業部資料都是台灣日期，改用 UtcNow（慢 8 小時）或主機本地時區
	/// 在日界前後會差一天。時間來源一律走 TimeProvider 注入，測試可固定時刻做邊界驗證
	/// </summary>
	public static class TaiwanTime
	{
		// Windows 時區 ID 與 Linux/macOS 不同，兩邊都要對應；
		// 查詢一次後快取，不在每次呼叫時重新 FindSystemTimeZoneById
		private static readonly TimeZoneInfo TaipeiTimeZone =
			TimeZoneInfo.FindSystemTimeZoneById(
				OperatingSystem.IsWindows() ? "Taipei Standard Time" : "Asia/Taipei");

		/// <summary>取得台灣時區的今天日期</summary>
		public static DateOnly Today(TimeProvider timeProvider)
			=> DateOnly.FromDateTime(
				TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), TaipeiTimeZone).DateTime);
	}
}
