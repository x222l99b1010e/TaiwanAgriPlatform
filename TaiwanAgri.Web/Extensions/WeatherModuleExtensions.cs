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
			// 農藥查詢（W24）：即時打農業部 API、不落地，因此不注入 WeatherDbContext，
			// 只依賴 MoaApi 具名 HttpClient（由 Program.cs 的 AddMoaApiClient 註冊）
			services.AddScoped<IPesticideService, PesticideService>();

			return services;
		}
	}
}