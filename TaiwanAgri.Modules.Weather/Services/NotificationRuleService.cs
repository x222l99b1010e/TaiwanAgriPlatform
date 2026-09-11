using Microsoft.EntityFrameworkCore;
using TaiwanAgri.Core.Dtos;
using TaiwanAgri.Modules.Weather.Constants;
using TaiwanAgri.Modules.Weather.Data;
using TaiwanAgri.Modules.Weather.Dtos.ApiRequests;
using TaiwanAgri.Modules.Weather.Dtos.ApiResponses;
using TaiwanAgri.Modules.Weather.Entities;

namespace TaiwanAgri.Modules.Weather.Services
{
	public class NotificationRuleService : INotificationRuleService
	{
		private readonly WeatherDbContext _context;
		private readonly PestRuleEngine _ruleEngine;
		private readonly TimeProvider _timeProvider;

		public NotificationRuleService(WeatherDbContext context, PestRuleEngine ruleEngine, TimeProvider timeProvider)
		{
			_context = context;
			_ruleEngine = ruleEngine;
			_timeProvider = timeProvider;
		}

		public async Task<PagedResult<NotificationRuleResponseDto>> GetRulesAsync(
			string userId, PagedQueryDto query, CancellationToken cancellationToken = default)
		{
			var rules = _context.PestRuleConfigs.AsNoTracking().Where(p => p.UserId == userId);

			var totalCount = await rules.CountAsync(cancellationToken);
			var page = await rules
				// Id 當第二排序鍵：同一秒內建立的兩條規則若只比 CreatedAt，
				// 回傳順序沒有承諾，同一筆可能在兩頁各出現一次
				.OrderByDescending(p => p.CreatedAt)
				.ThenByDescending(p => p.Id)
				.Skip((query.Page - 1) * query.PageSize)
				.Take(query.PageSize)
				.ToListAsync(cancellationToken);

			var notificationCounts = await CountNotificationsAsync(
				page.Select(p => p.Id).ToList(), cancellationToken);

			var items = page
				.Select(p => MapToResponseDto(p, notificationCounts.GetValueOrDefault(p.Id)))
				.ToList();

			return PagedResult<NotificationRuleResponseDto>.Create(items, totalCount, query.Page, query.PageSize);
		}

		public async Task<NotificationRuleResponseDto?> GetRuleByIdAsync(
			int id, string userId, CancellationToken cancellationToken = default)
		{
			var rule = await _context.PestRuleConfigs
				.AsNoTracking()
				.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, cancellationToken);
			if (rule is null) return null;

			var notificationCounts = await CountNotificationsAsync([rule.Id], cancellationToken);
			return MapToResponseDto(rule, notificationCounts.GetValueOrDefault(rule.Id));
		}

		public async Task<NotificationRuleResponseDto> CreateRuleAsync(
			string userId, NotificationRuleRequestDto request, CancellationToken cancellationToken = default)
		{
			// 在任何寫入之前就擋下來，這樣 catch 得到的例外只可能是這一種
			var ruleCount = await _context.PestRuleConfigs.CountAsync(p => p.UserId == userId, cancellationToken);
			if (ruleCount >= NotificationRule.Limits.MaxRulesPerUser)
				throw new RuleLimitExceededException(
					$"已達規則數上限 {NotificationRule.Limits.MaxRulesPerUser} 條，請先刪除其他規則");

			var rule = new PestRuleConfig
			{
				UserId = userId,
				CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
			};
			ApplyRequest(rule, request);

			_context.PestRuleConfigs.Add(rule);
			await _context.SaveChangesAsync(cancellationToken);

			// 剛建立的規則還沒被評估過，一定是零則通知
			return MapToResponseDto(rule, notificationCount: 0);
		}

		public async Task<bool> UpdateRuleAsync(
			int id, string userId, NotificationRuleRequestDto request, CancellationToken cancellationToken = default)
		{
			// 這裡不加 AsNoTracking：要靠變更追蹤把修改寫回去
			var rule = await _context.PestRuleConfigs
				.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, cancellationToken);
			if (rule is null) return false;

			ApplyRequest(rule, request);
			await _context.SaveChangesAsync(cancellationToken);
			return true;
		}

		public async Task<bool> DeleteRuleAsync(int id, string userId, CancellationToken cancellationToken = default)
		{
			var rule = await _context.PestRuleConfigs
				.FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, cancellationToken);
			if (rule is null) return false;

			// 這條規則產生的通知由外鍵的 CASCADE 連帶刪除，不在這裡逐筆刪——
			// 「每筆通知都對應到一條存在的規則」這個不變式因此由資料庫維護，
			// 不依賴每一條刪除路徑的作者都記得處理通知
			_context.PestRuleConfigs.Remove(rule);
			await _context.SaveChangesAsync(cancellationToken);
			return true;
		}

		public async Task<RuleEvaluationResponseDto> EvaluateNowAsync(
			string userId, CancellationToken cancellationToken = default)
		{
			// 一定要帶 userId：一個使用者按「立即檢查」不該讓別人的規則跟著跑，
			// 而且逐條規則的例外會離開整支評估方法，限定範圍等於把影響收在呼叫者身上
			var outcome = await _ruleEngine.EvaluateAsync(userId, cancellationToken);

			return new RuleEvaluationResponseDto
			{
				RulesEvaluated = outcome.RulesEvaluated,
				NotificationsCreated = outcome.NotificationsCreated,
				LatestObservedAt = outcome.LatestObservedAt,
				HasFreshObservation = outcome.HasFreshObservation,
				NumericRulesEvaluated = outcome.NumericRulesEvaluated,
				NumericRulesWithNewObservations = outcome.NumericRulesWithNewObservations
			};
		}

		/// <summary>
		/// 規則 Id → 該規則已產生的通知則數。查不到的規則不會出現在字典裡（零則）。
		/// 分成獨立一次查詢而不是寫進投影的子查詢：一個使用者最多七條規則，
		/// 多這一次往返可以忽略，換來的是查詢形狀單純、每個資料庫提供者都翻得動。
		/// </summary>
		private async Task<Dictionary<int, int>> CountNotificationsAsync(
			List<int> ruleIds, CancellationToken cancellationToken)
		{
			if (ruleIds.Count == 0) return [];

			var counts = await _context.UserNotifications
				.AsNoTracking()
				.Where(n => ruleIds.Contains(n.PestRuleConfigId))
				.GroupBy(n => n.PestRuleConfigId)
				.Select(g => new { RuleId = g.Key, Count = g.Count() })
				.ToListAsync(cancellationToken);

			return counts.ToDictionary(x => x.RuleId, x => x.Count);
		}

		/// <summary>
		/// 把請求套進規則。建立與更新共用同一段，兩邊各寫一次的話，
		/// 日後改欄位時漏改的那一邊會安靜地少存一個值。
		/// </summary>
		private static void ApplyRequest(PestRuleConfig rule, NotificationRuleRequestDto request)
		{
			var isNumeric = request.RuleType == NotificationRule.Types.Numeric;

			rule.RuleName = request.RuleName.Trim();
			rule.RuleType = request.RuleType;
			rule.SourceTable = request.SourceTable;
			rule.IsActive = request.IsActive;

			// 沒帶保留天數時依型態補預設。型別若是不可為 null 的 int，
			// 漏帶會變成 0，通知一產生就過期、下一輪評估直接刪掉，而使用者只會覺得通知沒來
			rule.ExpiryDays = request.ExpiryDays ?? NotificationRule.Limits.DefaultExpiryDays(request.RuleType);

			rule.FilterCity = Normalize(request.FilterCity);

			// 另一個型態專用的欄位一律清成 null，而不是照收。
			// 引擎本來就不會讀它們，所以照收不會出錯；清掉是為了讓「這一組欄位對這條規則有沒有意義」
			// 從資料本身就看得出來，下一個查資料庫的人不必先知道型態才讀得懂那幾格
			rule.FilterPlantName = isNumeric ? null : Normalize(request.FilterPlantName);
			rule.FilterDateFrom = isNumeric ? null : request.FilterDateFrom;

			rule.MetricName = isNumeric ? request.MetricName : null;
			rule.Comparison = isNumeric ? request.Comparison : null;
			rule.Threshold = isNumeric ? request.Threshold : null;

			// 水位是數值型專用欄位，非數值型一律為 null。
			// 同型態內改條件時水位不動——重置等於往前追，把已經看過的觀測重新灌成一批通知，
			// 而那正是水位要解決的問題。改成事件型再改回來時水位是 null，
			// 下次評估退到最後一批，是兩者之中較保守的那一邊
			rule.LastEvaluatedAt = isNumeric ? rule.LastEvaluatedAt : null;
		}

		/// <summary>空白字串與 null 在這裡是同一件事（都代表「不限這個條件」），統一存成 null。</summary>
		private static string? Normalize(string? value)
			=> string.IsNullOrWhiteSpace(value) ? null : value.Trim();

		private static NotificationRuleResponseDto MapToResponseDto(PestRuleConfig rule, int notificationCount)
			=> new()
			{
				Id = rule.Id,
				RuleName = rule.RuleName,
				RuleType = rule.RuleType,
				SourceTable = rule.SourceTable,
				IsActive = rule.IsActive,
				ExpiryDays = rule.ExpiryDays,
				CreatedAt = rule.CreatedAt,
				FilterCity = rule.FilterCity,
				FilterPlantName = rule.FilterPlantName,
				FilterDateFrom = rule.FilterDateFrom,
				MetricName = rule.MetricName,
				Comparison = rule.Comparison,
				Threshold = rule.Threshold,
				NotificationCount = notificationCount
			};
	}
}
