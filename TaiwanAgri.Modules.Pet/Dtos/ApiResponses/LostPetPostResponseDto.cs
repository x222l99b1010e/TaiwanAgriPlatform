namespace TaiwanAgri.Modules.Pet.Dtos.ApiResponses
{
	/// <summary>
	/// 不含 UserId——公開 API 不外露內部使用者識別碼。
	/// 「這是不是本人貼文」改由後端在查詢當下算好、以 <see cref="IsOwner"/> 布林值回傳，
	/// 前端不需要（也拿不到）自己的 UserId 去比對——這是 W23 前端串接時修正的設計缺口：
	/// 原註解曾寫「前端自行比對登入身分」，但 DTO 從未帶識別欄位、前端 authStore 也未存 UserId，
	/// 該行為在舊版資料形狀下無法實現（詳見 DevLog 條目 291）。
	/// </summary>
	public class LostPetPostResponseDto
	{
		public int Id { get; set; }
		public string Title { get; set; } = string.Empty;
		public string Description { get; set; } = string.Empty;
		public string County { get; set; } = string.Empty;
		public string Phone { get; set; } = string.Empty;
		public string Email { get; set; } = string.Empty;
		public string PhotoUrl { get; set; } = string.Empty;
		public decimal? Latitude { get; set; }
		public decimal? Longitude { get; set; }
		public string Status { get; set; } = string.Empty;
		public DateTime CreatedAt { get; set; }
		public DateTime UpdatedAt { get; set; }

		/// <summary>目前登入者是否為本篇作者；未登入時一律 false。前端只需依此顯示編輯／刪除按鈕，不做任何比對</summary>
		public bool IsOwner { get; set; }
	}
}
