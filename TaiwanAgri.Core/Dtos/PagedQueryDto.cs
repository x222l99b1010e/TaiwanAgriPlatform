using System.ComponentModel.DataAnnotations;

namespace TaiwanAgri.Core.Dtos
{
	/// <summary>
	/// 所有分頁查詢的共用參數與界限。
	/// <para>
	/// 界限用 DataAnnotations 而不是在每個 Controller 動作裡手寫 if——
	/// 手寫版本原本在六個動作裡逐字重複，而且**新加的端點要靠下一個人記得複製貼上**；
	/// 掛在型別上則是「用了這個 DTO 就自動有界限」，[ApiController] 會在進入動作前擋下來回 400。
	/// </para>
	/// </summary>
	public class PagedQueryDto
	{
		/// <summary>頁碼，從 1 起算</summary>
		[Range(1, int.MaxValue, ErrorMessage = "頁碼必須大於 0")]
		public int Page { get; set; } = 1;

		/// <summary>
		/// 每頁筆數。上限 100 是防止單一請求把整張表撈走，
		/// 與各端點自己的資料量無關，所以放在共用基底而不是各自訂
		/// </summary>
		[Range(1, MaxPageSize, ErrorMessage = "每頁筆數必須大於 0 且小於等於 100")]
		public int PageSize { get; set; } = DefaultPageSize;

		public const int DefaultPageSize = 20;
		public const int MaxPageSize = 100;
	}
}
