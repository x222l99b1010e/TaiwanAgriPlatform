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

		/// <summary>
		/// 外部圖床連結，可以不填。上限 1200 字跟前端表單的 maxlength 一致；
		/// 原本沒有任何驗證、欄位又是 nvarchar(max)，直接打 API 可以塞幾十 MB（2026-11 總檢查資安 A4）
		/// </summary>
		[MaxLength(1200, ErrorMessage = "照片網址最多 1200 個字元"), OptionalHttpUrl]
		public string PhotoUrl { get; set; } = string.Empty;

		// 座標欄位是 decimal(10,6)，整數部分只容得下 4 位，超出會在寫入時變成 500；
		// 用經緯度本來的範圍擋在驗證這一層，回 400
		[Range(-90, 90, ErrorMessage = "緯度必須介於 -90 到 90")]
		public decimal? Latitude { get; set; }
		[Range(-180, 180, ErrorMessage = "經度必須介於 -180 到 180")]
		public decimal? Longitude { get; set; }
	}
}
