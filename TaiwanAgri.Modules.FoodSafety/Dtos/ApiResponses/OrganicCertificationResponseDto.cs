namespace TaiwanAgri.Modules.FoodSafety.Dtos.ApiResponses
{
	public class OrganicCertificationResponseDto
	{
		public int Id { get; set; }
		public string CertOrganicSn { get; set; } = string.Empty;

		/// <summary>對應 Entity: Name（經營者名稱）</summary>
		public string OperatorName { get; set; } = string.Empty;

		public string Address { get; set; } = string.Empty;
		public string Tel { get; set; } = string.Empty;
		public string Products { get; set; } = string.Empty;
		public string BehaviorType { get; set; } = string.Empty;
		/// <summary>對應 Entity: CompanyName（驗證機構名稱）</summary>
		public string VerificationBodyName { get; set; } = string.Empty;
		public DateOnly? EffectiveDate { get; set; }
		public string Status { get; set; } = string.Empty;

		/// <summary>對應 Entity: ContainCrops（產品範圍）</summary>
		public string ProductScope { get; set; } = string.Empty;

		public string MailingAddress { get; set; } = string.Empty;

		/// <summary>對應 Entity: OldCertOrganicSN（舊制證書字號）</summary>
		public string LegacyCertNumber { get; set; } = string.Empty;

		/// <summary>
		/// 是否存在模糊的品項對應關係。
		/// 對應 Entity: IsMultiCertSource——為 true 時，Products／ProductScope
		/// 為未拆分的多證號合併原始字串，前端應顯示提示 badge，交由使用者自行判斷
		/// </summary>
		public bool HasAmbiguousProductMapping { get; set; }
	}
}
