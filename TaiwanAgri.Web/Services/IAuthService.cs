using TaiwanAgri.Web.Dtos;

namespace TaiwanAgri.Web.Services
{
	public interface IAuthService
	{
		Task<AuthResponseDto> LoginAsync(LoginRequestDto request);
		Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request);
	}
}
