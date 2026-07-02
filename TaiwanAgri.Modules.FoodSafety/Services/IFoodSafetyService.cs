using TaiwanAgri.Core.Dtos;
using TaiwanAgri.Modules.FoodSafety.Dtos.ApiResponses;

namespace TaiwanAgri.Modules.FoodSafety.Services
{
	public interface IFoodSafetyService
	{
		Task<TraceabilityResponseDto> SearchTraceabilityAsync(string traceCode);

		Task<PagedResult<ViolationResponseDto>> GetViolationsAsync(int days, string? inspectResult = null, int page = 1, int pageSize = 20);
	}
}
