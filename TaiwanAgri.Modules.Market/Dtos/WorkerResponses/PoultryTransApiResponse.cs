using System.Text.Json.Serialization;
using TaiwanAgri.Core.Dtos;

namespace TaiwanAgri.Modules.Market.Dtos.WorkerResponses
{
	/// <summary>
	/// 四支家禽行情 API 的共用回應外殼。四支的 Data 內容欄位集各不相同，
	/// 但外層 RS/Data/Next 完全一致，所以用泛型參數承接內容型別，
	/// 不必為四支各寫一個形狀相同的 ApiResponse 類別（PorkTransTypeApiResponse 是
	/// 單一來源的既有寫法，這裡因為有四支同形狀來源才值得泛型化）
	/// </summary>
	public class PoultryTransApiResponse<TDto> : IMoaPagedResponse<TDto>
	{
		[JsonPropertyName("RS")]
		public string RS { get; set; } = string.Empty;

		[JsonPropertyName("Data")]
		public List<TDto> Data { get; set; } = new();

		[JsonPropertyName("Next")]
		public bool Next { get; set; }
	}
}
