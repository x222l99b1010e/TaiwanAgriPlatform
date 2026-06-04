using Microsoft.EntityFrameworkCore;
using TaiwanAgri.Modules.Weather.Data;
using TaiwanAgri.Modules.Weather.Services;

namespace TaiwanAgri.Web.Extensions
{
	public static class WeatherModuleExtensions
	{
		public static IServiceCollection AddWeatherModule(this IServiceCollection services,	IConfiguration configuration)
		{
			services.AddDbContext<WeatherDbContext>(options =>
				options.UseSqlServer(
					configuration.GetConnectionString("DefaultConnection")));

			services.AddScoped<IWeatherService, WeatherService>();
			services.AddScoped<IPestService, PestService>();

			return services;
		}
	}
}