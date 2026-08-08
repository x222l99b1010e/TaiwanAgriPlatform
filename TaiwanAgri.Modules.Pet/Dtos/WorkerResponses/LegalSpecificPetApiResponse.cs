using System.Text.Json.Serialization;
using TaiwanAgri.Core.Dtos;

namespace TaiwanAgri.Modules.Pet.Dtos.WorkerResponses
{
	/// <summary>
	/// 新制 api/v1/LegalSpecificPet/（逐縣市查詢）的回應包裝。
	/// 舊制 TransService.aspx 回傳裸陣列，不會用到這個包裝類別。
	/// </summary>
	public class LegalSpecificPetApiResponse : IMoaPagedResponse<LegalSpecificPetDto>
	{
		[JsonPropertyName("RS")]
		public string RS { get; set; } = string.Empty;

		[JsonPropertyName("Data")]
		public List<LegalSpecificPetDto> Data { get; set; } = new();

		[JsonPropertyName("Next")]
		public bool Next { get; set; }
	}
}
