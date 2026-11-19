using Microsoft.EntityFrameworkCore;
using TaiwanAgri.Core.Constants;
using TaiwanAgri.Core.Helpers;
using TaiwanAgri.Modules.FoodSafety.Data;
using TaiwanAgri.Modules.FoodSafety.Dtos.WorkerResponses;
using TaiwanAgri.Modules.FoodSafety.Entities;
using TaiwanAgri.Worker;

namespace TaiwanAgri.Worker.FoodSafety
{
	public class OrganicCertificationSyncWorker : ScheduledSyncWorkerBase
	{
		private readonly ILogger<OrganicCertificationSyncWorker> _logger;
		private readonly HttpClient _httpClient;
		private readonly IServiceScopeFactory _scopeFactory;

		public OrganicCertificationSyncWorker(ILogger<OrganicCertificationSyncWorker> logger, IHttpClientFactory httpClientFactory, IServiceScopeFactory scopeFactory)
			: base(logger)
		{
			_logger = logger;
			_httpClient = httpClientFactory.CreateClient("MoaApi");
			_scopeFactory = scopeFactory;
		}

		protected override TimeSpan Interval => TimeSpan.FromDays(1); // 正式排程每1天一次
		protected override string LogPrefix => "[OrganicCertificationSync]";

		protected override async Task SyncAsync(CancellationToken stoppingToken)
		{
			// 用 IServiceScopeFactory 建立獨立 Scope，才能在這個 Singleton Worker 裡安全注入 Scoped 的 DbContext
			using var scope = _scopeFactory.CreateScope();
			var db = scope.ServiceProvider.GetRequiredService<FoodSafetyDbContext>();

			// 分頁抓取邏輯（第一頁不帶參數、RS 判斷、Next 旗標、20 頁保護上限）
			// 統一由 MoaPagedFetcher 處理，與 PesticideViolationSyncWorker 共用
			var allDtos = await MoaPagedFetcher.FetchAllPagesAsync<OrganicCertificationApiResponse, OrganicCertificationDto>(
				_httpClient, MoaApiEndpoints.OrganicVerification, _logger, "[OrganicCertificationSync]", stoppingToken);

			// 與 PesticideViolation 的關鍵差異：
			// PesticideViolation 是 Select + Where(!=null)，一筆 DTO 對應零或一筆 Entity
			// 這裡是 SelectMany，因為 CertOrganicSn 異值並存時，一筆 DTO 要拆成多筆 Entity（見 MapToEntities 註解）
			// DistinctBy 在 SelectMany 展開「之後」執行，確保拆分出來的每一筆都各自參與批次內去重判斷
			var incoming = allDtos
				.SelectMany(dto => MapToEntities(dto, _logger))
				.DistinctBy(x => x.CertOrganicSn)
				.ToList();

			if (incoming.Count == 0)
			{
				_logger.LogWarning("[OrganicCertificationSync] 全部資料轉換失敗或無有效資料");
				return;
			}

			// 有則更新、沒有才新增：證書狀態（通過、結束、終止、暫終）在同步之後會變，
			// 原本只新增、證號已存在就整筆略過，狀態變了資料庫不會跟著改
			// （2026-11 本機實測：1,006 張共有的證書裡 32 張狀態跟上游不同，總檢查 3-B6）。
			// 跟合法寵物業同一個理由、同一個做法；批次內去重（上面的 DistinctBy）照樣要先做
			var incomingSns = incoming.Select(x => x.CertOrganicSn).ToList();
			await DbSyncHelper.UpsertByKeyAsync(
				db,
				db.OrganicCertifications.Where(x => incomingSns.Contains(x.CertOrganicSn)),
				incoming,
				x => x.CertOrganicSn,
				ApplyUpdate,
				_logger, "[OrganicCertificationSync]", stoppingToken);
		}

		/// <summary>
		/// existing 是被 EF 追蹤的既有實體（要修改的對象），incoming 是這批新抓到的資料（值的來源）。
		/// 證號是業務鍵、不覆寫；Id 是技術主鍵、不覆寫。EF 只會對真的有變動的欄位產生 UPDATE
		/// </summary>
		internal static void ApplyUpdate(OrganicCertification existing, OrganicCertification incoming)
		{
			existing.Name = incoming.Name;
			existing.Address = incoming.Address;
			existing.Tel = incoming.Tel;
			existing.Products = incoming.Products;
			existing.BehaviorType = incoming.BehaviorType;
			existing.CompanyName = incoming.CompanyName;
			existing.EffectiveDate = incoming.EffectiveDate;
			existing.Status = incoming.Status;
			existing.ContainCrops = incoming.ContainCrops;
			existing.MailingAddress = incoming.MailingAddress;
			existing.OldCertOrganicSn = incoming.OldCertOrganicSn;
			existing.IsMultiCertSource = incoming.IsMultiCertSource;
			existing.SyncedAt = incoming.SyncedAt;
		}

		/// <summary>
		/// 將單筆 DTO 轉換為一到多筆 Entity。
		/// CertOrganicSn 正規化後有三種結果：
		///   1. 單一值 → 回傳 1 筆 Entity
		///   2. 頓號分隔但去重後仍為單一值（同值重複的髒資料，如 "1-008-205501、1-008-205501"）→ 回傳 1 筆 Entity
		///   3. 頓號分隔且去重後仍有多值（異值並存，如 "1-009-110011、1-009-120840"，代表這筆 API 記錄
		///      實際上合併了多張證書）→ 拆分為多筆 Entity，每筆各自使用其中一個證號，
		///      並標記 IsMultiCertSource = true，因為 Products／ContainCrops 等欄位
		///      無法確定與哪一個證號精確對應，只能各自沿用同一份完整原始字串
		///
		/// 設計取捨：此方法設計為 static（logger 改用參數傳入，而非讀取 Worker 的 _logger 欄位），
		/// 與 PesticideViolationSyncWorker.MapToEntity（private 實例方法）的寫法不同。
		/// 原因：這段邏輯是純粹的資料轉換（不依賴 _httpClient、_scopeFactory 等 Worker 實例狀態），
		/// 若維持實例方法，xUnit 測試時必須先建構 OrganicCertificationSyncWorker 實例，
		/// 也就是要 Mock IHttpClientFactory、IServiceScopeFactory——但這兩個依賴測試邏輯根本用不到，
		/// 純粹是為了滿足建構子簽名而存在的儀式性程式碼。
		/// 改成 static 後，測試可直接呼叫 OrganicCertificationSyncWorker.MapToEntities(dto, NullLogger.Instance)，
		/// 不需 Mock 任何依賴。代價是與既有 Worker 的 Map 方法寫法不完全一致，
		/// 這裡判斷「可測試性帶來的好處」大於「風格一致性的些微落差」，故採用此設計。
		/// </summary>
		internal static List<OrganicCertification> MapToEntities(OrganicCertificationDto dto, ILogger logger)
		{
			var certSns = SplitCertOrganicSn(dto.CertOrganicSn);

			if (certSns.Length == 0)
			{
				logger.LogWarning("[OrganicCertificationSync] CertOrganicSn 為空，跳過此筆：{Name}", dto.Name);
				return new List<OrganicCertification>();
			}

			var effectiveDate = ParseEffectiveDate(dto.EffectiveDate, dto.CertOrganicSn, logger);
			var isMultiCertSource = certSns.Length > 1;

			if (isMultiCertSource)
			{
				logger.LogWarning("[OrganicCertificationSync] CertOrganicSn 異值並存，拆分為 {Count} 筆：{Raw}，Name={Name}",
					certSns.Length, dto.CertOrganicSn, dto.Name);
			}

			return certSns.Select(sn => new OrganicCertification
			{
				CertOrganicSn = sn,
				Name = dto.Name,
				Address = dto.Address,
				Tel = dto.Tel,
				Products = dto.Products,
				BehaviorType = dto.BehaviorType,
				CompanyName = dto.CompanyName,
				EffectiveDate = effectiveDate,
				Status = dto.Status,
				ContainCrops = dto.ContainCrops,
				MailingAddress = dto.MailingAddress,
				OldCertOrganicSn = dto.OldCertOrganicSN,
				IsMultiCertSource = isMultiCertSource,
				SyncedAt = DateTime.UtcNow
			}).ToList();
		}

		/// <summary>
		/// 正規化 CertOrganicSn：以頓號分隔、去除空白、去重。
		/// 回傳陣列長度為 1 時，代表單一值或同值重複；長度大於 1 時，代表異值並存
		/// </summary>
		internal static string[] SplitCertOrganicSn(string raw)
		{
			if (string.IsNullOrWhiteSpace(raw))
			{
				return Array.Empty<string>();
			}

			return raw.Split('、', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
					   .Distinct()
					   .ToArray();
		}

		/// <summary>
		/// 解析 EffectiveDate，原始格式固定為 "yyyy/MM/dd"（如 "2028/10/14"）。
		/// 解析失敗時記錄 warning 並回傳 null，刻意不採用 PesticideViolation 那種
		/// 「整筆記錄回傳 null、跳過寫入」的策略，因為 EffectiveDate 不是這個 Entity
		/// 的核心識別依據（CertOrganicSn 才是），不該讓次要欄位的解析失敗連累其他有效欄位一併遺失
		/// </summary>
		internal static DateOnly? ParseEffectiveDate(string raw, string certOrganicSnForLog, ILogger logger)
		{
			if (DateOnly.TryParseExact(raw, "yyyy/MM/dd",
				System.Globalization.CultureInfo.InvariantCulture,
				System.Globalization.DateTimeStyles.None,
				out var parsed))
			{
				return parsed;
			}

			logger.LogWarning("[OrganicCertificationSync] EffectiveDate 解析失敗：{Raw}，CertOrganicSn={Sn}", raw, certOrganicSnForLog);
			return null;
		}
	}
}