using TaiwanAgri.Modules.Weather.Dtos.ApiResponses;

namespace TaiwanAgri.Modules.Weather.Services
{
	public interface INotificationService
	{
		/// <summary>每頁筆數的單一真相來源；前端不再自備一份</summary>
		const int PageSize = 20;

		Task<UserNotificationPageDto> GetUserNotificationsAsync(string userId, int page, CancellationToken cancellationToken = default);
		Task<UnreadCountResponseDto> GetUnreadCountAsync(string userId, CancellationToken cancellationToken = default);
		Task MarkAsReadAsync(int notificationId, string userId, CancellationToken cancellationToken = default);

		/// <summary>把該使用者所有未讀通知一次標記為已讀，回傳實際更新筆數</summary>
		Task<int> MarkAllAsReadAsync(string userId, CancellationToken cancellationToken = default);
	}
}
