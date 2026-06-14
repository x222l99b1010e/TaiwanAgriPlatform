namespace TaiwanAgri.Modules.User.Dtos.ApiResponses
{
	public class WatchlistEnrichedItemDto
	{
		// 來自 WatchlistItemDto（靜態偏好）
		public int Id { get; set; }
		public string CropCode { get; set; } = string.Empty;
		public string CropName { get; set; } = string.Empty;
		public string? MarketCode { get; set; }
		public string? MarketName { get; set; }
		public string MarketType { get; set; } = string.Empty;

		// 來自 PriceResponseDto（動態價格）
		public decimal? AvgPrice { get; set; }
		public DateOnly? TransDate { get; set; }
	}
}
