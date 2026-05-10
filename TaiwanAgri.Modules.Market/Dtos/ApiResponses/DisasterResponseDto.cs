namespace TaiwanAgri.Modules.Market.Dtos.ApiResponses
{
	public class DisasterResponseDto
	{
		public string DisasterName { get; set; } = string.Empty;
		public string County { get; set; } = string.Empty;
		public string Town { get; set; } = string.Empty;
		public string AlertLevel { get; set; } = string.Empty;
		public string AlertType { get; set; } = string.Empty;
		public DateOnly LastUpdateDate { get; set; }
	}
}
