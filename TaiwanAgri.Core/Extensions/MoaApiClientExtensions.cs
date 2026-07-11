using Microsoft.Extensions.DependencyInjection;
using Polly;

namespace TaiwanAgri.Core.Extensions
{
	/// <summary>
	/// 農業部開放資料 API 的 Named HttpClient 註冊。
	/// Web 與 Worker 兩個 DI 容器共用同一份設定（BaseAddress／timeout／UA／重試策略），
	/// 調整只需改這一處
	/// </summary>
	public static class MoaApiClientExtensions
	{
		public const string ClientName = "MoaApi";

		public static IServiceCollection AddMoaApiClient(this IServiceCollection services)
		{
			services.AddHttpClient(ClientName, client =>
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

			return services;
		}
	}
}
