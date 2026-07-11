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
				.Select(dto => MapToEntity(dto, _logger))
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

			// 既有鍵以「本批最舊取樣日」為視窗下界，避免對隨年份無上限成長的違規表全表掃描：
			// API 每次只回近期資料，且同一 Number 的 SamplingDate 不會變動，
			// 視窗之外不可能出現與本批重複的 Number
			var oldestSamplingDate = incoming.Min(x => x.SamplingDate);

			await DbSyncHelper.InsertNewByKeyAsync(
				db,
				db.PesticideViolations
					.Where(x => x.SamplingDate >= oldestSamplingDate)
					.Select(x => x.Number),
				incoming,
				x => x.Number,
				_logger, "[PesticideViolationSync]", stoppingToken);
		}

		/// <summary>
		/// 將單筆 DTO 轉換為 Entity；日期轉換失敗記 warning 並回傳 null（整筆跳過）。
		/// 比照 OrganicCertificationSyncWorker.MapToEntities 的 internal static 模式：
		/// 純資料轉換不依賴 Worker 實例狀態，static 化後測試不需 Mock 建構子依賴
		/// </summary>
		internal static PesticideViolation? MapToEntity(PesticideViolationDto dto, ILogger logger)
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
				logger.LogWarning("[PesticideViolationSync] 日期轉換失敗，跳過此筆。Number: {Number}, SamplingDate: {Date}, 原因: {Message}",
					dto.Number, dto.SamplingDate, ex.Message);
				return null;
			}
		}
	}
}
