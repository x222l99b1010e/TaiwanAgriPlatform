using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TaiwanAgri.Modules.User.Dtos.ApiRequests;
using TaiwanAgri.Modules.User.Services;

namespace TaiwanAgri.Web.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	[Authorize]
	// [Authorize]：這個 Controller 所有端點都需要 JWT，未登入回 401
	// userId 從 JWT Claims 取，不從 Query 參數傳（和 NotificationController 的還原方式一致）
	public class ProfileController(IUserProfileService userProfileService) : ControllerBase
	{
		[HttpGet("farm")]
		public async Task<IActionResult> GetFarmProfile(CancellationToken cancellationToken = default)
		{
			// User.FindFirstValue：從 JWT Claims 裡找 NameIdentifier（就是 UserId）
			// JWT 驗證通過但 Claim 不存在是異常情況，回 401
			var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
			if (userId is null) return Unauthorized();
			

			var profile = await userProfileService.GetUserFarmProfileAsync(userId, cancellationToken);

			if (profile is null)
			{
				// 第一次進來，還沒有農場設定
				// 回 200 + null，不回 404
				// 理由：「沒有設定」是正常情況，不是錯誤
				return Ok(null);
			}

			return Ok(new
			{
				profile.FarmCity,
				profile.FarmType,
				profile.CreatedAt,
				profile.UpdatedAt,
				Crops = profile.Crops.Select(c => new
				{
					c.CropCode,
					c.CropName
				}).ToList()
			});
		}

		[HttpPut("farm")]
		public async Task<IActionResult> UpsertFarmProfile([FromBody] UpsertFarmProfileRequestDto request, CancellationToken cancellationToken = default)
		{
			var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
			if (userId is null) return Unauthorized();

			await userProfileService.UpsertUserFarmProfileAsync(
				userId,
				request.FarmCity,
				request.FarmType,
				request.Crops.Select(c => (c.CropCode, c.CropName)).ToList()
			);
			// 204 NoContent：儲存成功，不需要回傳資料
			// 前端儲存成功後再呼叫 GET 取得最新資料
			return NoContent();
			
		}
	}
}
