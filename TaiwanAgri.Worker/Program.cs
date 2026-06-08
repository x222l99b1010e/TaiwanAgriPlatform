using Microsoft.EntityFrameworkCore;
using Polly;
using Serilog;
using TaiwanAgri.Core.Infrastructure.Data;
using TaiwanAgri.Modules.Market.Data;
using TaiwanAgri.Modules.Weather.Data;
using TaiwanAgri.Modules.Weather.Services;

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

			builder.Services.AddHttpClient("MoaApi", client =>
			{
				client.BaseAddress = new Uri("https://data.moa.gov.tw/");
				client.Timeout = TimeSpan.FromSeconds(120);
				client.DefaultRequestHeaders.Add(
					"User-Agent",
					"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120.0.0.0 Safari/537.36"
					);
			}).AddTransientHttpErrorPolicy(policy =>
					policy.WaitAndRetryAsync(3, _ => TimeSpan.FromSeconds(2)));
			// 遇到網路錯誤或 5xx，自動等待 2 秒並重試，最多 3 次

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

			var host = builder.Build();
			host.Run();
		}
	}
}
