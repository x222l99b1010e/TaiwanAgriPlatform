using Microsoft.EntityFrameworkCore;
using TaiwanAgri.Modules.Market.Constants;
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

			// 查詢上限走強型別選項；沒設定 MarketQueryLimits 區段時用類別上的預設值
			services.Configure<MarketQueryOptions>(
				configuration.GetSection(MarketQueryOptions.SectionName));

			services.AddScoped<IMarketService, MarketService>();

			return services;
		}
	}
}