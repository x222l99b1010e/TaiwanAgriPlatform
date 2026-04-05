using Microsoft.EntityFrameworkCore;
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
			});

			builder.Services.AddHostedService<WeatherSyncWorker>();

			

			var host = builder.Build();
			host.Run();
		}
	}
}
