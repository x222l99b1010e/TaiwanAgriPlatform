using TaiwanAgri.Core.Dtos;

namespace TaiwanAgri.Core.Services
{
	public interface INavService
	{
		Task<List<NavModuleDto>> GetNavModulesAsync(bool isAuthenticated, string? roleId);
	}
}
