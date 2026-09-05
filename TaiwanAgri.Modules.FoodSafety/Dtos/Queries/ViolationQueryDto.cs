using TaiwanAgri.Core.Dtos;

namespace TaiwanAgri.Modules.FoodSafety.Dtos.Queries
{
	public class ViolationQueryDto : PagedQueryDto
	{
		/// <summary>取樣日期回溯天數；上限驗證（3650）在 Controller，與其他查詢一致</summary>
		public int Days { get; set; } = 90;

		/// <summary>檢驗結果精確過濾；null 或空白視同未過濾</summary>
		public string? InspectResult { get; set; }
	}
}
