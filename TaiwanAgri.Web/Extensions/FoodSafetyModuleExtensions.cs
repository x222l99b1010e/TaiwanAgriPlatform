using Microsoft.EntityFrameworkCore;
using TaiwanAgri.Core.Extensions;
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

			// MoaApi Named Client 設定與 Worker 共用（TaiwanAgri.Core.Extensions）
			services.AddMoaApiClient();

			services.AddScoped<IFoodSafetyService, FoodSafetyService>();
			services.AddScoped<ITraceabilityService, TraceabilityService>();

			return services;
		}
	}
}
