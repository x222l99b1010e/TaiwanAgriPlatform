using Microsoft.EntityFrameworkCore;
using TaiwanAgri.Core.Infrastructure.Data;
using TaiwanAgri.Core.Services;
using TaiwanAgri.Modules.Weather.Services;

namespace TaiwanAgri.Web.Extensions
{
	public static class CoreModuleExtensions
	{
		public static IServiceCollection AddCoreModule(this IServiceCollection services, IConfiguration configuration)
		{
			services.AddDbContext<CoreDbContext>(options =>
				options.UseSqlServer(
					configuration.GetConnectionString("DefaultConnection")));

			services.AddScoped<INavService, NavService>();
			services.AddScoped<INotificationService, NotificationService>();

			return services;
		}
	}
}