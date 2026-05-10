using System.Text.Json.Serialization;

namespace TaiwanAgri.Modules.Market.Dtos.WorkerResponses
{
	public class MarketRestDayDto
	{
		[JsonPropertyName("MarkerNo")]
		public string MarketCode { get; set; } = string.Empty;
		[JsonPropertyName("MarkerName")]
		public string MarketName { get; set; } = string.Empty;
		[JsonPropertyName("MarketTypeList")]
		public List<MarketRestDayTypeDto> MarketTypeList { get; set; } = new();

	}

	public class MarketRestDayTypeDto
	{
		[JsonPropertyName("MarketType")]
		public string Type { get; set; } = string.Empty;
		[JsonPropertyName("YearList")]
		public List<MarketRestDayYearDto> YearList { get; set; } = new();

	}

	public class MarketRestDayYearDto
	{
		[JsonPropertyName("Year")]
		public int Year { get; set; }
		[JsonPropertyName("MonthList")]
		public List<MarketRestDayMonthDto> MonthList { get; set; } = new();
	}

	public class MarketRestDayMonthDto
	{
		[JsonPropertyName("Month")]
		public int Month { get; set; }
		[JsonPropertyName("Rest")]
		public string RestDay { get; set; } = string.Empty;

	}
}
