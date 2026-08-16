using TaiwanAgri.Web.Services;

namespace TaiwanAgri.Web.Extensions
{
	public static class InfrastructureExtensions
	{
		public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
		{
			// Redis
			services.AddStackExchangeRedisCache(options =>
			{
				options.Configuration = configuration.GetConnectionString("Redis");
			});

			// CORS
			services.AddCors(options =>
			{
				options.AddPolicy("MyPolicy", policy =>
				{
					var origins = configuration
						.GetSection("Cors:AllowedOrigins")
						.Get<string[]>() ?? [];

					policy.WithOrigins(origins)
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