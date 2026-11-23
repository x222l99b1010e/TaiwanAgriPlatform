using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TaiwanAgri.Web.Extensions;
using TaiwanAgri.Web.Dtos;
using TaiwanAgri.Web.Services;

namespace TaiwanAgri.Web.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	// 登入與註冊以來源位址限流（見 RateLimitingExtensions.AuthPolicy）
	[EnableRateLimiting(RateLimitingExtensions.AuthPolicy)]
	public class AuthController : ControllerBase
	{
		private readonly IAuthService _authService;

		public AuthController(IAuthService authService)
		{
			_authService = authService;
		}

		[HttpPost("login")]
		public async Task<IActionResult> Login([FromBody] LoginRequestDto request, CancellationToken cancellationToken = default)
		{
			try
			{
				var result = await _authService.LoginAsync(request, cancellationToken);
				return Ok(result);
			}
			catch (UnauthorizedAccessException ex)
			{
				return Unauthorized(ex.Message);
			}
		}

		[HttpPost("register")]
		public async Task<IActionResult> Register([FromBody] RegisterRequestDto request, CancellationToken cancellationToken = default)
		{
			try
			{
				var result = await _authService.RegisterAsync(request, cancellationToken);
				return Ok(result);
			}
			// 只接「被 Identity 擋下」這一種，訊息是寫給使用者看的；
			// 其他例外（資料庫錯誤之類）交給 GlobalExceptionMiddleware 回 500，原文不回給使用者
			catch (RegistrationRejectedException ex)
			{
				return BadRequest(ex.Message);
			}
		}
	}
}
