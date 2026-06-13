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
		 //Snapshot: intentionally denormalized, not a FK join。
		// 快照欄位：CropName 來自 MarketDbContext 的 CropInfos
		// 跨 DbContext 無法 JOIN，故在寫入時複製一份到 UserDbContext
		// 代價是資料可能與來源略有落差，但農產品名稱極少變動，可接受
		[MaxLength(50)]
		public string? CropName { get; set; }
		

		// 導覽屬性（物理 FK 在同一 DbContext，可以建立）
		public UserFarmProfile UserFarmProfile { get; set; } = null!;
	}
}
