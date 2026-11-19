using TaiwanAgri.Core.Dtos;

namespace TaiwanAgri.Modules.Weather.Dtos.Queries
{
	/// <summary>
	/// 病蟲害警報列表的查詢條件。分頁參數繼承共用的 <see cref="PagedQueryDto"/>（頁碼至少 1、每頁最多 100）：
	/// 原本這支端點收兩個沒有界限的整數，page=0 回 500、pageSize=100000 一次回整張表，
	/// 而且它免登入、沒有限流（2026-11 總檢查 3-A7）
	/// </summary>
	public class PestAlertQueryDto : PagedQueryDto
	{
		/// <summary>縣市，「臺」「台」兩種寫法都可以；不帶＝全台</summary>
		public string? CityName { get; set; }
	}
}
