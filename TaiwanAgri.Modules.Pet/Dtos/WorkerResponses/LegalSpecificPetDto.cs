using System.Text.Json.Serialization;

namespace TaiwanAgri.Modules.Pet.Dtos.WorkerResponses
{
	/// <summary>
	/// 合法特定寵物業 API 回應 DTO（LegalSpecificPet），忠實承接原始資料形狀，
	/// 型別轉換（enum／DateOnly／縣市代碼轉名稱）延後至 MapToEntity 執行。
	/// 新制（api/v1，{RS,Data,Next} 包裝）與舊制（TransService.aspx，裸陣列）
	/// 回傳的欄位形狀完全一致，共用同一份 DTO。
	/// </summary>
	public class LegalSpecificPetDto
	{
		[JsonPropertyName("ID")]
		public string ID { get; set; } = string.Empty;

		[JsonPropertyName("legaltype")]
		public string LegalType { get; set; } = string.Empty;

		[JsonPropertyName("legalname")]
		public string LegalName { get; set; } = string.Empty;

		[JsonPropertyName("legaladdress")]
		public string LegalAddress { get; set; } = string.Empty;

		[JsonPropertyName("busitem")]
		public string BusItem { get; set; } = string.Empty;

		[JsonPropertyName("animaltype")]
		public string AnimalType { get; set; } = string.Empty;

		[JsonPropertyName("validnum")]
		public string ValidNum { get; set; } = string.Empty;

		/// <summary>原始格式為 "2028/3/12 上午 12:00:00"，保留字串，轉換延後至 Entity 映射層</summary>
		[JsonPropertyName("validdate")]
		public string ValidDate { get; set; } = string.Empty;

		[JsonPropertyName("own_name")]
		public string OwnName { get; set; } = string.Empty;

		[JsonPropertyName("bos_name")]
		public string BosName { get; set; } = string.Empty;

		[JsonPropertyName("rank_year")]
		public string RankYear { get; set; } = string.Empty;

		[JsonPropertyName("rank_code")]
		public string RankCode { get; set; } = string.Empty;

		[JsonPropertyName("rank_flag_1")]
		public string RankFlag1 { get; set; } = string.Empty;

		[JsonPropertyName("rank_flag_2")]
		public string RankFlag2 { get; set; } = string.Empty;

		[JsonPropertyName("rank_text")]
		public string RankText { get; set; } = string.Empty;

		[JsonPropertyName("state_flag")]
		public string StateFlag { get; set; } = string.Empty;
	}
}
