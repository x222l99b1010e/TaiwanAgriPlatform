using Microsoft.EntityFrameworkCore;
using TaiwanAgri.Modules.FoodSafety.Data;

namespace TaiwanAgri.Web.Extensions
{
	public static class FoodSafetyModuleExtensions
	{
		public static IServiceCollection AddFoodSafetyModule(this IServiceCollection services, IConfiguration configuration)
		{
			services.AddDbContext<FoodSafetyDbContext>(options =>
				options.UseSqlServer(
					configuration.GetConnectionString("DefaultConnection")));
			return services;
		}
	}
}
