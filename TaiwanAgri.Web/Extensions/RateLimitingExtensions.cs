using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

namespace TaiwanAgri.Web.Extensions
{
	/// <summary>
	/// 公開查詢端點的請求速率限制。
	/// <para>
	/// 行情類端點刻意設計成免登入（資料本身是公開的，原始來源也不需要驗證），
	/// 但「不需要身分」不等於「可以無限次呼叫」。沒有限流時，
	/// 一支腳本就能持續打同一組端點；而 GetPricesAsync 的快取鍵包含使用者可控的
	/// 作物代碼組合與日期區間，所以受害的不只是資料庫負載，
	/// 還包括被任意參數組合灌滿的 Redis——快取層本身變成攻擊面。
	/// </para>
	/// <para>
	/// 用 in-memory 的計數器：目前是單一執行個體部署，多執行個體時每台各記各的，
	/// 實際上限會變成「設定值 × 台數」。要跨執行個體共用計數需要改走 Redis，
	/// 那是部署形態改變之後才需要決定的事，已記為技術債
	/// </para>
	/// </summary>
	public static class RateLimitingExtensions
	{
		/// <summary>公開查詢端點共用的限流策略名稱</summary>
		public const string PublicQueryPolicy = "public-query";

		public static IServiceCollection AddPublicQueryRateLimiting(
			this IServiceCollection services, IConfiguration configuration)
		{
			var options = new RateLimitOptions();
			configuration.GetSection(RateLimitOptions.SectionName).Bind(options);
			services.Configure<RateLimitOptions>(configuration.GetSection(RateLimitOptions.SectionName));

			services.AddRateLimiter(limiter =>
			{
				// 預設是 200，也就是「擋下來但看起來像伺服器錯誤」。
				// 429 才說得出「你被限流了、等一下再來」這件事，
				// 呼叫端才有辦法分辨自己是被擋還是打壞了什麼
				limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

				limiter.AddPolicy(PublicQueryPolicy, context =>
					RateLimitPartition.GetFixedWindowLimiter(
						// 免登入端點沒有使用者身分可以分割，只能用來源位址。
						// 這擋得住單一來源的腳本，擋不住分散來源——後者需要的是另一個層級的防護
						partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
						factory: _ => new FixedWindowRateLimiterOptions
						{
							PermitLimit = options.PermitLimit,
							Window = TimeSpan.FromSeconds(options.WindowSeconds),
							// 不排隊：查詢端點的請求排隊等於讓使用者盯著轉圈圈，
							// 直接回 429 讓前端決定要重試還是提示，比較誠實
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
	/// 這一組管的是中介層每個來源能打幾次，跟任何一個模組的查詢語意無關
	/// </summary>
	public class RateLimitOptions
	{
		public const string SectionName = "RateLimiting";

		/// <summary>一個時間窗內允許的請求數。行情頁一次載入會打兩支 API，
		/// 六十次對正常操作綽綽有餘，對腳本則是有效上限</summary>
		public int PermitLimit { get; set; } = 60;

		/// <summary>時間窗長度（秒）</summary>
		public int WindowSeconds { get; set; } = 60;
	}
}
