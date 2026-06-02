using TaiwanAgri.Modules.Weather.Dtos.ApiResponses;

namespace TaiwanAgri.Modules.Weather.Services
{
	public interface IPestService
	{
		Task<List<PestAlertResponseDto>> GetPestAlertsByCityAsync(string? cityName = null, int page = 1);

		Task<List<PestDecadeSummaryResponseDto>> GetPestDecadeDensityByPestNameAsync(string pestName);

		Task<List<string>> GetAllPestNamesAsync();
	}
}
