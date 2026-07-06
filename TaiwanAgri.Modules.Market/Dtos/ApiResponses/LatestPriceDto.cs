namespace TaiwanAgri.Modules.Market.Dtos.ApiResponses
{
	/// <summary>
	/// 單一（作物, 市場）組合的最新一筆均價。
	/// 供監看清單等「一次要多組最新價」的批次查詢使用，
	/// 與 PriceResponseDto（日期區間 + 跨市場聚合）的語意不同
	/// </summary>
	public class LatestPriceDto
	{
		public string CropCode { get; set; } = string.Empty;
		public string MarketCode { get; set; } = string.Empty;
		public DateOnly TransDate { get; set; }
		public decimal AvgPrice { get; set; }
	}
}
