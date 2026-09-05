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
		public async Task<UserNotificationPageDto> GetUserNotificationsAsync(string userId, int page)
		{
			// 多取一筆用來判斷還有沒有下一頁，回傳前砍掉。
			// 這樣 HasMore 是「真的還有資料」而不是「這頁剛好滿」——後者在總筆數是
			// 每頁筆數倍數時會多給一次載入更多，點了拿到空陣列
			var rows = await _dbContext.UserNotifications
				.Where(x => x.UserId == userId)
				.OrderByDescending(x => x.TriggeredAt)
				.Skip((page - 1) * INotificationService.PageSize)
				.Take(INotificationService.PageSize + 1)
				.Select(x => new UserNotificationResponseDto
				{
					Id = x.Id,
					Message = x.Message,
					RuleName = x.PestRuleConfig.RuleName,
					TriggeredAt = x.TriggeredAt,
					IsRead = x.IsRead
				})
				.ToListAsync();

			var hasMore = rows.Count > INotificationService.PageSize;
			return new UserNotificationPageDto
			{
				Items = hasMore ? rows.Take(INotificationService.PageSize).ToList() : rows,
				HasMore = hasMore
			};
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

		public async Task<int> MarkAllAsReadAsync(string userId)
		{
			// 一次查出未讀、一次 SaveChanges，取代前端逐筆送 PATCH（N 個請求、N 次 DB round trip）。
			// 刻意不用 ExecuteUpdateAsync：本專案 Service 層測試一律走 EF InMemory，
			// 而 InMemory 不支援 ExecuteUpdate，改用它等於讓這支方法無法被既有方式測試
			var unread = await _dbContext.UserNotifications
				.Where(x => x.UserId == userId && !x.IsRead)
				.ToListAsync();

			if (unread.Count == 0)
				return 0;

			foreach (var n in unread)
				n.IsRead = true;

			await _dbContext.SaveChangesAsync();
			return unread.Count;
		}
	}
}
