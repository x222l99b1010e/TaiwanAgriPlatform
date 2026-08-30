using TaiwanAgri.Core.Dtos;
using TaiwanAgri.Modules.Weather.Dtos.ApiResponses;

namespace TaiwanAgri.Modules.Weather.Services
{
	public interface IPestService
	{
		Task<PagedResult<PestAlertResponseDto>> GetPestAlertsByCityAsync(string? cityName = null, int page = 1, int pageSize = 20);

		Task<List<PestDecadeSummaryResponseDto>> GetPestDecadeDensityByPestNameAsync(string pestName);

		Task<List<string>> GetAllPestNamesAsync();
	}
}
