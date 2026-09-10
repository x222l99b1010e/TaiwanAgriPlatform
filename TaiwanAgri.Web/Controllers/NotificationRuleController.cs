using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using TaiwanAgri.Core.Dtos;
using TaiwanAgri.Modules.Weather.Dtos.ApiRequests;
using TaiwanAgri.Modules.Weather.Services;
using TaiwanAgri.Web.Extensions;

namespace TaiwanAgri.Web.Controllers
{
	/// <summary>
	/// 使用者自己的通知規則。
	///
	/// <para>
	/// 與 <see cref="NotificationController"/>（讀通知、標記已讀）分開成兩支，
	/// 是因為既有的十支 controller 一律按資源分而不是按模組分——規則是獨立資源、有自己的 CRUD。
	/// 路由取 NotificationRule 而不是沿用 entity 的 PestRuleConfig：
	/// 那個 Pest 前綴是歷史命名（數值型的來源已改成氣象觀測），
	/// entity 因為改名要動資料表結構而維持不動，但 API 路由是對外契約，
	/// 沒有理由讓新的契約去繼承一個已知不精確的名字。
	/// </para>
	///
	/// <para>
	/// 請求的驗證全部由 <see cref="NotificationRuleRequestDto"/> 自己表達，
	/// [ApiController] 會在進入動作之前收集完並回 400，所以這裡看不到任何驗證程式碼。
	/// 唯一在這一層翻譯的是規則數上限——它要看資料庫狀態，請求本身看不出來。
	/// </para>
	/// </summary>
	[Route("api/[controller]")]
	[ApiController]
	[Authorize]
	public class NotificationRuleController : ControllerBase
	{
		private readonly INotificationRuleService _ruleService;

		public NotificationRuleController(INotificationRuleService ruleService)
		{
			_ruleService = ruleService;
		}

		// GET /api/NotificationRule?page=1&pageSize=20
		[HttpGet]
		public async Task<IActionResult> GetRules([FromQuery] PagedQueryDto query, CancellationToken cancellationToken = default)
		{
			var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
			if (userId is null) return Unauthorized();

			var result = await _ruleService.GetRulesAsync(userId, query, cancellationToken);
			return Ok(result);
		}

		// GET /api/NotificationRule/{id}
		[HttpGet("{id:int}")]
		public async Task<IActionResult> GetRuleById(int id, CancellationToken cancellationToken = default)
		{
			var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
			if (userId is null) return Unauthorized();

			var rule = await _ruleService.GetRuleByIdAsync(id, userId, cancellationToken);
			return rule is null ? NotFound() : Ok(rule);
		}

		// POST /api/NotificationRule
		[HttpPost]
		public async Task<IActionResult> CreateRule(
			[FromBody] NotificationRuleRequestDto request, CancellationToken cancellationToken = default)
		{
			var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
			if (userId is null) return Unauthorized();

			try
			{
				var rule = await _ruleService.CreateRuleAsync(userId, request, cancellationToken);
				return CreatedAtAction(nameof(GetRuleById), new { id = rule.Id }, rule);
			}
			catch (RuleLimitExceededException ex)
			{
				// 訊息由服務層帶上限值組好，這裡不重組一次——兩邊各寫一份的話，
				// 上限改了而訊息沒改的那一份會對使用者說錯數字
				return BadRequest(ex.Message);
			}
		}

		// PUT /api/NotificationRule/{id}
		[HttpPut("{id:int}")]
		public async Task<IActionResult> UpdateRule(
			int id, [FromBody] NotificationRuleRequestDto request, CancellationToken cancellationToken = default)
		{
			var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
			if (userId is null) return Unauthorized();

			// 查不到與不是本人都回 404：分成 404 與 403 的話，403 等於告訴呼叫者
			// 「這個 Id 存在，只是不屬於你」，那本身就是一則不該外流的資訊
			var updated = await _ruleService.UpdateRuleAsync(id, userId, request, cancellationToken);
			return updated ? NoContent() : NotFound();
		}

		// DELETE /api/NotificationRule/{id}
		[HttpDelete("{id:int}")]
		public async Task<IActionResult> DeleteRule(int id, CancellationToken cancellationToken = default)
		{
			var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
			if (userId is null) return Unauthorized();

			var deleted = await _ruleService.DeleteRuleAsync(id, userId, cancellationToken);
			return deleted ? NoContent() : NotFound();
		}

		/// <summary>
		/// 立即檢查：不等每天一次的排程，馬上評估這個使用者自己的規則。
		/// 存在的理由是使用者剛建完規則會想知道結果，而排程要等到明天。
		/// 掛限流是因為每按一次就要對氣象觀測表掃一輪，而按鈕本身沒有任何成本。
		/// </summary>
		// POST /api/NotificationRule/evaluate
		[HttpPost("evaluate")]
		[EnableRateLimiting(RateLimitingExtensions.RuleEvaluationPolicy)]
		public async Task<IActionResult> EvaluateNow(CancellationToken cancellationToken = default)
		{
			var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
			if (userId is null) return Unauthorized();

			var outcome = await _ruleService.EvaluateNowAsync(userId, cancellationToken);
			return Ok(outcome);
		}
	}
}
