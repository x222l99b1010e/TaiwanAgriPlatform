using TaiwanAgri.Web.Services;

namespace TaiwanAgri.Web.Extensions
{
	public static class InfrastructureExtensions
	{
		/// <summary>CORS 原則名稱。原本叫 "MyPolicy"，名字說不出它是給誰用的</summary>
		public const string FrontendCorsPolicy = "FrontendCors";

		public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
		{
			// Redis
			services.AddStackExchangeRedisCache(options =>
			{
				options.Configuration = configuration.GetConnectionString("Redis");
			});

			// CORS
			var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
			if (allowedOrigins.Length == 0)
			{
				// 沒設定時 WithOrigins([]) 會擋掉所有跨來源請求，而且不留下任何訊息。
				// 開發時察覺不到是因為 Vite proxy 讓請求變成同源、根本不經過 CORS；
				// 一旦前端改成直接打後端（正式部署的常態）就會整個壞掉，且症狀只出現在瀏覽器端。
				// 不直接 throw 是因為「只透過 proxy 對外」本來就是合法的部署方式，
				// 但這件事必須講出來，不能安靜地生效
				Console.WriteLine(
					"[CORS] 警告：未設定 Cors:AllowedOrigins，將拒絕所有跨來源請求。" +
					"若前端不是透過同源 proxy 存取本 API，請在 appsettings 補上允許的來源。");
			}

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