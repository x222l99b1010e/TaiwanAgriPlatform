using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using System.Threading.RateLimiting;

namespace TaiwanAgri.Web.Extensions
{
	/// <summary>
	/// API 的請求速率限制。三個策略，擋的東西不同。
	///
	/// <para>
	/// <b>公開查詢（免登入的查詢端點）</b>：這些端點刻意設計成免登入（資料本身是公開的，
	/// 原始來源也不需要驗證），但「不需要身分」不等於「可以無限次呼叫」。沒有限流時，
	/// 一支腳本就能持續打同一組端點；而 GetPricesAsync 的快取鍵包含使用者可控的
	/// 作物代碼組合與日期區間，所以受害的不只是資料庫負載，
	/// 還包括被任意參數組合灌滿的 Redis——快取層本身變成攻擊面。
	/// 沒有身分可以分割，只能用來源位址：擋得住單一來源的腳本，擋不住分散來源。
	/// 原本只掛在行情控制器上，病蟲害、寵物、食安、氣象、導覽這幾支同樣免登入的控制器沒有掛
	/// （追溯碼與農藥查詢每打一次就對農業部送好幾個請求，2026-11 總檢查資安 A5、B1）；
	/// 現在五個免登入的控制器都掛。計數以「來源位址＋模組」分開（/api/market、/api/pest……各一份），
	/// 一個模組的額度跟擴大範圍之前的行情端點一樣，正常瀏覽在幾個模組之間切換不會互相吃掉額度。
	/// </para>
	///
	/// <para>
	/// <b>登入與註冊</b>：以來源位址計數、額度最緊。登入另有帳號鎖定（同一個帳號連錯 5 次鎖 5 分鐘），
	/// 但鎖定擋不住「每個帳號試幾次」的撒網，也擋不住腳本大量註冊帳號
	/// </para>
	///
	/// <para>
	/// <b>手動觸發規則評估</b>：這一支要登入，所以改用使用者識別碼分割——
	/// 用來源位址的話，同一個出口 IP 後面的所有使用者會共用同一份額度，
	/// 一個人狂按會把其他人一起擋掉。額度也小得多：按一次就要對氣象觀測表掃一輪，
	/// 而它是每小時寫入約 876 筆、保留 30 天的滾動視窗；
	/// 這個按鈕的正常用法是「剛建完規則想馬上看結果」，一分鐘按不到五次。
	/// </para>
	///
	/// <para>
	/// 兩個策略都用 in-memory 的計數器：單一執行個體部署時正確，
	/// 多執行個體時每台各記各的，實際上限會變成「設定值 × 台數」。
	/// 要跨執行個體共用計數需要改走 Redis，那是部署形態確定之後才需要決定的事，已記為技術債
	/// </para>
	/// </summary>
	public static class RateLimitingExtensions
	{
		/// <summary>公開查詢端點共用的限流策略名稱</summary>
		public const string PublicQueryPolicy = "public-query";

		/// <summary>手動觸發規則評估的限流策略名稱</summary>
		public const string RuleEvaluationPolicy = "rule-evaluation";

		/// <summary>登入與註冊的限流策略名稱</summary>
		public const string AuthPolicy = "auth";

		/// <summary>
		/// 公開查詢的計數分割鍵：來源位址＋路徑的模組段（/api/Market/prices → market）。
		/// 大小寫不分：路由不分大小寫，分大小寫的話換個寫法就多一份額度
		/// </summary>
		public static string PublicQueryPartitionKey(string? remoteIp, PathString path)
		{
			var segments = (path.Value ?? string.Empty).Split('/', StringSplitOptions.RemoveEmptyEntries);
			var module = segments.Length >= 2 ? segments[1].ToLowerInvariant() : string.Empty;
			return $"{remoteIp ?? "unknown"}|{module}";
		}

		public static IServiceCollection AddApiRateLimiting(
			this IServiceCollection services, IConfiguration configuration)
		{
			var options = new RateLimitOptions();
			configuration.GetSection(RateLimitOptions.SectionName).Bind(options);
			services.Configure<RateLimitOptions>(configuration.GetSection(RateLimitOptions.SectionName));

			services.AddRateLimiter(limiter =>
			{
				// 預設是 503，也就是「擋下來但看起來像伺服器掛了」。
				// 429 才說得出「你被限流了、等一下再來」這件事，
				// 呼叫端才有辦法分辨自己是被擋還是打壞了什麼
				limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

				limiter.AddPolicy(PublicQueryPolicy, context =>
					RateLimitPartition.GetFixedWindowLimiter(
						// 免登入端點沒有使用者身分可以分割，只能用來源位址（再依模組分開，見 PublicQueryPartitionKey）。
						// 這擋得住單一來源的腳本，擋不住分散來源——後者需要的是另一個層級的防護
						partitionKey: PublicQueryPartitionKey(context.Connection.RemoteIpAddress?.ToString(), context.Request.Path),
						factory: _ => new FixedWindowRateLimiterOptions
						{
							PermitLimit = options.PermitLimit,
							Window = TimeSpan.FromSeconds(options.WindowSeconds),
							// 不排隊：查詢端點的請求排隊等於讓使用者盯著轉圈圈，
							// 直接回 429 讓前端決定要重試還是提示，比較誠實
							QueueLimit = 0
						}));

				limiter.AddPolicy(RuleEvaluationPolicy, context =>
					RateLimitPartition.GetFixedWindowLimiter(
						// 這支端點掛了 [Authorize]，所以正常情況一定拿得到識別碼；
						// 退回來源位址只是不讓分割鍵變成同一個值——那會讓全站共用一份額度
						partitionKey: context.User.FindFirstValue(ClaimTypes.NameIdentifier)
							?? context.Connection.RemoteIpAddress?.ToString()
							?? "unknown",
						factory: _ => new FixedWindowRateLimiterOptions
						{
							PermitLimit = options.RuleEvaluationPermitLimit,
							Window = TimeSpan.FromSeconds(options.RuleEvaluationWindowSeconds),
							QueueLimit = 0
						}));

				limiter.AddPolicy(AuthPolicy, context =>
					RateLimitPartition.GetFixedWindowLimiter(
						partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
						factory: _ => new FixedWindowRateLimiterOptions
						{
							PermitLimit = options.AuthPermitLimit,
							Window = TimeSpan.FromSeconds(options.AuthWindowSeconds),
							QueueLimit = 0
						}));

				// 被擋下來時附上可以重試的時間，呼叫端才不必猜
				limiter.OnRejected = async (context, cancellationToken) =>
				{
					if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
					{
						context.HttpContext.Response.Headers.RetryAfter =
							((int)retryAfter.TotalSeconds).ToString();
					}

					await context.HttpContext.Response.WriteAsync(
						"請求過於頻繁，請稍後再試", cancellationToken);
				};
			});

			return services;
		}
	}

	/// <summary>
	/// 限流參數。與 MarketQueryOptions 分開是因為兩者的層級不同——
	/// 這一組管的是中介層每個呼叫者能打幾次，跟任何一個模組的查詢語意無關
	/// </summary>
	public class RateLimitOptions
	{
		public const string SectionName = "RateLimiting";

		/// <summary>公開查詢：一個時間窗內允許的請求數。行情頁一次載入會打兩支 API，
		/// 六十次對正常操作綽綽有餘，對腳本則是有效上限</summary>
		public int PermitLimit { get; set; } = 60;

		/// <summary>公開查詢：時間窗長度（秒）</summary>
		public int WindowSeconds { get; set; } = 60;

		/// <summary>
		/// 手動觸發規則評估：一個時間窗內允許的次數。
		/// 比公開查詢緊得多，因為每一次都要對氣象觀測表掃一輪；
		/// 正常用法是建完規則按一次看結果，一分鐘五次已經留了重試的餘裕
		/// </summary>
		public int RuleEvaluationPermitLimit { get; set; } = 5;

		/// <summary>手動觸發規則評估：時間窗長度（秒）</summary>
		public int RuleEvaluationWindowSeconds { get; set; } = 60;

		/// <summary>
		/// 登入與註冊（合計）：一個時間窗內允許的次數。正常使用一分鐘頂多登入、打錯重試幾次；
		/// 十次留了餘裕，又讓腳本一分鐘只能試十組帳密、建十個帳號
		/// </summary>
		public int AuthPermitLimit { get; set; } = 10;

		/// <summary>登入與註冊：時間窗長度（秒）</summary>
		public int AuthWindowSeconds { get; set; } = 60;
	}
}
