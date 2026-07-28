using System.Text.Json.Serialization;
using TaiwanAgri.Core.Dtos;

namespace TaiwanAgri.Modules.Pet.Dtos.WorkerResponses
{
	/// <summary>
	/// API 整體回應包裝，含分頁控制與狀態碼
	/// </summary>
	public class ShelterAnimalApiResponse : IMoaPagedResponse<ShelterAnimalDto>
	{
		[JsonPropertyName("RS")]
		public string RS { get; set; } = string.Empty;

		[JsonPropertyName("Data")]
		public List<ShelterAnimalDto> Data { get; set; } = new();

		[JsonPropertyName("Next")]
		public bool Next { get; set; }
	}
}