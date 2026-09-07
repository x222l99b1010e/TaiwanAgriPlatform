namespace TaiwanAgri.Modules.Weather.Dtos.ApiResponses
{
	/// <summary>
	/// 通知列表的一頁。HasMore 由後端明確回答，不讓前端從「回傳筆數是不是滿一頁」反推——
	/// 反推在「總筆數恰好是每頁筆數的倍數」時會多給一次「載入更多」，
	/// 點下去拿到空陣列；而且每頁筆數會變成前後端各硬編碼一次，改一邊就會壞
	/// </summary>
	public class UserNotificationPageDto
	{
		public List<UserNotificationResponseDto> Items { get; set; } = new();
		public bool HasMore { get; set; }
	}
}
