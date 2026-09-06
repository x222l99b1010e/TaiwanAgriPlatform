using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TaiwanAgri.Modules.Pet.Entities.Enums;

namespace TaiwanAgri.Modules.Pet.Entities
{
	/// <summary>
	/// 自建遺失啟事，使用者登入後可自行張貼與管理（CRUD）。
	/// 跟 OfficialLostPetPost（PetLoseList 官方唯讀同步）是兩張不同的表——
	/// 這張沒有外部業務鍵，Id 純粹是自建資料的代理鍵。
	/// </summary>
	public class LostPetPost
	{
		public int Id { get; set; }

		/// <summary>邏輯 FK → AspNetUsers（跨 DbContext 只能邏輯 FK，無導覽屬性），比照 UserWatchlist.UserId</summary>
		[Required, StringLength(450)]
		public string UserId { get; set; } = string.Empty;

		[Required, MaxLength(100)]
		public string Title { get; set; } = string.Empty;

		[Required, MaxLength(2000)]
		public string Description { get; set; } = string.Empty;

		[MaxLength(20)]
		public string County { get; set; } = string.Empty;

		/// <summary>未填時是空字串（不是 null）；需與 Email 至少填一個，此規則在 Controller 驗證，不在 Entity 層強制</summary>
		[MaxLength(50)]
		public string Phone { get; set; } = string.Empty;

		[MaxLength(254)]
		public string Email { get; set; } = string.Empty;

		/// <summary>使用者貼外部圖床連結，不做檔案上傳；長度不可預期，比照 OfficialLostPetPost.PictureUrl</summary>
		[Column(TypeName = "nvarchar(max)")]
		public string PhotoUrl { get; set; } = string.Empty;

		/// <summary>前端 Leaflet 點地圖直接取得，不做地址地理編碼（已驗證 Nominatim 對台灣地址不可行）</summary>
		[Column(TypeName = "decimal(10,6)")]
		public decimal? Latitude { get; set; }

		[Column(TypeName = "decimal(10,6)")]
		public decimal? Longitude { get; set; }

		public LostPetPostStatus Status { get; set; }

		[Column(TypeName = "datetime2")]
		public DateTime CreatedAt { get; set; }

		[Column(TypeName = "datetime2")]
		public DateTime UpdatedAt { get; set; }
	}
}
