using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
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
		[HttpGet("list")]
		public async Task<IActionResult> GetUserNotifications([FromQuery] int page = 1)
		{
			var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
			if (userId == null) return Unauthorized();

			var notifications = await _notificationService.GetUserNotificationsAsync(userId, page);
			return Ok(notifications);
		}
		// GET /api/Notification/unread-count
		[HttpGet("unread-count")]
		public async Task<IActionResult> GetUnreadCount()
		{
			var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
			if (userId == null) return Unauthorized();

			var unreadCount = await _notificationService.GetUnreadCountAsync(userId);
			return Ok(unreadCount);
		}
		// PATCH /api/Notification/read-all
		[HttpPatch("read-all")]
		public async Task<IActionResult> MarkAllAsRead()
		{
			var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
			if (userId == null) return Unauthorized();

			var updated = await _notificationService.MarkAllAsReadAsync(userId);
			return Ok(new { updated });
		}
		// PATCH /api/Notification/{id}/read
		[HttpPatch("{id}/read")]
		public async Task<IActionResult> MarkAsRead(int id)
		{
			var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
			if (userId == null) return Unauthorized();

			try
			{
				await _notificationService.MarkAsReadAsync(id, userId);
				return NoContent();
			}
			catch (KeyNotFoundException)
			{
				return NotFound();
			}
		}
	}
}

