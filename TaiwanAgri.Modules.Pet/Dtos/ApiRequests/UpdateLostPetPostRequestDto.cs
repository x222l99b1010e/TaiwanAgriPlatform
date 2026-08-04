using System.ComponentModel.DataAnnotations;
using TaiwanAgri.Modules.Pet.Entities.Enums;

namespace TaiwanAgri.Modules.Pet.Dtos.ApiRequests
{
	public class UpdateLostPetPostRequestDto
	{
		[Required, MaxLength(100)]
		public string Title { get; set; } = string.Empty;

		[Required, MaxLength(2000)]
		public string Description { get; set; } = string.Empty;

		[MaxLength(20)]
		public string County { get; set; } = string.Empty;

		/// <summary>Phone／Email 至少填一個，於 Controller 驗證</summary>
		[MaxLength(50)]
		public string Phone { get; set; } = string.Empty;

		[MaxLength(254)]
		public string Email { get; set; } = string.Empty;

		public string PhotoUrl { get; set; } = string.Empty;

		public decimal? Latitude { get; set; }
		public decimal? Longitude { get; set; }

		[Required]
		public LostPetPostStatus Status { get; set; }
	}
}
