using System.Security.Cryptography;//SHA256
using System.Text;//Encoding.UTF8
using System.Text.Json;
using TaiwanAgri.Core.Constants;
using TaiwanAgri.Modules.Weather.Data;
using TaiwanAgri.Modules.Weather.Dtos;
using TaiwanAgri.Modules.Weather.Entities;
using Microsoft.EntityFrameworkCore;

namespace TaiwanAgri.Worker
{
	public class PestAlertSyncWorker : BackgroundService
	{
		private readonly ILogger<PestAlertSyncWorker> _logger;
		private readonly HttpClient _httpClient;
		private readonly IServiceScopeFactory _scopeFactory;
		public PestAlertSyncWorker(ILogger<PestAlertSyncWorker> logger, IHttpClientFactory httpClientFactory, IServiceScopeFactory scopeFactory)
		{
			_logger = logger;
			_httpClient = httpClientFactory.CreateClient("MoaApi");
			_scopeFactory = scopeFactory;
		}
		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			while (!stoppingToken.IsCancellationRequested)
			{
				try
				{
					await SyncPestAlertAsync(stoppingToken);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "[PestAlertSync] 同步失敗");
				}

				await Task.Delay(TimeSpan.FromDays(1), stoppingToken); // 正式排程每天一次
			}
		}

		private async Task SyncPestAlertAsync(CancellationToken stoppingToken)
		{
			//每次寫入建立一個獨立的 Scope，確保 DbContext 的生命週期正確，並且在操作完成後能夠正確釋放資源。
			//這樣做的好處是可以避免 DbContext 在長時間運行的背景服務中持續存在，從而減少內存泄漏和資源占用的風險。
			using var scope = _scopeFactory.CreateScope();
			var db = scope.ServiceProvider.GetRequiredService<WeatherDbContext>();
			//抓取分頁資料
			var allDtos = new List<PestAlertDto>();
			int Page = 1;
			while (true)
			{
				var url = (Page == 1) ? MoaApiEndpoints.PlantEpidemic : $"{MoaApiEndpoints.PlantEpidemic}?page={Page}";

				var json = await _httpClient.GetStringAsync(url, stoppingToken);
				var response = JsonSerializer.Deserialize<PestAlertApiResponse>(json);

				if (response?.RS != "OK" || response.Data.Count == 0)
				{
					if (Page == 1)
						_logger.LogWarning("[PestAlertSync] API 回傳異常或無資料");
					else
						_logger.LogInformation("[PestAlertSync] 第 {Page} 頁無資料或無分頁權限，停止抓取", Page);
					break;
				}
				_logger.LogInformation("[PestAlertSync] 成功抓取第 {Page} 頁，共 {Count} 筆資料", Page, response.Data.Count);
				//將Data加入DTO
				allDtos.AddRange(response.Data);
				if (!response.Next)
					break;
				Page ++;
				//
				if (Page > 20) //安全機制，避免無限迴圈
				{
					_logger.LogWarning("[PestAlertSync] 已達分頁上限（20頁），停止繼續抓取");
					break;
				}
			}
			_logger.LogInformation("[PestAlertSync] 合計取得 {Count} 筆原始資料", allDtos.Count);

			// --- 前面的抓取與 MapToEntity 都不變 ---
			var incoming = allDtos
				.Select(MapToEntity)
				.Where(p => p != null)
				.Cast<PestAlert>()
				.ToList();

			// 計算所有 incoming 資料的 SourceHash
			var targetHashes = incoming
				.Select(p => p.SourceHash)
				.Distinct()
				.ToHashSet();

			
			var existingHashes = (await db.PestAlerts
				.Where(p => targetHashes.Contains(p.SourceHash))
				.Select(p =>p.SourceHash)
				.ToListAsync(stoppingToken))
				.ToHashSet();
			var newPestAlerts = incoming
				.Where(p => !existingHashes.Contains(p.SourceHash))
				.ToList();

			if ( newPestAlerts.Count == 0)
			{
				_logger.LogInformation("[PestAlertSync] 無新資料需要同步");
				return;
			}

			await db.PestAlerts.AddRangeAsync(newPestAlerts, stoppingToken);
			await db.SaveChangesAsync(stoppingToken);
			_logger.LogInformation("[PestAlertSync] 成功同步 {Count} 筆新資料 略過 {Skipped} 筆重複", 
				newPestAlerts.Count, 
				incoming.Count - newPestAlerts.Count);
		}

		private PestAlert? MapToEntity(PestAlertDto dto)
		{
			// TIME 格式是 "2026/04/02 11:00"，需要轉成 DateTime
			if (!DateOnly.TryParseExact(dto.PubDate, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var pubDate))
			{
				_logger.LogWarning("[PestAlertSync] 時間格式錯誤，略過主題 {Subject}: {PubDate}",dto.Subject,dto.PubDate);
				return null;
			}

			return new PestAlert
			{
				Subject = dto.Subject,
				Body = dto.Body,
				Prescription = dto.Prescription,
				CitiesRaw = dto.City,
				PlantNamesRaw = dto.PlantName,
				PubDate = pubDate,
				Issue = dto.Issue,
				SourceHash = ComputeHash(dto.Subject + "|" + dto.PubDate),
				SyncedAt = DateTime.Now,

				Cities = string.IsNullOrWhiteSpace(dto.City)
					? new List<PestAlertCity>()
					: dto.City.Split(',').Select(c => new PestAlertCity { CityName = c.Trim() }).ToList(),
				//如果 PlantName 是空白或 null，則不建立任何 Crop 實體，直接給予一個空的 List，避免產生一筆 CropName 為空字串的資料。
				Crops = string.IsNullOrWhiteSpace(dto.PlantName) 
					? new List<PestAlertCrop>() 
					: dto.PlantName.Split(',').Select(p => new PestAlertCrop { CropName = p.Trim() }).ToList(),	
				//Crops = dto.PlantName.Split(',').Select(p => new PestAlertCrop { CropName = p.Trim() }).ToList(),
			};
		}

		private static string ComputeHash(string v)
		{
			var bytes = Encoding.UTF8.GetBytes(v);
			var hashBytes = SHA256.HashData(bytes);
			return Convert.ToHexString(hashBytes);
		}
	}
}
