namespace TaiwanAgri.Modules.Weather.Dtos.ApiResponses
{
	public class UserNotificationResponseDto
	{
		public int Id { get; set; }
		public string Message { get; set; } = string.Empty;
		public string RuleName { get; set; } = string.Empty; // 來自 PestRuleConfig.RuleName
		public DateTime TriggeredAt { get; set; }
		public bool IsRead { get; set; }
	}
}
