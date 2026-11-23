using System.Diagnostics;

namespace TaiwanAgri.Web.Middlewares
{
	public class GlobalExceptionMiddleware
	{
		private readonly RequestDelegate _next;
		private readonly ILogger<GlobalExceptionMiddleware> _logger;

		public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
		{
			_next = next;
			_logger = logger;
		}

		public async Task InvokeAsync(HttpContext context)
		{
			try
			{
				await _next(context);  // 讓 request 繼續往下走
			}
			catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
			{
				// 客戶端主動斷線（關頁面、取消請求）不是伺服器錯誤，
				// 記 debug 即可，不進 error 日誌、不回應（連線已斷）
				_logger.LogDebug("請求被客戶端取消：{Path}", context.Request.Path);
			}
			catch (Exception ex)
			{
				// 記錄帶上是哪一支端點（方法與路徑；不記查詢字串）和請求編號，同一個編號也放進回給使用者的 500，
				// 使用者回報「剛剛出錯」時對得回是哪一筆（2026-11 總檢查 G-C14）。
				// 編號取法與 400 驗證錯誤回應裡的 traceId 相同，兩種錯誤回應用的是同一種編號
				var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
				_logger.LogError(ex, "未處理的例外：{Method} {Path}（traceId {TraceId}）",
					context.Request.Method, context.Request.Path, traceId);

				// 回應已開始串流時無法再改寫 status code / body，
				// 硬寫會拋第二個例外蓋掉原始錯誤，只能重拋讓 Kestrel 中斷連線
				if (context.Response.HasStarted)
					throw;

				await HandleExceptionAsync(context, traceId);   // 回傳標準化錯誤
			}
		}

		private static async Task HandleExceptionAsync(HttpContext context, string traceId)
		{
			context.Response.StatusCode = 500;
			context.Response.ContentType = "application/json";

			var response = new
			{
				status = 500,
				message = "伺服器發生非預期錯誤，請稍後再試",
				traceId
			};

			await context.Response.WriteAsJsonAsync(response);
		}
	}
}
