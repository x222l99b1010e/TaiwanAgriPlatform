using TaiwanAgri.Core.Dtos;
using TaiwanAgri.Modules.FoodSafety.Dtos.ApiResponses;
using TaiwanAgri.Modules.FoodSafety.Dtos.Queries;

namespace TaiwanAgri.Modules.FoodSafety.Services
{
	public interface IFoodSafetyService
	{
		Task<TraceabilityResponseDto> SearchTraceabilityAsync(string traceCode);

		Task<PagedResult<ViolationResponseDto>> GetViolationsAsync(ViolationQueryDto queryDto);

		Task<PagedResult<OrganicCertificationResponseDto>> GetOrganicCertificationsAsync(OrganicCertificationQueryDto queryDto);
	}
}
