namespace TaiwanAgri.Modules.User.Dtos.ApiResponses
{
	public class WatchlistItemDto
	{
		public int Id { get; set; }
		public string CropCode { get; set; } = string.Empty;
		public string CropName { get; set; } = string.Empty;
		public string? MarketCode { get; set; }
		public string? MarketName { get; set;}

	}
}
