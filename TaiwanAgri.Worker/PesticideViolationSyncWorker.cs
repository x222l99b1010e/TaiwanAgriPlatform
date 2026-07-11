using Microsoft.EntityFrameworkCore;
using TaiwanAgri.Core.Constants;
using TaiwanAgri.Core.Helpers;
using TaiwanAgri.Modules.FoodSafety.Data;
using TaiwanAgri.Modules.FoodSafety.Dtos.WorkerResponses;
using TaiwanAgri.Modules.FoodSafety.Entities;

namespace TaiwanAgri.Worker
{
	public class PesticideViolationSyncWorker : ScheduledSyncWorkerBase
	{
		private readonly ILogger<PesticideViolationSyncWorker> _logger;
		private readonly HttpClient _httpClient;
		private readonly IServiceScopeFactory _scopeFactory;

		public PesticideViolationSyncWorker(ILogger<PesticideViolationSyncWorker> logger, IHttpClientFactory httpClientFactory, IServiceScopeFactory scopeFactory)
			: base(logger)
		{
			_logger = logger;
			_httpClient = httpClientFactory.CreateClient("MoaApi");
			_scopeFactory = scopeFactory;
		}

		protected override TimeSpan Interval => TimeSpan.FromDays(1); // 正式排程每1天一次
		protected override string LogPrefix => "[PesticideViolationSync]";

		protected override async Task SyncAsync(CancellationToken stoppingToken)
		{
			using var scope = _scopeFactory.CreateScope();
			var db = scope.ServiceProvider.GetRequiredService<FoodSafetyDbContext>();

			// 分頁抓取邏輯統一由 MoaPagedFetcher 處理，與 OrganicCertificationSyncWorker 共用
			var allDtos = await MoaPagedFetcher.FetchAllPagesAsync<PesticideViolationApiResponse, PesticideViolationDto>(
				_httpClient, MoaApiEndpoints.PesticideViolation, _logger, "[PesticideViolationSync]", stoppingToken);

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

			var existingNumbers = await db.PesticideViolations
				.Select(x => x.Number)
				.ToHashSetAsync(stoppingToken);

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
