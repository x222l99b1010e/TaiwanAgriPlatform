using System.Text.Json.Serialization;

namespace TaiwanAgri.Modules.Pet.Dtos.WorkerResponses
{
	/// <summary>
	/// 寵物遺失啟事 API 回應 DTO（PetLoseList），忠實承接原始資料形狀，
	/// 型別轉換（enum／DateOnly）延後至 MapToEntity 執行
	/// </summary>
	public class PetLoseListDto
	{
		[JsonPropertyName("KeyNo")]
		public string KeyNo { get; set; } = string.Empty;

		[JsonPropertyName("ChipNum")]
		public string ChipNum { get; set; } = string.Empty;

		[JsonPropertyName("PetName")]
		public string PetName { get; set; } = string.Empty;

		[JsonPropertyName("PetCategory")]
		public string PetCategory { get; set; } = string.Empty;

		[JsonPropertyName("Gender")]
		public string Gender { get; set; } = string.Empty;

		[JsonPropertyName("Variety")]
		public string Variety { get; set; } = string.Empty;

		[JsonPropertyName("Coat")]
		public string Coat { get; set; } = string.Empty;

		[JsonPropertyName("Exterior")]
		public string Exterior { get; set; } = string.Empty;

		[JsonPropertyName("Feature")]
		public string Feature { get; set; } = string.Empty;

		/// <summary>原始格式為 "2024/01/01"，保留字串，轉換延後至 Entity 映射層</summary>
		[JsonPropertyName("LostTime")]
		public string LostTime { get; set; } = string.Empty;

		[JsonPropertyName("LostPlace")]
		public string LostPlace { get; set; } = string.Empty;

		[JsonPropertyName("FeederName")]
		public string FeederName { get; set; } = string.Empty;

		[JsonPropertyName("PhoneNum")]
		public string PhoneNum { get; set; } = string.Empty;

		[JsonPropertyName("EMail")]
		public string EMail { get; set; } = string.Empty;

		[JsonPropertyName("Picture")]
		public string Picture { get; set; } = string.Empty;
	}
}
