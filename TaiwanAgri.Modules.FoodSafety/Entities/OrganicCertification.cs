using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiwanAgri.Modules.FoodSafety.Entities
{
	/// <summary>
	/// 有機農產品驗證資訊
	/// 對應農業部 GET /TWOrganicAgricultureVerificationInformationType/ 資料
	/// CertOrganicSn 為業務唯一鍵（Unique Index），Id 僅作為技術性 PK
	/// </summary>
	public class OrganicCertification
	{
		[Key]
		public int Id { get; set; }

		/// <summary>
		/// 證書序號。原始資料可能為單一值，或以頓號分隔的多值字串：
		/// 同值重複（如 "1-008-205501、1-008-205501"）視為髒資料，於 MapToEntity 正規化為單一值；
		/// 異值並存（如 "1-009-110011、1-009-120840"）視為單一 API 記錄合併了多張證書，
		/// 拆分為多筆 Entity 存入，並標記 IsMultiCertSource = true
		/// </summary>
		[MaxLength(150)]
		public string CertOrganicSn { get; set; } = string.Empty;

		[MaxLength(200)]
		public string Name { get; set; } = string.Empty;

		[MaxLength(300)]
		public string Address { get; set; } = string.Empty;

		[MaxLength(100)]
		public string Tel { get; set; } = string.Empty;

		/// <summary>
		/// 品項分類，可能為多值以頓號串接的長字串
		/// </summary>
		[Column(TypeName = "nvarchar(max)")]
		public string Products { get; set; } = string.Empty;

		[MaxLength(100)]
		public string BehaviorType { get; set; } = string.Empty;

		[MaxLength(150)]
		public string CompanyName { get; set; } = string.Empty;

		/// <summary>
		/// 證書效期。原始格式為 "yyyy/MM/dd" 字串，於 MapToEntity 轉型；
		/// 解析失敗時本欄位為 null，其餘欄位照常寫入（不比照 W21c 整筆跳過），
		/// 因本欄位非此 Entity 的核心識別依據
		/// </summary>
		public DateOnly? EffectiveDate { get; set; }

		/// <summary>
		/// 驗證狀態（通過／結束／終止）。查詢邏輯預設回傳完整歷史，不預設過濾特定狀態
		/// </summary>
		[MaxLength(50)]
		public string Status { get; set; } = string.Empty;


		/// <summary>
		/// 通過驗證的作物／產品品項，可能為極長字串（多值以頓號串接）
		/// </summary>
		[Column(TypeName = "nvarchar(max)")]
		public string ContainCrops { get; set; } = string.Empty;

		[MaxLength(300)]
		public string MailingAddress { get; set; } = string.Empty;

		/// <summary>
		/// 舊制證書字號，格式與 CertOrganicSn 不同（如 "TOC-C0417"），部分記錄為空字串
		/// </summary>
		[MaxLength(500)]
		public string OldCertOrganicSN { get; set; } = string.Empty;

		/// <summary>
		/// 標記此筆記錄是否來自 CertOrganicSn 異值並存的原始記錄拆分而來。
		/// 若為 true，代表 Products／ContainCrops 為未拆分的完整原始字串，
		/// 無法保證與本筆 CertOrganicSn 精確對應，僅供人工事後查證參考
		/// </summary>
		public bool IsMultiCertSource { get; set; }

		[Column(TypeName = "datetime2")]
		public DateTime SyncedAt { get; set; }
	}
}