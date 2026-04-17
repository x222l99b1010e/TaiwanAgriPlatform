using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using TaiwanAgri.Core.Entities;

namespace TaiwanAgri.Modules.Weather.Entities
{
	public class PestRuleConfig
	{
		[Key]
		public int Id { get; set; } //PK
		[Required, MaxLength(450)]
		public string UserId { get; set; } //FK → AspNetUsers
		[Required, MaxLength(100)]
		public string RuleName { get; set; } //使用者自訂規則名稱
		[Required, MaxLength(20)]
		public string RuleType { get; set; } //"Numeric" / "Event"
		[Required, MaxLength(50)]
		public string SourceTable { get; set; } //"PestDecade" / "PlantEpidemic" / "TreePest"
		public bool IsActive { get; set; } //規則是否啟用
		public int ExpiryDays { get; set; } //事件型：幾天後自動關閉通知
		public int? Threshold { get; set; } //數值型閾值
		[MaxLength]
		public string? FilterJson { get; set; } //事件型過濾條件
		public DateTime CreatedAt { get; set; } //規則建立時間

		public ApplicationUser User { get; set; } // 導覽屬性，直接引用現有類別

	}
}
