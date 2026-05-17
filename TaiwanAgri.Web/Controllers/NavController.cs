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

			// 2. 取得 roleId
			//var roleId = new Claim(ClaimTypes.Role, "Admin");
			var roleId = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;


			// 3. 呼叫 Service
			var modules = await _navService.GetNavModulesAsync(isAuthenticated, roleId);
			return Ok(modules);
		}
	}
}
