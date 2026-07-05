using System.Text.Json;
using TaiwanAgri.Core.Constants;
using TaiwanAgri.Core.Helpers;
using TaiwanAgri.Modules.FoodSafety.Data;
using TaiwanAgri.Modules.FoodSafety.Dtos.WorkerResponses;
using TaiwanAgri.Modules.FoodSafety.Entities;

namespace TaiwanAgri.Worker
{
	public class PesticideViolationSyncWorker : BackgroundService
	{
		private readonly ILogger<PesticideViolationSyncWorker> _logger;
		private readonly HttpClient _httpClient;
		private readonly IServiceScopeFactory _scopeFactory;

		public PesticideViolationSyncWorker(ILogger<PesticideViolationSyncWorker> logger, IHttpClientFactory httpClientFactory, IServiceScopeFactory scopeFactory)
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
					await SyncPesticideViolationAsync(stoppingToken);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "[PesticideViolationSync] 同步失敗");
				}
				await Task.Delay(TimeSpan.FromDays(1), stoppingToken); // 正式排程每1天一次
			}
		}

		private async Task SyncPesticideViolationAsync(CancellationToken stoppingToken)
		{
			using var scope = _scopeFactory.CreateScope();
			var db = scope.ServiceProvider.GetRequiredService<FoodSafetyDbContext>();

			var allDtos = new List<PesticideViolationDto>();
			int page = 1;
			while (true)
			{
				var url = (page == 1)? MoaApiEndpoints.PesticideViolation : $"{MoaApiEndpoints.PesticideViolation}?page={page}";
				var json = await _httpClient.GetStringAsync(url, stoppingToken);
				var response = JsonSerializer.Deserialize<PesticideViolationApiResponse>(json);

				if (response?.RS != "OK" || response.Data.Count == 0)
				{
					if (page == 1)
					{
						_logger.LogWarning("[PesticideViolationSync] API回應異常或無資料，停止同步");
					}
					else
					{
						_logger.LogInformation("[PesticideViolationSync] 第 {Page} 頁無資料或無分頁權限，停止抓取", page);
					}
					break;
				}
				_logger.LogInformation("[PesticideViolationSync] 成功抓取第 {Page} 頁， 共 {Count} 筆資料", page, response.Data.Count);
				allDtos.AddRange(response.Data);
				if (!response.Next)
					break;
				page++;
				if (page > 20)
				{
					_logger.LogWarning("[PesticideViolationSync] 已達分頁上限（20頁），停止繼續抓取");
					break;
				}	
			}
			_logger.LogInformation("[PesticideViolationSync] 共抓取 {Count} 筆資料", allDtos.Count);
			
			var incoming = allDtos
				.Select(MapToEntity)
				.Where(x => x != null)
				//在 .Where(x => x != null) 後面加一個 cast 告訴編譯器「我保證這裡不會有 null」
				.Cast<PesticideViolation>()
				.DistinctBy(x => x.Number)
				.ToList();

			if (incoming.Count == 0)
			{
				_logger.LogWarning("[PesticideViolationSync] 全部資料轉換失敗（MapToEntity 皆回 null）");
				return;
			}

			var existingNumbers = db.PesticideViolations.Select(x => x.Number).ToHashSet();

			var toInsert = incoming.Where(x => !existingNumbers.Contains(x.Number)).ToList();

			if (toInsert.Count == 0)
			{
				_logger.LogInformation("[PesticideViolationSync] 無新資料需要同步");
				return;
			}
			await db.PesticideViolations.AddRangeAsync(toInsert, stoppingToken);
			await db.SaveChangesAsync(stoppingToken);
			_logger.LogInformation("[PesticideViolationSync] 成功同步 {Count} 筆新資料 略過 {Skipped} 筆重複", toInsert.Count, incoming.Count - toInsert.Count	);
		}

		private PesticideViolation? MapToEntity(PesticideViolationDto dto)
		{
			try
			{
				return new PesticideViolation
				{
					Number = dto.Number,
					SamplingDate = DateHelper.ParseRocNumericDate(dto.SamplingDate),
					ProductName = dto.ProductName,
					ProductId = dto.ProductId,
					ProducerName = dto.ProducerName,
					SamplingLocation = dto.SamplingLocation,
					InspectResult = dto.InspectResult,
					Note = dto.Note,
					SyncedAt = DateTime.UtcNow
				};
			}
			catch (ArgumentException ex)
			{
				_logger.LogWarning("[PesticideViolationSync] 日期轉換失敗，跳過此筆。Number: {Number}, SamplingDate: {Date}, 原因: {Message}",
					dto.Number, dto.SamplingDate, ex.Message);
				return null;
			}
		}
	}
}
