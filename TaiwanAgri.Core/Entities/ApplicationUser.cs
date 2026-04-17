using Microsoft.AspNetCore.Identity;

namespace TaiwanAgri.Core.Entities
{
	public class ApplicationUser : IdentityUser
	{
		// 顯示名稱，例如農場主人或消費者的名字
		public string? DisplayName { get; set; }
		// 偏好縣市，用於個人化通知篩選（例如：只顯示台北市的病蟲害警示）
		public string? PreferredCity { get; set; }
		// 使用者類型，例如 "Farmer" / "Consumer" / "Researcher"
		public string? UserType { get; set; }
	}
}