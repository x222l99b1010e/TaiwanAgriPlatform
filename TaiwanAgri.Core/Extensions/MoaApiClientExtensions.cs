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
				client.Timeout = Timeout.InfiniteTimeSpan;
				client.DefaultRequestHeaders.Add(
					"User-Agent",
					"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120.0.0.0 Safari/537.36"
					);
			})
			// 2026-07-30 發現（PetLoseListSyncWorker 分批平行實測）：農業部這個站台會對每個請求
			// 回 Set-Cookie: ASP.NET_SessionId，HttpClient 預設用同一個 CookieContainer 記住它，
			// 導致同一個具名 client 送出的平行請求全部帶著同一個 session id——伺服器端的
			// ASP.NET Session State 對同一個 session 的並行請求會排隊序列化處理，看起來像是平行送出，
			// 實際上變成一個接一個處理（實測：共用 session 3 個平行呼叫總計 91 秒，各自獨立 session
			// 只要 28 秒）。這支 API 是唯讀查詢、不需要登入也不依賴任何跨請求狀態，關閉 cookie
			// 讓每個請求都是獨立 session，伺服器就會真的平行處理。
			// 影響範圍：這是具名 client 全域設定，所有透過 "MoaApi" 呼叫農業部 API 的 Worker
			// 都會套用到（含 AgriProductsTransSyncWorker 既有的併發抓取），對它們只有好處
			// （不需要 session 的呼叫關掉 cookie 沒有副作用），不需要個別調整。
			.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
			{
				UseCookies = false
			})
			.AddTransientHttpErrorPolicy(policy =>
					policy.WaitAndRetryAsync(3, _ => TimeSpan.FromSeconds(2)));
			// 遇到網路錯誤或 5xx，自動等待 2 秒並重試，最多 3 次

			return services;
		}
	}
}
