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
			services.AddScoped<INotificationRuleService, NotificationRuleService>();
			// 規則引擎在 Worker 專案也註冊了一次，因為兩者是各自獨立的組合根
			// （Worker 不參考 Web，連 DbContext 都各註冊各的）。Web 需要它是為了「立即檢查」端點：
			// 使用者剛建完規則要能馬上看到結果，不必等每天一次的排程。
			// 它自己從 IServiceScopeFactory 開 scope 取 DbContext，所以生命週期是 Singleton
			services.AddSingleton<PestRuleEngine>();
			// 農藥查詢（W24）：即時打農業部 API、不落地，因此不注入 WeatherDbContext，
			// 只依賴 MoaApi 具名 HttpClient（由 Program.cs 的 AddMoaApiClient 註冊）
			services.AddScoped<IPesticideService, PesticideService>();

			return services;
		}
	}
}