using System.Text.Json.Serialization;

namespace TaiwanAgri.Modules.FoodSafety.Dtos.WorkerResponses
{
	/// <summary>
	/// 有機農產品驗證資訊 API 回應 DTO
	/// 對應農業部 GET /TWOrganicAgricultureVerificationInformationType/ 回傳格式
	/// 忠實承接原始資料形狀，型別轉換與清洗延後至 MapToEntity 執行
	/// </summary>
	public class OrganicCertificationDto
	{
		[JsonPropertyName("Name")]
		public string Name { get; set; } = string.Empty;

		[JsonPropertyName("Address")]
		public string Address { get; set; } = string.Empty;

		[JsonPropertyName("Tel")]
		public string Tel { get; set; } = string.Empty;

		[JsonPropertyName("Products")]
		public string Products { get; set; } = string.Empty;

		[JsonPropertyName("BehaviorType")]
		public string BehaviorType { get; set; } = string.Empty;

		[JsonPropertyName("CompanyName")]
		public string CompanyName { get; set; } = string.Empty;

		[JsonPropertyName("CertOrganicSn")]
		public string CertOrganicSn { get; set; } = string.Empty;

		/// <summary>
		/// 原始格式為 "2028/10/14"，保留字串，轉換延後至 Entity 映射層
		/// </summary>
		[JsonPropertyName("EffectiveDate")]
		public string EffectiveDate { get; set; } = string.Empty;

		[JsonPropertyName("Status")]
		public string Status { get; set; } = string.Empty;

		[JsonPropertyName("ContainCrops")]
		public string ContainCrops { get; set; } = string.Empty;

		[JsonPropertyName("MailingAddress")]
		public string MailingAddress { get; set; } = string.Empty;

		[JsonPropertyName("OldCertOrganicSN")]
		public string OldCertOrganicSN { get; set; } = string.Empty;
	}
}
