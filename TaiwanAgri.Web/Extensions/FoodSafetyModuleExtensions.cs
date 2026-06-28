using Microsoft.EntityFrameworkCore;
using Polly;
using TaiwanAgri.Modules.FoodSafety.Data;
using TaiwanAgri.Modules.FoodSafety.Services;

namespace TaiwanAgri.Web.Extensions
{
	public static class FoodSafetyModuleExtensions
	{
		public static IServiceCollection AddFoodSafetyModule(this IServiceCollection services, IConfiguration configuration)
		{
			services.AddDbContext<FoodSafetyDbContext>(options =>
				options.UseSqlServer(
					configuration.GetConnectionString("DefaultConnection")));
			

			services.AddHttpClient("MoaApi", client =>
			{
				client.BaseAddress = new Uri("https://data.moa.gov.tw/");
				client.Timeout = TimeSpan.FromSeconds(120);
				client.DefaultRequestHeaders.Add(
					"User-Agent",
					"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120.0.0.0 Safari/537.36"
					);
			}).AddTransientHttpErrorPolicy(policy =>
					policy.WaitAndRetryAsync(3, _ => TimeSpan.FromSeconds(2)));
			// 遇到網路錯誤或 5xx，自動等待 2 秒並重試，最多 3 次

			services.AddScoped<IFoodSafetyService, FoodSafetyService>();

			return services;
		}
	}
}
