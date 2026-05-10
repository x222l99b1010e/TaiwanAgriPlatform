namespace TaiwanAgri.Modules.Market.Dtos.ApiResponses
{
	public class PriceResponseDto
	{
		public string CropCode { get; set; } = string.Empty;
		public string CropName { get; set; } = string.Empty;
		public decimal AvgPrice { get; set; }
		public decimal UpperPrice { get; set; }
		public decimal MiddlePrice { get; set; }
		public decimal LowerPrice { get; set; }
		public decimal TransQuantity { get; set; }
		public DateOnly TransDate { get; set; }
	}
}
