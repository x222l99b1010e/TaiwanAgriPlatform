using TaiwanAgri.Modules.FoodSafety.Dtos.ApiResponses;

namespace TaiwanAgri.Modules.FoodSafety.Services
{
	public interface ITraceabilityService
	{
		Task<TraceabilityResponseDto> SearchTraceabilityAsync(string traceCode, CancellationToken cancellationToken = default);
	}
}
