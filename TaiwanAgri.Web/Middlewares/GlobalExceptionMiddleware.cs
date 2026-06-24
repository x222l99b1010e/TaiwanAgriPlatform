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
			catch (Exception ex)
			{
				_logger.LogError(ex, "未處理的例外");  // 記錄完整 exception
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
