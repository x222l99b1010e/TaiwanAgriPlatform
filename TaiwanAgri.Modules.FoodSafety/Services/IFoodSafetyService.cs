using TaiwanAgri.Modules.FoodSafety.Dtos.ApiResponses;

namespace TaiwanAgri.Modules.FoodSafety.Services
{
	public interface IFoodSafetyService
	{
		Task<TraceabilityResponseDto> SearchTraceabilityAsync(string traceCode);
	}
}
