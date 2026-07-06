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
				_logger.LogError(ex, "未處理的例外");  // 記錄完整 exception

				// 回應已開始串流時無法再改寫 status code / body，
				// 硬寫會拋第二個例外蓋掉原始錯誤，只能重拋讓 Kestrel 中斷連線
				if (context.Response.HasStarted)
					throw;

				await HandleExceptionAsync(context);   // 回傳標準化錯誤
			}
		}

		private static async Task HandleExceptionAsync(HttpContext context)
		{
			context.Response.StatusCode = 500;
			context.Response.ContentType = "application/json";

			var response = new
			{
				status = 500,
				message = "伺服器發生非預期錯誤，請稍後再試"
			};

			await context.Response.WriteAsJsonAsync(response);
		}
	}
}
