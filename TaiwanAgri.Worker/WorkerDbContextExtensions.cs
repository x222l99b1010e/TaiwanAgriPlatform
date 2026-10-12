using TaiwanAgri.Core.Extensions;
using TaiwanAgri.Core.Infrastructure.Data;
using TaiwanAgri.Modules.FoodSafety.Data;
using TaiwanAgri.Modules.Market.Data;
using TaiwanAgri.Modules.Pet.Data;
using TaiwanAgri.Modules.Weather.Data;

namespace TaiwanAgri.Worker
{
	/// <summary>
	/// Worker 用到的五個 DbContext（使用者與身分那兩個只有 Web 需要）。
	/// 抽成方法而不是寫在 Program.Main 裡，是為了讓測試能拿同一份註冊檢查連線設定，
	/// 不必真的啟動整個 Host。
	/// </summary>
	public static class WorkerDbContextExtensions
	{
		public static IServiceCollection AddWorkerDbContexts(this IServiceCollection services, IConfiguration configuration)
		{
			var connectionString = configuration.GetConnectionString("DefaultConnection");

			services.AddDbContext<CoreDbContext>(options => options.UseSqlServerWithRetry(connectionString));
			services.AddDbContext<WeatherDbContext>(options => options.UseSqlServerWithRetry(connectionString));
			services.AddDbContext<MarketDbContext>(options => options.UseSqlServerWithRetry(connectionString));
			services.AddDbContext<FoodSafetyDbContext>(options => options.UseSqlServerWithRetry(connectionString));
			services.AddDbContext<PetDbContext>(options => options.UseSqlServerWithRetry(connectionString));

			return services;
		}
	}
}
