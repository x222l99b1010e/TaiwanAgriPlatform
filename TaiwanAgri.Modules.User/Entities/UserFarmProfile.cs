using System.ComponentModel.DataAnnotations;

namespace TaiwanAgri.Modules.User.Entities
{
	public class UserFarmProfile
	{
		// PK = 邏輯 FK → AspNetUsers.Id
		// 一個 UserId 只能有一筆，由 PK 保證唯一性
		[Key]
		[MaxLength(450)]
		public string UserId { get; set; } = string.Empty; //FK → AspNetUsers
		// 農場所在縣市，例如 "台北市"、"南投縣"
		[MaxLength(20)]
		public string? FarmCity { get; set; }
		// 農場類型，例如 "蔬菜"、"果樹"、"花卉"
		[MaxLength(20)]
		public string? FarmType { get; set; }
		// 第一次儲存的時間，Upsert 時只在新增時設定
		public DateTime CreatedAt { get; set; }
		// 每次儲存都更新
		public DateTime UpdatedAt { get; set; }

	}
}
