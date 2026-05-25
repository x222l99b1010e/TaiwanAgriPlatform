using Microsoft.EntityFrameworkCore;
using TaiwanAgri.Modules.Weather.Data;
using TaiwanAgri.Modules.Weather.Dtos.ApiResponses;

namespace TaiwanAgri.Modules.Weather.Services
{
	public class NotificationService : INotificationService
	{
		private readonly WeatherDbContext _dbContext;
		public NotificationService(WeatherDbContext dbContext)
		{
			_dbContext = dbContext;
		}
		public async Task<List<UserNotificationResponseDto>> GetUserNotificationsAsync(string userId, int page)
		{
			var userNotification = await _dbContext.UserNotifications
				.Where(x => x.UserId == userId)
				.OrderByDescending(x => x.TriggeredAt)
				.Skip((page - 1) * 20)
				.Take(20)
				.Select(x => new UserNotificationResponseDto
				{
					Id = x.Id,
					Message = x.Message,
					RuleName = x.PestRuleConfig.RuleName,
					TriggeredAt = x.TriggeredAt,
					IsRead = x.IsRead
				})
				.ToListAsync();
			return userNotification;
		}
		public async Task<UnreadCountResponseDto> GetUnreadCountAsync(string userId)
		{
			var unreadCount = await _dbContext.UserNotifications
				.Where(x => x.UserId == userId && !x.IsRead)
				.CountAsync();
			return new UnreadCountResponseDto { Count = unreadCount };
		}

		public async Task MarkAsReadAsync(int notificationId, string userId)
		{
			var notification = await _dbContext.UserNotifications
				.FirstOrDefaultAsync(x => x.Id == notificationId && x.UserId == userId);
			if (notification == null)
				throw new KeyNotFoundException($"通知 {notificationId} 不存在或無權限");

			notification.IsRead = true;
			await _dbContext.SaveChangesAsync();
		}
	}
}
