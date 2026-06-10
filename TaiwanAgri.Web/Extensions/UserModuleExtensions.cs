using Microsoft.EntityFrameworkCore;
using TaiwanAgri.Modules.User.Services;
using TaiwanAgri.Modules.User.Data;

namespace TaiwanAgri.Web.Extensions
{
	public static class UserModuleExtensions
	{
		public static IServiceCollection AddUserModule(this IServiceCollection services, IConfiguration configuration)
		{
			services.AddDbContext<UserDbContext>(options =>
				options.UseSqlServer(
					configuration.GetConnectionString("DefaultConnection")));

			// IUserProfileService → UserProfileService
			// Scoped：每個 HTTP Request 建立一個實例，Request 結束就釋放
			// 和其他 Service（IMarketService、IWeatherService）一致
			services.AddScoped<IUserProfileService, UserProfileService>();
			services.AddScoped<IUserWatchlistService, UserWatchlistService>();

			return services;
		}
	}
}
