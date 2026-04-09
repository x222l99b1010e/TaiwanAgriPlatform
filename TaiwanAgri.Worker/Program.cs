using Microsoft.EntityFrameworkCore;
using Polly;
using TaiwanAgri.Modules.Weather.Data;

namespace TaiwanAgri.Worker
{
	public class Program
	{
		public static void Main(string[] args)
		{
			var builder = Host.CreateApplicationBuilder(args);
			//DbContext 註冊
			builder.Services.AddDbContext<WeatherDbContext>(options =>
				options.UseSqlServer(
					builder.Configuration.GetConnectionString("DefaultConnection")));

			builder.Services.AddHttpClient("MoaApi", client =>
			{
				client.BaseAddress = new Uri("https://data.moa.gov.tw/");
				client.Timeout = TimeSpan.FromSeconds(60);
				client.DefaultRequestHeaders.Add(
					"User-Agent",
					"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120.0.0.0 Safari/537.36"
					);
			}).AddTransientHttpErrorPolicy(policy =>
					policy.WaitAndRetryAsync(3, _ => TimeSpan.FromSeconds(2)));
			// 遇到網路錯誤或 5xx，自動等待 2 秒並重試，最多 3 次


			builder.Services.AddHostedService<WeatherSyncWorker>();

			

			var host = builder.Build();
			host.Run();
		}
	}
}
