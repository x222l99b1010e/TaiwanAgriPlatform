using TaiwanAgri.Core.Dtos;
using TaiwanAgri.Modules.Weather.Dtos.ApiRequests;
using TaiwanAgri.Modules.Weather.Dtos.ApiResponses;

namespace TaiwanAgri.Modules.Weather.Services
{
	/// <summary>
	/// 使用者自己的通知規則 CRUD，以及手動觸發一次評估。
	///
	/// <para>
	/// 每一支方法都收 userId 並把它寫進查詢條件，而不是先查出規則再比對擁有者——
	/// 後者要靠每一支方法都記得比對，漏掉的那一支不會有任何訊號，而且會是一個越權漏洞。
	/// 查不到與不是本人回同一種結果（false／null），呼叫端一律翻成 404：
	/// 兩種情況若回不同的狀態碼，等於告訴呼叫者「這個 Id 存在，只是不屬於你」。
	/// </para>
	/// </summary>
	public interface INotificationRuleService
	{
		/// <summary>該使用者的規則，新到舊。</summary>
		Task<PagedResult<NotificationRuleResponseDto>> GetRulesAsync(
			string userId, PagedQueryDto query, CancellationToken cancellationToken = default);

		/// <summary>單一規則；不存在或不屬於該使用者時回 null。</summary>
		Task<NotificationRuleResponseDto?> GetRuleByIdAsync(
			int id, string userId, CancellationToken cancellationToken = default);

		/// <summary>
		/// 建立規則。
		/// 已達規則數上限時丟 <see cref="RuleLimitExceededException"/>，
		/// 訊息可直接顯示給使用者。
		/// </summary>
		Task<NotificationRuleResponseDto> CreateRuleAsync(
			string userId, NotificationRuleRequestDto request, CancellationToken cancellationToken = default);

		/// <summary>
		/// 更新規則；不存在或不屬於該使用者時回 false。
		/// 新條件從此刻起往後套用——既有通知不刪、訊息不變、去重鍵延續。
		/// </summary>
		Task<bool> UpdateRuleAsync(
			int id, string userId, NotificationRuleRequestDto request, CancellationToken cancellationToken = default);

		/// <summary>
		/// 刪除規則；不存在或不屬於該使用者時回 false。
		/// 該規則產生的通知會由資料庫的外鍵連帶刪除。
		/// </summary>
		Task<bool> DeleteRuleAsync(int id, string userId, CancellationToken cancellationToken = default);

		/// <summary>
		/// 立刻評估這個使用者自己的規則，不等每日排程。
		/// 回傳值要讓呼叫端分得出「條件沒命中」與「資料不夠新」兩種空結果。
		/// </summary>
		Task<RuleEvaluationResponseDto> EvaluateNowAsync(string userId, CancellationToken cancellationToken = default);
	}
}
