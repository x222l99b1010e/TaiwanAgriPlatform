using TaiwanAgri.Modules.Market.Dtos.ApiResponses;

namespace TaiwanAgri.Modules.Market.Services
{
	public interface IMarketService
	{
		Task<List<RestDayResponseDto>> GetRestDaysAsync(
		string marketCode,
		DateOnly startDate,
		DateOnly endDate);

		Task<List<MarketResponseDto>> GetMarketsAsync(
			string marketType);

		Task<List<CropResponseDto>> GetCropsAsync(
			string marketType);

		Task<List<DisasterResponseDto>> GetDisastersAsync(
			string[] counties,
			DateOnly startDate,
			DateOnly endDate);   // ← 移除 alertDate

		Task<List<PriceResponseDto>> GetPricesAsync(
			string marketType,
			string[] cropCodes,
			string? marketCode = null,
			DateOnly? startDate = null,
			DateOnly? endDate = null);
	}
}
