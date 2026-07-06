using System.Text.Json;
using Microsoft.Extensions.Logging;
using TaiwanAgri.Core.Dtos;

namespace TaiwanAgri.Core.Helpers
{
	/// <summary>
	/// 農業部開放資料 API 的通用分頁抓取器。
	/// 統一處理：第一頁不帶 page 參數的分頁慣例、RS != "OK" 判斷、
	/// Next 換頁旗標、非會員無分頁權限的靜默停止、保護性頁數上限。
	/// 各 Worker 只需提供 endpoint 與回應型別，不再各自複製分頁迴圈
	/// </summary>
	public static class MoaPagedFetcher
	{
		/// <summary>保護性分頁上限，避免 API 異常（如 Next 永遠為 true）導致無限迴圈</summary>
		public const int DefaultMaxPages = 20;

		/// <param name="httpClient">已設定 BaseAddress 的 MoaApi HttpClient</param>
		/// <param name="endpoint">API 端點路徑（不含 page 參數）</param>
		/// <param name="logPrefix">日誌前綴，如 "[OrganicCertificationSync]"</param>
		public static async Task<List<TDto>> FetchAllPagesAsync<TResponse, TDto>(
			HttpClient httpClient,
			string endpoint,
			ILogger logger,
			string logPrefix,
			CancellationToken cancellationToken,
			int maxPages = DefaultMaxPages)
			where TResponse : class, IMoaPagedResponse<TDto>
		{
			var allDtos = new List<TDto>();
			int page = 1;
			while (true)
			{
				// 第一頁不帶 page 參數，第二頁以後才帶（農業部 API 的分頁慣例）
				var url = (page == 1) ? endpoint : $"{endpoint}?page={page}";
				var json = await httpClient.GetStringAsync(url, cancellationToken);
				var response = JsonSerializer.Deserialize<TResponse>(json);

				if (response?.RS != "OK" || response.Data.Count == 0)
				{
					if (page == 1)
					{
						logger.LogWarning("{Prefix} API回應異常或無資料，停止同步", logPrefix);
					}
					else
					{
						// 非會員只能拿第一頁，第二頁以後可能直接無資料，這不算異常，是權限限制
						logger.LogInformation("{Prefix} 第 {Page} 頁無資料或無分頁權限，停止抓取", logPrefix, page);
					}
					break;
				}
				logger.LogInformation("{Prefix} 成功抓取第 {Page} 頁，共 {Count} 筆資料", logPrefix, page, response.Data.Count);
				allDtos.AddRange(response.Data);

				// API 回傳 Next=false 代表沒有下一頁，主動停止
				if (!response.Next)
					break;

				page++;

				if (page > maxPages)
				{
					logger.LogWarning("{Prefix} 已達分頁上限（{MaxPages}頁），停止繼續抓取", logPrefix, maxPages);
					break;
				}
			}
			logger.LogInformation("{Prefix} 共抓取 {Count} 筆原始資料", logPrefix, allDtos.Count);
			return allDtos;
		}
	}
}
