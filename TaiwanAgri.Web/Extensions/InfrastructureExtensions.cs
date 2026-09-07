using TaiwanAgri.Web.Services;

namespace TaiwanAgri.Web.Extensions
{
	public static class InfrastructureExtensions
	{
		/// <summary>CORS 原則名稱。原本叫 "MyPolicy"，名字說不出它是給誰用的</summary>
		public const string FrontendCorsPolicy = "FrontendCors";

		/// <summary>
		/// Development 未設定 CORS 來源時的啟動警告。
		/// 由 Program.cs 在 DI 容器建好之後記錄——設定階段還拿不到 ILogger，
		/// 而寫 Console 進不了正式的 log pipeline，等於警告了沒人收得到
		/// </summary>
		public const string CorsOriginsMissingWarning =
			"未設定 Cors:AllowedOrigins，本服務將拒絕所有跨來源請求。" +
			"目前只有同源請求（例如經由 Vite proxy）能通過；若前端要直接呼叫本 API，" +
			"請填入允許的來源，或以 Cors:SameOriginOnly = true 明確宣告不需要 CORS。";

		/// <summary>
		/// <c>Cors:AllowedOrigins</c> 沒填、且沒有用 <c>Cors:SameOriginOnly</c> 宣告不需要 CORS。
		/// <para>
		/// 這個狀態下 <c>WithOrigins([])</c> 會拒絕所有跨來源請求，而且不留任何訊息——
		/// 前端在瀏覽器端全掛，伺服器端一切正常。本機察覺不到是因為 Vite proxy
		/// 讓請求變成同源、根本不經過 CORS。
		/// </para>
		/// <para>
		/// 之所以要 SameOriginOnly 這個旗標：「忘了填」與「刻意只走同源 proxy」在設定檔裡
		/// 長得一模一樣，程式無從分辨。把後者變成要寫下來的宣告，剩下的空白就只有一種解釋
		/// </para>
		/// </summary>
		public static bool IsCorsOriginsMissing(IConfiguration configuration) =>
			(configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? []).Length == 0
			&& !configuration.GetValue<bool>("Cors:SameOriginOnly");

		/// <summary>
		/// 啟動時檢查 CORS 來源設定，非 Development 環境缺設定就讓啟動直接失敗。
		/// <para>
		/// Development 只警告不中斷：本機經 Vite proxy 存取是同源，本來就不需要 CORS。
		/// 其他環境沒有這個豁免——設定漏了只會在使用者的瀏覽器裡現形，伺服器端毫無跡象，
		/// 啟動時失敗比上線後才發現便宜得多
		/// </para>
		/// </summary>
		public static void ValidateCorsConfiguration(IConfiguration configuration, IHostEnvironment environment)
		{
			if (environment.IsDevelopment() || !IsCorsOriginsMissing(configuration))
			{
				return;
			}

			throw new InvalidOperationException(
				"Cors:AllowedOrigins 未設定。前端若直接呼叫本 API，所有請求都會被瀏覽器擋掉，" +
				"而伺服器端不會留下任何錯誤紀錄。請填入允許的來源（例：https://your-frontend.example）；" +
				"若本服務只透過同源 proxy 對外、確實不需要 CORS，" +
				"請明確設定 Cors:SameOriginOnly = true。");
		}

		public static IServiceCollection AddInfrastructure(
			this IServiceCollection services,
			IConfiguration configuration,
			IHostEnvironment environment)
		{
			// Redis
			services.AddStackExchangeRedisCache(options =>
			{
				options.Configuration = configuration.GetConnectionString("Redis");
			});

			// CORS
			ValidateCorsConfiguration(configuration, environment);
			var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

			services.AddCors(options =>
			{
				options.AddPolicy(FrontendCorsPolicy, policy =>
				{
					policy.WithOrigins(allowedOrigins)
						  .AllowAnyMethod()
						  .AllowAnyHeader()
						  .AllowCredentials();
				});
			});

			// RabbitMQ Consumer
			services.AddHostedService<PriceUpdatedConsumer>();

			// Web API 基礎
			services.AddControllers();
			services.AddProblemDetails(); // 註冊標準錯誤格式服務
			services.AddEndpointsApiExplorer();
			services.AddSwaggerGen();

			return services;
		}
	}
}