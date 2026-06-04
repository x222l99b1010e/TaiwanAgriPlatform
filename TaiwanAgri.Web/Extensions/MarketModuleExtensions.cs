using Microsoft.EntityFrameworkCore;
using TaiwanAgri.Modules.Market.Data;
using TaiwanAgri.Modules.Market.Services;

namespace TaiwanAgri.Web.Extensions
{
	public static class MarketModuleExtensions
	{
		public static IServiceCollection AddMarketModule(this IServiceCollection services,	IConfiguration configuration)
		{
			services.AddDbContext<MarketDbContext>(options =>
				options.UseSqlServer(
					configuration.GetConnectionString("DefaultConnection")));

			services.AddScoped<IMarketService, MarketService>();

			return services;
		}
	}
}