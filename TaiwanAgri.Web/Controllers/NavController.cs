using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TaiwanAgri.Core.Services;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace TaiwanAgri.Web.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	public class NavController : ControllerBase
	{
		private readonly INavService _navService;
		public NavController(INavService navService)
		{
			_navService = navService;
		}
		[HttpGet("modules")]
		[AllowAnonymous]
		public async Task<IActionResult> GetModules()
		{
			// 1. 取得 isAuthenticated
			var isAuthenticated = User.Identity?.IsAuthenticated ?? false;

			// 2. 取得 roleName
			//var roleId = new Claim(ClaimTypes.Role, "Admin");
			var roleName = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;
			// 3. 呼叫 Service
			var modules = await _navService.GetNavModulesAsync(isAuthenticated, roleName);

			//throw new Exception("測試 GlobalExceptionMiddleware");  // 測試 GlobalExceptionMiddleware

			return Ok(modules);
		}
	}
}
