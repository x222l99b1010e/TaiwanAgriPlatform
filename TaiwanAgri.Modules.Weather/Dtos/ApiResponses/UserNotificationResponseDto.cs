using TaiwanAgri.Core.Helpers;

namespace TaiwanAgri.Modules.Weather.Dtos.ApiResponses
{
	public class UserNotificationResponseDto
	{
		public int Id { get; set; }
		public string Message { get; set; } = string.Empty;
		public string RuleName { get; set; } = string.Empty; // 來自 PestRuleConfig.RuleName
		// 存的是 UTC，讀回來不帶標記，在這裡補上（原因見 UtcTimestamp）
		private DateTime _triggeredAt;
		public DateTime TriggeredAt { get => _triggeredAt; set => _triggeredAt = UtcTimestamp.MarkUtc(value); }
		public bool IsRead { get; set; }
	}
}
