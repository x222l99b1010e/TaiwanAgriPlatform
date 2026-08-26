using Microsoft.EntityFrameworkCore;
using Serilog;
using TaiwanAgri.Core.Extensions;
using TaiwanAgri.Core.Infrastructure.Data;
using TaiwanAgri.Modules.FoodSafety.Data;
using TaiwanAgri.Modules.Market.Data;
using TaiwanAgri.Modules.Pet.Data;
using TaiwanAgri.Modules.Weather.Data;
using TaiwanAgri.Modules.Weather.Services;
using TaiwanAgri.Worker.Weather;
using TaiwanAgri.Worker.Market;
using TaiwanAgri.Worker.FoodSafety;
using TaiwanAgri.Worker.Pet;

namespace TaiwanAgri.Worker
{
	public class Program
	{
		public static void Main(string[] args)
		{
			Log.Logger = new LoggerConfiguration()
				.MinimumLevel.Information()
				.WriteTo.Console()
				.WriteTo.File(
					path: "logs/worker-.log",
					rollingInterval: RollingInterval.Day,      // 每天一個新檔案
					retainedFileCountLimit: 60,                // 只保留最近 60 天
					outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
				)
				.CreateLogger();

			var builder = Host.CreateApplicationBuilder(args);
			builder.Logging.ClearProviders();
			builder.Logging.AddSerilog();

			//DbContext 註冊
			builder.Services.AddDbContext<CoreDbContext>(options =>
				options.UseSqlServer(
					builder.Configuration.GetConnectionString("DefaultConnection")));
			builder.Services.AddDbContext<WeatherDbContext>(options =>
				options.UseSqlServer(
					builder.Configuration.GetConnectionString("DefaultConnection")));

			builder.Services.AddDbContext<MarketDbContext>(options =>
				options.UseSqlServer(
					builder.Configuration.GetConnectionString("DefaultConnection")));
			builder.Services.AddDbContext<FoodSafetyDbContext>(options =>
				options.UseSqlServer(
					builder.Configuration.GetConnectionString("DefaultConnection")));
			builder.Services.AddDbContext<PetDbContext>(options =>
				options.UseSqlServer(
					builder.Configuration.GetConnectionString("DefaultConnection")));

			// MoaApi Named Client 設定與 Web 共用（TaiwanAgri.Core.Extensions）
			builder.Services.AddMoaApiClient();
			// 時鐘統一走 TimeProvider 注入（跟 TaiwanAgri.Web 保持一致慣例）
			builder.Services.AddSingleton(TimeProvider.System);

			//Weather 註冊
			builder.Services.AddHostedService<WeatherSyncWorker>();
			builder.Services.AddHostedService<PestAlertSyncWorker>();
			builder.Services.AddHostedService<RainfallStationSyncWorker>();
			builder.Services.AddHostedService<RainfallSyncWorker>();
			builder.Services.AddHostedService<PestDecadeSyncWorker>();
			builder.Services.AddSingleton<PestRuleEngine>();
			builder.Services.AddHostedService<PestRuleEngineWorker>();
			//Market 註冊
			builder.Services.AddHostedService<MarketRestDaySyncWorker>();
			builder.Services.AddHostedService<CropMarketSyncWorker>();
			builder.Services.AddHostedService<AgriProductsTransSyncWorker>();
			builder.Services.AddHostedService<DebrisAlertRecordSyncWorker>();
			builder.Services.AddHostedService<PorkTransSyncWorker>();
			builder.Services.AddHostedService<PoultryTransSyncWorker>();
			//FoodSafety 註冊
			builder.Services.AddHostedService<PesticideViolationSyncWorker>();
			builder.Services.AddHostedService<OrganicCertificationSyncWorker>();
			//Pet 註冊
			builder.Services.AddHostedService<AnimalRecognitionSyncWorker>();
			builder.Services.AddHostedService<PetLoseListSyncWorker>();
			builder.Services.AddHostedService<LegalSpecificPetSyncWorker>();

			var host = builder.Build();
			host.Run();
		}
	}
}
