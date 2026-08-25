using TaiwanAgri.Modules.Market.Dtos.ApiResponses;

namespace TaiwanAgri.Modules.Market.Services
{
	public interface IMarketService
	{
		Task<List<PorkResponseDto>> GetPorkAsync(
			string? marketName,
			DateOnly? startDate,
			DateOnly? endDate);

		/// <summary>
		/// 家禽行情查詢。metricCodes 為 null 或空陣列時回傳全部 17 個指標；
		/// 回傳含價格為 null 的資料點（狀態由 PriceStatus 表達），不在此層過濾
		/// </summary>
		Task<List<PoultryResponseDto>> GetPoultryAsync(
			string[]? metricCodes,
			DateOnly? startDate,
			DateOnly? endDate);
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

		Task<DateOnly?> GetLatestTransDateAsync(string marketCode);

		/// <summary>
		/// 批次取得多組（作物, 市場）的最新一筆均價。
		/// 一次 SQL 完成，取代逐筆呼叫 GetPricesAsync 的 N+1 查詢
		/// </summary>
		Task<List<LatestPriceDto>> GetLatestPricesAsync(
			IEnumerable<(string CropCode, string MarketCode)> keys);
	}
}
