using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using TaiwanAgri.Core.Services;
using TaiwanAgri.Web.Extensions;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace TaiwanAgri.Web.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	// 免登入的查詢：掛公開查詢限流（掛在類別上，新增端點自動涵蓋；見 RateLimitingExtensions）
	[EnableRateLimiting(RateLimitingExtensions.PublicQueryPolicy)]
	public class NavController : ControllerBase
	{
		private readonly INavService _navService;
		public NavController(INavService navService)
		{
			_navService = navService;
		}
		[HttpGet("modules")]
		[AllowAnonymous]
		public async Task<IActionResult> GetModules(CancellationToken cancellationToken = default)
		{
			// 1. 取得 isAuthenticated
			var isAuthenticated = User.Identity?.IsAuthenticated ?? false;

			// 2. 取得 roleName
			//var roleId = new Claim(ClaimTypes.Role, "Admin");
			var roleName = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;
			// 3. 呼叫 Service
			var modules = await _navService.GetNavModulesAsync(isAuthenticated, roleName, cancellationToken);

			//throw new Exception("測試 GlobalExceptionMiddleware");  // 測試 GlobalExceptionMiddleware

			return Ok(modules);
		}
	}
}
