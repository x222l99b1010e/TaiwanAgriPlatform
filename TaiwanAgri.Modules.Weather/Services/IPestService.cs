using TaiwanAgri.Core.Dtos;
using TaiwanAgri.Modules.Weather.Dtos.ApiResponses;

namespace TaiwanAgri.Modules.Weather.Services
{
	public interface IPestService
	{
		Task<PagedResult<PestAlertResponseDto>> GetPestAlertsByCityAsync(string? cityName = null, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);

		/// <summary>各縣市被幾則警報影響（縣市燈號用），縣市一律是正式寫法「臺」</summary>
		Task<List<PestAlertCityCountDto>> GetPestAlertCountsByCityAsync(CancellationToken cancellationToken = default);

		Task<List<PestDecadeSummaryResponseDto>> GetPestDecadeDensityByPestNameAsync(string pestName, CancellationToken cancellationToken = default);

		Task<List<string>> GetAllPestNamesAsync(CancellationToken cancellationToken = default);
	}
}
