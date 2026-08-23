using System.Text.Json.Serialization;

namespace TaiwanAgri.Modules.Weather.Dtos.ExternalResponses
{
	/// <summary>
	/// 農藥「使用範圍」的單筆核准用途，來源是許可證資料裡 ScopeOfUse 欄位指向的網址。
	///
	/// 兩個與其他外部 DTO 不同的地方：
	/// 1. 這支端點回的是**裸 JSON 陣列**，沒有 RS／Data／Next 信封，
	///    所以反序列化目標是 List&lt;PesticideUsageExternalDto&gt; 而不是某個 ApiResponse 類別。
	/// 2. 它的 JSON 屬性名是**中文**，所以每個欄位都必須標 JsonPropertyName，不能靠命名慣例對應。
	/// </summary>
	public class PesticideUsageExternalDto
	{
		[JsonPropertyName("作物名稱")] public string CropName { get; set; } = string.Empty;
		[JsonPropertyName("病蟲害名稱")] public string PestName { get; set; } = string.Empty;
		[JsonPropertyName("施用次數")] public string ApplicationCount { get; set; } = string.Empty;
		[JsonPropertyName("每公頃使用用藥量")] public string DosagePerHectare { get; set; } = string.Empty;
		[JsonPropertyName("稀釋倍數")] public string Dilution { get; set; } = string.Empty;
		[JsonPropertyName("使用時期")] public string ApplicationTiming { get; set; } = string.Empty;
		[JsonPropertyName("施藥間隔")] public string ApplicationInterval { get; set; } = string.Empty;

		/// <summary>安全採收期。這是整個功能對使用者最關鍵的欄位——用錯會導致農藥殘留超標。</summary>
		[JsonPropertyName("安全採收期")] public string SafeHarvestInterval { get; set; } = string.Empty;

		[JsonPropertyName("備註")] public string Notes { get; set; } = string.Empty;
		[JsonPropertyName("注意事項")] public string Precautions { get; set; } = string.Empty;
		[JsonPropertyName("施藥方法")] public string ApplicationMethod { get; set; } = string.Empty;
		[JsonPropertyName("說明")] public string Description { get; set; } = string.Empty;

		/// <summary>核准日期，民國「七位數字連寫」格式（如 "1110524"），與許可證的兩種日期格式又不同。</summary>
		[JsonPropertyName("核准日期")] public string ApprovalDate { get; set; } = string.Empty;

		[JsonPropertyName("原始登記廠商名稱")] public string OriginalRegistrant { get; set; } = string.Empty;
	}
}
