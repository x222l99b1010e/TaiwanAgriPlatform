namespace TaiwanAgri.Modules.User.Dtos.ApiRequests
{
	public class AddWatchlistRequestDto
	{
		public string CropCode { get; set; } = string.Empty;
		public string CropName { get; set; } = string.Empty;
		public string? MarketCode { get; set; }
		public string? MarketName { get; set; }
		public string MarketType { get; set; } = string.Empty;
	}
}
