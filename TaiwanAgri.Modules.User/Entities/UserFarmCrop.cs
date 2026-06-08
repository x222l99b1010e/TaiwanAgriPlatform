using System.ComponentModel.DataAnnotations;

namespace TaiwanAgri.Modules.User.Entities
{
	public class UserFarmCrop
	{
		// 自動遞增 PK
		[Key]
		public int Id { get; set; }
		// 物理 FK → UserFarmProfile.UserId（同一個 DbContext）
		[Required, MaxLength(450)]
		public string UserId { get; set; } = string.Empty;
		// 邏輯 FK → CropInfos.CropCode（跨 DbContext，純字串）
		[Required, MaxLength(10)]
		public string CropCode { get; set; } = string.Empty;
		// 快照：同步時存下來，避免跨 DbContext JOIN
		[MaxLength(50)]
		public string? CropName { get; set; }
		

		// 導覽屬性（物理 FK 在同一 DbContext，可以建立）
		public UserFarmProfile UserFarmProfile { get; set; } = null!;
	}
}
