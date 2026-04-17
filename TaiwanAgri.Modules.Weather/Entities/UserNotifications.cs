using System.ComponentModel.DataAnnotations;
using TaiwanAgri.Core.Entities;

namespace TaiwanAgri.Modules.Weather.Entities
{
	public class UserNotifications
	{
		[Key]
		public int Id { get; set; } //PK
		[Required, MaxLength(450)]
		public string UserId { get; set; } //FK → AspNetUsers
		[Required]
		public int PestRuleConfigId { get; set; } //FK → PestRuleConfig
		[MaxLength(500)]
		public string Message { get; set; } //觸發當下組好的通知內容快照
		[Required]
		public DateTime TriggeredAt { get; set; } //通知觸發時間
		public DateTime? ExpireAt { get; set; } //通知過期時間（事件型規則用）
		public bool IsRead { get; set; } //是否已讀
		public DateTime CreatedAt { get; set; } //通知建立時間

		public ApplicationUser User { get; set; } // 導覽屬性，直接引用現有類別
		public PestRuleConfig PestRuleConfig { get; set; } // 導覽屬性，直接引用現有類別
	}
}
