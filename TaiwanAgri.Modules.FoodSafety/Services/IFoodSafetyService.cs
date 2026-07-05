using TaiwanAgri.Core.Dtos;
using TaiwanAgri.Modules.FoodSafety.Dtos.ApiResponses;
using TaiwanAgri.Modules.FoodSafety.Dtos.Queries;

namespace TaiwanAgri.Modules.FoodSafety.Services
{
	public interface IFoodSafetyService
	{
		Task<TraceabilityResponseDto> SearchTraceabilityAsync(string traceCode);

		Task<PagedResult<ViolationResponseDto>> GetViolationsAsync(int days, string? inspectResult = null, int page = 1, int pageSize = 20);

		Task<PagedResult<OrganicCertificationResponseDto>> GetOrganicCertificationsAsync(OrganicCertificationQueryDto queryDto);
	}
}
