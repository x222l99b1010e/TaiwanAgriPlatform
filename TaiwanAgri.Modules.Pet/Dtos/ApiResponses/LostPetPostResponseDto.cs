namespace TaiwanAgri.Modules.Pet.Dtos.ApiResponses
{
	/// <summary>不含 UserId——公開 API 不外露內部使用者識別碼，前端自行比對登入身分判斷是否為本人貼文</summary>
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
	}
}
