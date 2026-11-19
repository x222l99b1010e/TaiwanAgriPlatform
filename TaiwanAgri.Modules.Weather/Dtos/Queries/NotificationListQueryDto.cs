using System.ComponentModel.DataAnnotations;

namespace TaiwanAgri.Modules.Weather.Dtos.Queries
{
	/// <summary>
	/// 通知列表的查詢條件。每頁筆數由服務端固定（<c>INotificationService.PageSize</c>），所以只有頁碼，
	/// 不繼承帶 PageSize 的 PagedQueryDto；頁碼的界限與訊息跟它一致。
	/// 原本是沒有界限的整數，page=0 會讓 Skip 收到負數、回 500（2026-11 總檢查 3-A7）
	/// </summary>
	public class NotificationListQueryDto
	{
		[Range(1, int.MaxValue, ErrorMessage = "頁碼必須大於 0")]
		public int Page { get; set; } = 1;
	}
}
