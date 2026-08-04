using System.Globalization;
using TaiwanAgri.Core.Infrastructure.Data;
using TaiwanAgri.Modules.Pet.Data;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;
using TaiwanAgri.Modules.Pet.Dtos.WorkerResponses;
using TaiwanAgri.Core.Constants;
using TaiwanAgri.Core.Helpers;
using TaiwanAgri.Core.Infrastructure.Entities;
using TaiwanAgri.Modules.Pet.Entities;
using TaiwanAgri.Modules.Pet.Entities.Enums;
using TaiwanAgri.Modules.Pet.Constants;
using TaiwanAgri.Worker;

namespace TaiwanAgri.Worker.Pet
{
	/// <summary>
	/// 同步農業部「合法特定寵物業」(LegalSpecificPet) 名單。
	/// 跟 AnimalRecognitionSyncWorker 相同的雙分支結構（決策25）：SyncState 不存在→回填分支，
	/// 打舊制端點一次抓全量歷史資料；SyncState 存在→常態分支，打新制端點逐縣市（22 個代碼）
	/// 整批重掃。但常態分支的迴圈對象是「縣市代碼」而不是「日期」——這個資料集沒有可用的
	/// 異動時間欄位可以篩「今天新增了什麼」（決策25），所以無法比照 AnimalRecognition/PetLoseList
	/// 用日期區間縮小範圍，只能每天整批重掃 22 個縣市。也因此落地用 upsert 而非 insert-only：
	/// 業者的評鑑等級/營業狀態會隨時間變動（今年優等、明年甲等；正常營業後來歇業），
	/// 只新增不更新會讓資料悄悄過期失真（詳見 LegalSpecificPet.cs 開頭註解）。
	/// </summary>
	public class LegalSpecificPetSyncWorker : ScheduledSyncWorkerBase
	{
		private const string SyncKey = "Pet_LegalSpecificPet";

		private readonly ILogger<LegalSpecificPetSyncWorker> _logger;
		private readonly HttpClient _httpClient;
		private readonly IServiceScopeFactory _scopeFactory;
		private readonly TimeProvider _timeProvider;
		private readonly IConfiguration _configuration;

		public LegalSpecificPetSyncWorker(
			ILogger<LegalSpecificPetSyncWorker> logger,
			IHttpClientFactory httpClientFactory,
			IServiceScopeFactory scopeFactory,
			TimeProvider timeProvider,
			IConfiguration configuration)
			: base(logger)
		{
			_logger = logger;
			_httpClient = httpClientFactory.CreateClient("MoaApi");
			_scopeFactory = scopeFactory;
			_timeProvider = timeProvider;
			_configuration = configuration;
		}

		protected override TimeSpan Interval => TimeSpan.FromDays(1); // 每天執行一次
		protected override string LogPrefix => "[LegalSpecificPetSyncWorker]";

		protected override async Task SyncAsync(CancellationToken stoppingToken)
		{
			using var scope = _scopeFactory.CreateScope();
			var dbPet = scope.ServiceProvider.GetRequiredService<PetDbContext>();
			var dbCore = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

			var lastSyncState = await dbCore.SyncStates
				.FirstOrDefaultAsync(s => s.SyncKey == SyncKey, cancellationToken: stoppingToken);

			if (lastSyncState == null)
			{
				// ===== 回填分支：只會執行這一次（執行完會建立 SyncState，下次進來就不是 null 了）=====
				// 舊制端點裸陣列格式，一次拿全量（決策25 實測 5845 筆，文件雖寫 1000 筆上限但實測不符，
				// 判斷容量沒有正式保證，只適合當一次性回填起點）。資料量偏大，比照 AnimalRecognitionLegacy
				// 的做法，用獨立 CancellationTokenSource 蓋掉共用 "MoaApi" client 的預設 120 秒逾時。
				var httpTimeoutSeconds = _configuration.GetValue<int>("LegalSpecificPetSyncWorker:LegacyFetchTimeoutSeconds", 300);
				using var httpTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(httpTimeoutSeconds));
				var legacyDtos = await _httpClient.GetFromJsonAsync<List<LegalSpecificPetDto>>(
					MoaApiEndpoints.LegalSpecificPetLegacy, httpTimeoutCts.Token) ?? new List<LegalSpecificPetDto>();

				await UpsertBatchAsync(dbPet, legacyDtos, _timeProvider, _logger, LogPrefix, stoppingToken);

				// 全部成功後才建立 SyncState（回填完成判斷機制，比照決策12／AnimalRecognitionSyncWorker：
				// 這行程式碼本身就是「回填完成」的證明，上面任一步拋例外就不會執行到這裡，
				// 下一輪 SyncState 仍是 null，整段回填會重跑一次——upsert 天生不怕重跑）。
				lastSyncState = new SyncState
				{
					SyncKey = SyncKey,
					LastSyncedDate = TaiwanTime.Today(_timeProvider),
					UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime
				};
				dbCore.SyncStates.Add(lastSyncState);
				await dbCore.SaveChangesAsync(stoppingToken);
			}
			else
			{
				// ===== 常態分支：從第一次 SyncState 建立後，每天都會走這裡 =====
				// 迴圈對象是「縣市代碼」不是「日期」：這個資料集沒有可用的異動時間欄位（決策25），
				// 沒辦法像其他兩支 Worker 那樣篩「今天新增了什麼」，只能每天把 22 個縣市全部重新掃
				// 一次，靠 upsert 讓評鑑等級/營業狀態的變動蓋上去。
				foreach (var (code, countyName) in LegalPetCounties.CodeToName)
				{
					var url = $"{MoaApiEndpoints.LegalSpecificPet}?legaltype={code}";

					// 故意不包 try/catch：例外直接往外拋，由 ScheduledSyncWorkerBase.ExecuteAsync
					// 的全域 catch 接住記 log。這裡沒有「跑到一半」的中繼狀態需要保留——中斷後
					// 明天整輪重跑就是了，不像 PetLoseList 需要煩惱 checkpoint 粒度。
					var response = await _httpClient.GetFromJsonAsync<LegalSpecificPetApiResponse>(url, stoppingToken);

					if (response?.RS != "OK" || response.Data == null || response.Data.Count == 0)
					{
						_logger.LogInformation("{LogPrefix} {County}（{Code}）無資料", LogPrefix, countyName, code);
						continue;
					}

					// 已知風險，非本輪處理範圍（決策25）：未登入查詢非會員只回第一頁，
					// 若單一縣市真實筆數超過 1000 會安靜漏資料、不會噴例外。目前最大新北市 884 筆，
					// 離門檻還有距離，先不處理分頁（YAGNI，等真的逼近門檻再回頭處理）。
					await UpsertBatchAsync(dbPet, response.Data, _timeProvider, _logger, LogPrefix, stoppingToken);

					_logger.LogInformation("{LogPrefix} {County}（{Code}）同步完成，{Count} 筆",
						LogPrefix, countyName, code, response.Data.Count);
				}

				// 這支 Worker 的 LastSyncedDate 不像其他兩支拿來算「下次要抓哪天」——
				// 常態分支每天本來就是全量重掃 22 個縣市，不看這個值決定要抓什麼範圍。
				// 這裡純粹當作「上次成功完整跑完是哪天」的觀測用途。
				lastSyncState.LastSyncedDate = TaiwanTime.Today(_timeProvider);
				lastSyncState.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
				await dbCore.SaveChangesAsync(stoppingToken);
			}
		}

		/// <summary>
		/// 把一批 DTO 轉成 Entity 後 upsert 落地。回填分支（全量無縣市過濾）跟常態分支
		/// （單一縣市過濾）共用同一套邏輯——DTO 本身就帶 legaltype（決策25：連舊制回填資料
		/// 也是靠這個欄位分組反查出縣市代碼對照表），不需要呼叫端額外傳入縣市代碼。
		/// </summary>
		private static async Task UpsertBatchAsync(
			PetDbContext dbPet,
			List<LegalSpecificPetDto> dtos,
			TimeProvider timeProvider,
			ILogger logger,
			string logPrefix,
			CancellationToken stoppingToken)
		{
			var incoming = dtos
				.Select(dto => MapToEntity(dto, timeProvider, logger))
				.DistinctBy(x => x.ExternalId)
				.ToList();

			if (incoming.Count == 0) return;

			// existingEntities 刻意縮小成「這批 ExternalId 命中的既有資料」，不像 PetLoseList
			// 的 existingKeys 選擇全表掃描——這裡的過濾條件就是等一下要比對的那把鍵本身
			// （ExternalId IN 這批清單），不是「假設某個間接欄位等於查詢參數」那種未驗證的關聯，
			// 沒有決策26 討論過的那種正確性風險，可以放心縮小範圍。
			var incomingIds = incoming.Select(x => x.ExternalId).ToList();
			var existingEntities = dbPet.LegalSpecificPets.Where(x => incomingIds.Contains(x.ExternalId));

			await DbSyncHelper.UpsertByKeyAsync(
				dbPet,
				existingEntities,
				incoming,
				x => x.ExternalId,
				ApplyUpdate,
				logger,
				logPrefix,
				stoppingToken);
		}

		/// <summary>existing 是被 EF 追蹤的既有實體（要修改的對象），incoming 是這批新抓到的資料（值的來源）</summary>
		private static void ApplyUpdate(LegalSpecificPet existing, LegalSpecificPet incoming)
		{
			existing.County = incoming.County;
			existing.BusinessItems = incoming.BusinessItems;
			existing.AnimalType = incoming.AnimalType;
			existing.Name = incoming.Name;
			existing.Address = incoming.Address;
			existing.PermitNumber = incoming.PermitNumber;
			existing.PermitValidDate = incoming.PermitValidDate;
			existing.OwnerName = incoming.OwnerName;
			existing.ResponsibleStaffName = incoming.ResponsibleStaffName;
			existing.RankYear = incoming.RankYear;
			existing.RankGrade = incoming.RankGrade;
			existing.RankDataConfirmed = incoming.RankDataConfirmed;
			existing.RankDescriptionConfirmed = incoming.RankDescriptionConfirmed;
			existing.RankText = incoming.RankText;
			existing.StateFlag = incoming.StateFlag;
			existing.SyncedAt = incoming.SyncedAt;
			// ExternalId 是業務鍵、County 對應的 legaltype 理論上不會變，故意不覆寫 Id（EF 技術 PK）
		}

		/// <summary>
		/// 把 API 回應的原始 DTO 轉成要存進 DB 的 Entity。
		/// internal static：不依賴任何實例欄位，方便單元測試直接呼叫驗證映射邏輯。
		/// </summary>
		internal static LegalSpecificPet MapToEntity(LegalSpecificPetDto dto, TimeProvider timeProvider, ILogger logger)
		{
			return new LegalSpecificPet
			{
				ExternalId = dto.ID,

				// legaltype 對照表已用真實資料反查驗證（決策25），查不到理論上不該發生，
				// 保底 fallback 直接存原始代碼字串（至少留下線索可查，不會存成空字串）
				County = LegalPetCounties.CodeToName.TryGetValue(dto.LegalType, out var countyName)
					? countyName
					: EnumMappingHelper.LogUnexpectedValue(dto.ID, nameof(dto.LegalType), dto.LegalType, dto.LegalType, logger),

				BusinessItems = dto.BusItem,

				// animaltype 原始值目前只用真實資料反查過「狗」「貓」兩種單一字面值（決策25 樣本），
				// 「狗、貓皆可」的組合值實際分隔符號未經樣本驗證，用 Contains 而非精確字串比對，
				// 之後跑真實資料若 LogUnexpectedValue 頻繁出現要回頭核對這個假設
				AnimalType = dto.AnimalType switch
				{
					var t when t.Contains('狗') && t.Contains('貓') => LegalPetAnimalType.Both,
					"狗" => LegalPetAnimalType.Dog,
					"貓" => LegalPetAnimalType.Cat,
					_ => EnumMappingHelper.LogUnexpectedValue(dto.ID, nameof(dto.AnimalType), dto.AnimalType, LegalPetAnimalType.Other, logger)
				},

				Name = dto.LegalName,
				Address = dto.LegalAddress,
				PermitNumber = dto.ValidNum,
				PermitValidDate = ParseValidDate(dto.ValidDate),
				OwnerName = dto.OwnName,
				ResponsibleStaffName = dto.BosName,
				RankYear = dto.RankYear,

				// rank_code：官方文件這張對照表排版清晰無爭議（決策25），A=優等 B=甲等 C=乙等 D=丙等
				RankGrade = dto.RankCode switch
				{
					"A" => LegalPetRankGrade.Excellent,
					"B" => LegalPetRankGrade.GradeA,
					"C" => LegalPetRankGrade.GradeB,
					"D" => LegalPetRankGrade.GradeC,
					_ => EnumMappingHelper.LogUnexpectedValue(dto.ID, nameof(dto.RankCode), dto.RankCode, LegalPetRankGrade.Unknown, logger)
				},

				// rank_flag_1/2：Y/N 是字面轉譯不是語意詮釋（決策25：不確定「確認/未確認」實際
				// 對應哪個代碼，但至少 Y→Yes／N→No 的方向不會錯）
				RankDataConfirmed = dto.RankFlag1 switch
				{
					"Y" => TriState.Yes,
					"N" => TriState.No,
					_ => EnumMappingHelper.LogUnexpectedValue(dto.ID, nameof(dto.RankFlag1), dto.RankFlag1, TriState.Unknown, logger)
				},
				RankDescriptionConfirmed = dto.RankFlag2 switch
				{
					"Y" => TriState.Yes,
					"N" => TriState.No,
					_ => EnumMappingHelper.LogUnexpectedValue(dto.ID, nameof(dto.RankFlag2), dto.RankFlag2, TriState.Unknown, logger)
				},

				RankText = dto.RankText,

				// state_flag：PDF 對照表順序錯位，改用 validdate 交叉比對真實資料反查驗證（決策25，
				// 1000 筆樣本）：N→營業中(高信心) B→廢止(高信心) P→歇業(中信心) S→停業(中信心)
				StateFlag = dto.StateFlag switch
				{
					"N" => LegalPetStateFlag.Operating,
					"B" => LegalPetStateFlag.Revoked,
					"P" => LegalPetStateFlag.Closed,
					"S" => LegalPetStateFlag.Suspended,
					_ => EnumMappingHelper.LogUnexpectedValue(dto.ID, nameof(dto.StateFlag), dto.StateFlag, LegalPetStateFlag.Unknown, logger)
				},

				SyncedAt = timeProvider.GetUtcNow().UtcDateTime
			};
		}

		/// <summary>
		/// validdate 原始格式為 "2028/3/12 上午 12:00:00"（決策25），含中文上午/下午時段字樣，
		/// DateOnly.ParseExact 對這種格式無法直接處理。明確指定 zh-TW 文化與對應格式字串解析
		/// （不依賴執行環境的預設文化，避免跟 PetLoseList 的 LostTime 一樣需要顧慮文化設定誤判），
		/// 再取日期部分（時間固定是 00:00:00，不會遺失資訊）。
		/// internal static：格式字串沒有正式規格保證，方便單元測試直接釘住這個假設，
		/// 比照 MapToEntity 的既有做法。
		/// </summary>
		internal static DateOnly? ParseValidDate(string raw)
		{
			if (string.IsNullOrWhiteSpace(raw)) return null;
			var parsed = DateTime.ParseExact(raw, "yyyy/M/d tt h:mm:ss", CultureInfo.GetCultureInfo("zh-TW"));
			return DateOnly.FromDateTime(parsed);
		}
	}
}
