using TaiwanAgri.Core.Dtos;
using TaiwanAgri.Modules.FoodSafety.Dtos.ApiResponses;
using TaiwanAgri.Modules.FoodSafety.Dtos.Queries;

namespace TaiwanAgri.Modules.FoodSafety.Services
{
	public interface IFoodSafetyService
	{
		Task<PagedResult<ViolationResponseDto>> GetViolationsAsync(ViolationQueryDto queryDto, CancellationToken cancellationToken = default);

		Task<PagedResult<OrganicCertificationResponseDto>> GetOrganicCertificationsAsync(OrganicCertificationQueryDto queryDto, CancellationToken cancellationToken = default);
	}
}
