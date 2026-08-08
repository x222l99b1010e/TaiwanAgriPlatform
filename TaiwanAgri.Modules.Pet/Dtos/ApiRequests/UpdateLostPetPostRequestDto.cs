using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
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

		/// <summary>
		/// 專案未全域註冊 JsonStringEnumConverter，System.Text.Json 反序列化 [FromBody] 時
		/// enum 屬性預設只吃數字（0/1/2），字串會直接丟例外、回 400。這裡單獨標註轉換器，
		/// 讓 Status 跟其他 enum 一樣「一律用字串」（W23 前端串接時發現，跟 IsOwner 同一個根因家族：
		/// 查詢參數的 Model Binding 是另一套邏輯、本來就吃字串，所以 W22 測試沒抓到這個缺口）
		/// </summary>
		[Required, JsonConverter(typeof(JsonStringEnumConverter))]
		public LostPetPostStatus Status { get; set; }
	}
}
