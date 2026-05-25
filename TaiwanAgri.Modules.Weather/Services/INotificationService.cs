using TaiwanAgri.Modules.Weather.Dtos.ApiResponses;

namespace TaiwanAgri.Modules.Weather.Services
{
	public interface INotificationService
	{
		Task<List<UserNotificationResponseDto>> GetUserNotificationsAsync(string userId, int page);
		Task<UnreadCountResponseDto> GetUnreadCountAsync(string userId);
		Task MarkAsReadAsync(int notificationId, string userId);
	}
}
