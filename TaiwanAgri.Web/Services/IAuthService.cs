using TaiwanAgri.Web.Dtos;

namespace TaiwanAgri.Web.Services
{
	public interface IAuthService
	{
		Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);
		Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default);
	}
}
