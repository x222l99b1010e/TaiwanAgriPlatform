using System.ComponentModel.DataAnnotations;
using TaiwanAgri.Core.Entities;

namespace TaiwanAgri.Modules.Weather.Entities
{
	public class UserNotification
	{
		[Key]
		public int Id { get; set; } //PK
		[Required, MaxLength(450)]
		public string UserId { get; set; } = string.Empty; //FK → AspNetUsers
		[Required]
		public int PestRuleConfigId { get; set; } //FK → PestRuleConfig
		public int? SourceRecordId { get; set; } // 觸發這筆通知的來源記錄 Id
		[MaxLength(500)]
		public string Message { get; set; } = string.Empty; //觸發當下組好的通知內容快照
		[Required]
		public DateTime TriggeredAt { get; set; } //通知觸發時間
		public DateTime? ExpireAt { get; set; } //通知過期時間（事件型規則用）
		public bool IsRead { get; set; } //是否已讀
		public DateTime CreatedAt { get; set; } //通知建立時間
		public PestRuleConfig PestRuleConfig { get; set; } = null!; // 導覽屬性，由 EF 於載入時填入
	}
}
