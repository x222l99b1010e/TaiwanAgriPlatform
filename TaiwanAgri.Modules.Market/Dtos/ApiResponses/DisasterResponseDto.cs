namespace TaiwanAgri.Modules.Market.Dtos.ApiResponses
{
	public class DisasterResponseDto
	{
		public string DisasterName { get; set; } = string.Empty;
		public string AlertType { get; set; } = string.Empty;
		public string AlertDate { get; set; } = string.Empty;
		public List<string> AffectedCounties { get; set; } = new();
	}
}
