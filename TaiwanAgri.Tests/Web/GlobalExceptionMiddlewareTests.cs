using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using TaiwanAgri.Web.Middlewares;

namespace TaiwanAgri.Tests.Web
{
	/// <summary>
	/// 未處理的例外：記錄要帶是哪一支端點與請求編號，回給使用者的 500 帶同一個編號、不帶例外原文。
	/// 原本記錄只有「未處理的例外」加例外本身，使用者回報「剛剛出錯」時對不回是哪一筆（2026-11 總檢查 G-C14）
	/// </summary>
	public class GlobalExceptionMiddlewareTests
	{
		private sealed class RecordingLogger : ILogger<GlobalExceptionMiddleware>
		{
			public List<(LogLevel Level, string Message)> Entries { get; } = new();

			public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
			public bool IsEnabled(LogLevel logLevel) => true;

			public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
				Func<TState, Exception?, string> formatter) =>
				Entries.Add((logLevel, formatter(state, exception)));
		}

		private static DefaultHttpContext Context()
		{
			var context = new DefaultHttpContext();
			context.Request.Method = "POST";
			context.Request.Path = "/api/Auth/register";
			context.Response.Body = new MemoryStream();
			return context;
		}

		private static async Task<JsonElement> ReadBodyAsync(HttpContext context)
		{
			context.Response.Body.Position = 0;
			using var doc = await JsonDocument.ParseAsync(context.Response.Body);
			return doc.RootElement.Clone();
		}

		/// <summary>正式環境每個請求都有 Activity，編號用它的 Id——與 400 驗證錯誤回應的 traceId 同一種</summary>
		[Fact]
		public async Task 回500帶請求編號且記錄帶方法路徑與同一個編號()
		{
			using var activity = new Activity("測試請求").Start();
			var logger = new RecordingLogger();
			var middleware = new GlobalExceptionMiddleware(
				_ => throw new InvalidOperationException("內部錯誤的原文"), logger);
			var context = Context();

			await middleware.InvokeAsync(context);

			Assert.Equal(500, context.Response.StatusCode);
			var body = await ReadBodyAsync(context);
			Assert.Equal(activity.Id, body.GetProperty("traceId").GetString());
			Assert.DoesNotContain("內部錯誤的原文", body.GetRawText());

			var entry = Assert.Single(logger.Entries);
			Assert.Equal(LogLevel.Error, entry.Level);
			Assert.Contains("POST /api/Auth/register", entry.Message);
			Assert.Contains(activity.Id!, entry.Message);
		}

		[Fact]
		public async Task 沒有Activity時改用HttpContext的請求編號()
		{
			var previous = Activity.Current;
			Activity.Current = null;
			try
			{
				var logger = new RecordingLogger();
				var middleware = new GlobalExceptionMiddleware(_ => throw new InvalidOperationException("x"), logger);
				var context = Context();
				context.TraceIdentifier = "0HN000000001:00000001";

				await middleware.InvokeAsync(context);

				var body = await ReadBodyAsync(context);
				Assert.Equal("0HN000000001:00000001", body.GetProperty("traceId").GetString());
				Assert.Contains("0HN000000001:00000001", Assert.Single(logger.Entries).Message);
			}
			finally
			{
				Activity.Current = previous;
			}
		}
	}
}
