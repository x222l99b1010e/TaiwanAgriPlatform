namespace TaiwanAgri.Core.Dtos
{
	/// <summary>
	/// 農業部開放資料 API 的分頁回應共同形狀（RS 狀態碼 + Data 清單 + Next 換頁旗標）。
	/// 各模組的 XxxApiResponse 實作此介面後，即可交給 MoaPagedFetcher 統一處理分頁抓取
	/// </summary>
	public interface IMoaPagedResponse<TDto>
	{
		string RS { get; }
		List<TDto> Data { get; }
		bool Next { get; }
	}
}
