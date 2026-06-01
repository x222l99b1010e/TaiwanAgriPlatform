namespace TaiwanAgri.Modules.Market.Dtos.ApiResponses
{
	public class PorkResponseDto
	{
		public DateOnly TransDate { get; set; }
		public string MarketName { get; set; } = string.Empty;
		public decimal ExcludeFreezerAvgPrice { get; set; }
		public decimal ExcludeFreezerAvgWeight { get; set; }
		public int ExcludeFreezerCount { get; set; }
	}
}