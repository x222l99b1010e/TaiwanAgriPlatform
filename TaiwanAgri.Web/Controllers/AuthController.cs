using Microsoft.AspNetCore.Mvc;
using TaiwanAgri.Web.Dtos;
using TaiwanAgri.Web.Services;

namespace TaiwanAgri.Web.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
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
			catch (InvalidOperationException ex)
			{
				return BadRequest(ex.Message);
			}
		}
	}
}
