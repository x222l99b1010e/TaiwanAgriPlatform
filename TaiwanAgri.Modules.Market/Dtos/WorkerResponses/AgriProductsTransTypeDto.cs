using System.Text.Json.Serialization;

namespace TaiwanAgri.Modules.Market.Dtos.WorkerResponses
{
	public class AgriProductsTransTypeDto
	{
		[JsonPropertyName("TransDate")]
		public string TransDate { get; set; } = string.Empty;
		[JsonPropertyName("TcType")]
		public string TcType { get; set; } = string.Empty;
		[JsonPropertyName("CropCode")]
		public string CropCode { get; set; } = string.Empty;
		[JsonPropertyName("CropName")]
		public string CropName { get; set; } = string.Empty;
		[JsonPropertyName("MarketCode")]
		public string MarketCode { get; set; } = string.Empty;
		[JsonPropertyName("MarketName")]
		public string MarketName { get; set; } = string.Empty;
		[JsonPropertyName("Upper_Price")]
		public decimal UpperPrice { get; set; }
		[JsonPropertyName("Middle_Price")]
		public decimal MiddlePrice { get; set; }
		[JsonPropertyName("Lower_Price")]
		public decimal LowerPrice { get; set; }
		[JsonPropertyName("Avg_Price")]
		public decimal AvgPrice { get; set; }
		[JsonPropertyName("Trans_Quantity")]
		public decimal TransQty { get; set; }
	}
}
