using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TaiwanAgri.Modules.Weather.Dtos.Queries;
using TaiwanAgri.Modules.Weather.Services;

namespace TaiwanAgri.Web.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	[Authorize]
	public class NotificationController : ControllerBase
	{
		private readonly INotificationService _notificationService;
		public NotificationController(INotificationService notificationService)
		{
			_notificationService = notificationService;
		}
		// GET /api/Notification/list?page=1
		// 頁碼的界限掛在查詢 DTO 上，page=0 由 [ApiController] 擋下回 400（原本 Skip 收到負數、回 500）
		[HttpGet("list")]
		public async Task<IActionResult> GetUserNotifications([FromQuery] NotificationListQueryDto query, CancellationToken cancellationToken = default)
		{
			var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
			if (userId == null) return Unauthorized();

			var notifications = await _notificationService.GetUserNotificationsAsync(userId, query.Page, cancellationToken);
			return Ok(notifications);
		}
		// GET /api/Notification/unread-count
		[HttpGet("unread-count")]
		public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken = default)
		{
			var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
			if (userId == null) return Unauthorized();

			var unreadCount = await _notificationService.GetUnreadCountAsync(userId, cancellationToken);
			return Ok(unreadCount);
		}
		// PATCH /api/Notification/read-all
		[HttpPatch("read-all")]
		public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken = default)
		{
			var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
			if (userId == null) return Unauthorized();

			var updated = await _notificationService.MarkAllAsReadAsync(userId, cancellationToken);
			return Ok(new { updated });
		}
		// PATCH /api/Notification/{id}/read
		[HttpPatch("{id}/read")]
		public async Task<IActionResult> MarkAsRead(int id, CancellationToken cancellationToken = default)
		{
			var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
			if (userId == null) return Unauthorized();

			try
			{
				await _notificationService.MarkAsReadAsync(id, userId, cancellationToken);
				return NoContent();
			}
			catch (KeyNotFoundException)
			{
				return NotFound();
			}
		}
	}
}

