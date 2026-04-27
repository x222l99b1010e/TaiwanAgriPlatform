using System.Text.Json.Serialization;

namespace TaiwanAgri.Modules.Market.Dtos
{
	public class CropMarketTypeDto
	{
		[JsonPropertyName("MarketCode")]
		public string MarketCode { get; set; } = string.Empty;
		[JsonPropertyName("MarketName")]
		public string MarketName { get; set; } = string.Empty;
	}
}
