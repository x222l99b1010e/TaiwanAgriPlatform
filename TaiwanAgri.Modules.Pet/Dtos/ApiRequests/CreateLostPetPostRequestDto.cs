using System.ComponentModel.DataAnnotations;

namespace TaiwanAgri.Modules.Pet.Dtos.ApiRequests
{
	public class CreateLostPetPostRequestDto
	{
		[Required, MaxLength(100)]
		public string Title { get; set; } = string.Empty;

		[Required, MaxLength(2000)]
		public string Description { get; set; } = string.Empty;

		[MaxLength(20)]
		public string County { get; set; } = string.Empty;

		/// <summary>Phone／Email 至少填一個，於 Controller 驗證（跨欄位規則，不適合用單一 DataAnnotation 表達）</summary>
		[MaxLength(50)]
		public string Phone { get; set; } = string.Empty;

		[MaxLength(254)]
		public string Email { get; set; } = string.Empty;

		public string PhotoUrl { get; set; } = string.Empty;

		public decimal? Latitude { get; set; }
		public decimal? Longitude { get; set; }
	}
}
