using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
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

			// 超長字串截到欄位上限再寫入（只有同步程式這樣做，理由見 TruncateToColumnLengthInterceptor）。
			// logger 用 GetService 取：只註冊 DbContext、沒有註冊記錄的測試容器也解析得出來
			services.AddSingleton(sp => new TruncateToColumnLengthInterceptor(
				sp.GetService<ILogger<TruncateToColumnLengthInterceptor>>()
					?? NullLogger<TruncateToColumnLengthInterceptor>.Instance));

			services.AddDbContext<CoreDbContext>((sp, options) => Configure(sp, options));
			services.AddDbContext<WeatherDbContext>((sp, options) => Configure(sp, options));
			services.AddDbContext<MarketDbContext>((sp, options) => Configure(sp, options));
			services.AddDbContext<FoodSafetyDbContext>((sp, options) => Configure(sp, options));
			services.AddDbContext<PetDbContext>((sp, options) => Configure(sp, options));

			void Configure(IServiceProvider sp, DbContextOptionsBuilder options)
				=> options.UseSqlServerWithRetry(connectionString)
					.AddInterceptors(sp.GetRequiredService<TruncateToColumnLengthInterceptor>());

			return services;
		}
	}
}
