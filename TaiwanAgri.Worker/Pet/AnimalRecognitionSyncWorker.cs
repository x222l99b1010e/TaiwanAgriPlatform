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
using TaiwanAgri.Worker;

namespace TaiwanAgri.Worker.Pet
{
	public class AnimalRecognitionSyncWorker : ScheduledSyncWorkerBase
	{
		private readonly ILogger<AnimalRecognitionSyncWorker> _logger;
		private readonly HttpClient _httpClient;
		private readonly IServiceScopeFactory _scopeFactory;
		private readonly TimeProvider _timeProvider;
		private readonly IConfiguration _configuration;
		public AnimalRecognitionSyncWorker
			(ILogger<AnimalRecognitionSyncWorker> logger, IHttpClientFactory httpClientFactory, IServiceScopeFactory scopeFactory, TimeProvider timeProvider, IConfiguration configuration)
			: base(logger)
		{
			_logger = logger;
			_httpClient = httpClientFactory.CreateClient("MoaApi");
			_scopeFactory = scopeFactory;
			_timeProvider = timeProvider;
			_configuration = configuration;
		}
		protected override TimeSpan Interval => TimeSpan.FromDays(1); // 每天執行一次
		protected override string LogPrefix => "[AnimalRecognitionSyncWorker]";
		protected override async Task SyncAsync(CancellationToken stoppingToken)
		{
			using var scope = _scopeFactory.CreateScope();
			var dbPet = scope.ServiceProvider.GetRequiredService<PetDbContext>();
			var dbCore = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

			var lastSyncState = await dbCore.SyncStates
				.FirstOrDefaultAsync(s => s.SyncKey == "Pet_AnimalRecognition", cancellationToken: stoppingToken);

			if (lastSyncState == null)
			{
				// ===== 回填分支：只會執行這一次（因為執行完會建立 SyncState，下次進來就不是 null 了）=====
				// 1. 打舊制 API，一次抓全部現存資料（裸陣列，不經過 MoaPagedFetcher 的分頁包裝）
				// 舊制一次回填 8000+ 筆，資料量大，不能沿用 "MoaApi" client 的預設 120 秒逾時，
				// 比照 AgriProductsTransSyncWorker 用獨立 CancellationTokenSource 蓋掉共用逾時設定
				var httpTimeoutSeconds = _configuration.GetValue<int>("AnimalRecognitionSyncWorker:LegacyFetchTimeoutSeconds", 300);
				using var httpTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(httpTimeoutSeconds));
				//var legacyDtos = await _httpClient.GetFromJsonAsync<List<ShelterAnimalDto>>(
				//	MoaApiEndpoints.AnimalRecognitionLegacy, stoppingToken) ?? new List<ShelterAnimalDto>();
				var legacyDtos = await _httpClient.GetFromJsonAsync<List<ShelterAnimalDto>>(
					MoaApiEndpoints.AnimalRecognitionLegacy, httpTimeoutCts.Token) ?? new List<ShelterAnimalDto>();

				// 2. 映射成 Entity
				var incoming = legacyDtos
					.Select(dto => MapToEntity(dto, _timeProvider, _logger))
					.DistinctBy(x => new { x.ShelterPkId, x.AnimalSubId })
					.ToList();

				// 3. 動物資料落地前，先確保這批資料涉及的收容所都已存在（決策21 防禦機制）
				//    吃 legacyDtos（原始 DTO），不是 incoming——ShelterName/Address/Tel 只在 DTO 上還在
				await EnsureSheltersExistAsync(dbPet, legacyDtos, _logger, LogPrefix, stoppingToken);

				// 4. 落地（複合鍵：ShelterPkId + AnimalSubId， Unique Index）
				var existingKeys = dbPet.ShelterAnimals
					.Select(x => new { x.ShelterPkId, x.AnimalSubId });

				await DbSyncHelper.InsertNewByKeyAsync(
					dbPet,
					existingKeys,
					incoming,
					x => new { x.ShelterPkId, x.AnimalSubId },
					_logger,
					LogPrefix,
					stoppingToken);

				// 5. 全部成功後，才建立 SyncState（回填完成判斷機制）
				// 全部寫入成功「之後」才建立 SyncState——這是決策 12 的核心：
				// 這行程式碼本身就是「回填完成」的證明。如果上面任何一步拋出例外，
				// 這行永遠不會執行到，SyncState 就不會被建立，下一輪（明天）又會是 null，
				// 整段回填會重跑一次（DbSyncHelper 靠複合鍵去重，重跑不會產生重複資料，
				// 只是白白多打一次 API，不會弄壞資料正確性）。
				lastSyncState = new SyncState
				{
					SyncKey = "Pet_AnimalRecognition",
					LastSyncedDate = TaiwanTime.Today(_timeProvider).AddDays(-15), // 15 天緩衝
					UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime
				};
				dbCore.SyncStates.Add(lastSyncState);
				await dbCore.SaveChangesAsync(stoppingToken);
			}
			else
			{
				// ===== 增量分支：從第一次 SyncState 建立後，每天都會走這裡 =====
				DateOnly startDate = lastSyncState.LastSyncedDate.AddDays(1);
				DateOnly yesterdayDate = TaiwanTime.Today(_timeProvider).AddDays(-1);
				// 只追到「昨天」為止、不含「今天」：今天的資料可能還在持續產生（收容所還在登打），
				// 抓「今天」容易抓到不完整的一天，明天再抓「今天」（那時候它已經變成「昨天」）反而更準確。
				// 這個邊界跟 AgriProductsTransSyncWorker 的 yesterdayDate 慣例一致。

				for (var currentDate = startDate; currentDate <= yesterdayDate; currentDate = currentDate.AddDays(1))
				{
					_logger.LogInformation("{LogPrefix} 開始同步日期: {Date}", LogPrefix, currentDate);

					// 1. 組 URL：$top=1000（避免踩到未登入預設分頁上限）+ animal_createtime 篩單日
					//    日期格式用斜線 yyyy/MM/dd，跟 animal_createtime 欄位本身格式一致
					//    （決策 7 已實測驗證：篩 2024/07/01 拿到 2 筆，跟舊制全量資料核對一致）
					var url = $"{MoaApiEndpoints.AnimalRecognition}?$top=1000&animal_createtime={currentDate:yyyy/MM/dd}";

					// 2. 打新制 API，反序列化成 ShelterAnimalApiResponse（RS/Data/Next 包裝）
					//    這裡「不用 try/catch」是刻意的：一旦這行拋出例外（網路失敗、反序列化失敗等），
					//    直接讓例外往外傳，中斷這個 for 迴圈、中斷整個 SyncAsync。
					//    外層 ScheduledSyncWorkerBase.ExecuteAsync 有全域 try/catch 會接住它、記 log，
					//    但「不會」讓 currentDate 這天的 LastSyncedDate 被推進（因為程式根本沒執行到推進那一行）。
					//    下一輪（明天）SyncAsync 再跑一次時，startDate 還是同一個失敗的日期，等於自動重試。
					var response = await _httpClient.GetFromJsonAsync<ShelterAnimalApiResponse>(url, stoppingToken);

					if (response?.RS != "OK" || response.Data == null || response.Data.Count == 0)
					{
						_logger.LogInformation("{LogPrefix} {Date} 無新資料", LogPrefix, currentDate);
					}
					else
					{
						// 3. 動物資料落地前，先確保這批資料涉及的收容所都已存在（決策21 防禦機制）
						//    吃 response.Data（原始 DTO），不是 incoming——ShelterName/Address/Tel 只在 DTO 上還在
						await EnsureSheltersExistAsync(dbPet, response.Data, _logger, LogPrefix, stoppingToken);

						// 4. 映射成 Entity（跟回填分支共用同一個 MapToEntity，不重複寫轉換邏輯）
						var incoming = response.Data
							.Select(dto => MapToEntity(dto, _timeProvider, _logger))
							.DistinctBy(x => new { x.ShelterPkId, x.AnimalSubId })
							.ToList();

						// 5. 落地：複合鍵去重（同一天內，也可能跟回填階段已存在的資料重複，
						//    DbSyncHelper 會自動過濾掉，不會產生重複列）
						var existingKeys = dbPet.ShelterAnimals.Select(x => new { x.ShelterPkId, x.AnimalSubId });
						await DbSyncHelper.InsertNewByKeyAsync(
							dbPet, existingKeys, incoming,
							x => new { x.ShelterPkId, x.AnimalSubId },
							_logger, LogPrefix, stoppingToken);
					}

					// 6. 這一天處理完畢（不管有沒有新資料，只要沒拋例外就算成功）才推進 LastSyncedDate
					//    這是整個 checkpoint 機制的關鍵：如果程式在第 3、4、5 步中途死掉，
					//    這一行就不會執行到，currentDate 這天不會被標記完成，下次會重跑。
					lastSyncState.LastSyncedDate = currentDate;
					lastSyncState.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
					await dbCore.SaveChangesAsync(stoppingToken);

					_logger.LogInformation("{LogPrefix} {Date} 同步完成", LogPrefix, currentDate);
				}
			}
		}

		internal static ShelterAnimal MapToEntity(ShelterAnimalDto dto, TimeProvider timeProvider, ILogger logger)
		{
			return new ShelterAnimal
			{
				AnimalSubId = dto.AnimalSubId,
				ShelterPkId = dto.AnimalShelterPkId,
				Kind = dto.AnimalKind switch
				{
					"狗" => AnimalKind.Dog,
					"貓" => AnimalKind.Cat,
					_ => EnumMappingHelper.LogUnexpectedValue(dto.AnimalSubId, nameof(dto.AnimalKind), dto.AnimalKind, AnimalKind.Other, logger)
				},
				Sex = dto.AnimalSex switch
				{
					"M" => AnimalSex.Male,
					"F" => AnimalSex.Female,
					_ => EnumMappingHelper.LogUnexpectedValue(dto.AnimalSubId, nameof(dto.AnimalSex), dto.AnimalSex, AnimalSex.Other, logger)
				},
				BodyType = dto.AnimalBodyType switch
				{
					"SMALL" => AnimalBodyType.Small,
					"MEDIUM" => AnimalBodyType.Medium,
					"BIG" => AnimalBodyType.Big,
					_ => EnumMappingHelper.LogUnexpectedValue(dto.AnimalSubId, nameof(dto.AnimalBodyType), dto.AnimalBodyType, AnimalBodyType.Other, logger)
				},
				Age = dto.AnimalAge switch
				{
					"CHILD" => AnimalAge.Child,
					"ADULT" => AnimalAge.Adult,
					_ => EnumMappingHelper.LogUnexpectedValue(dto.AnimalSubId, nameof(dto.AnimalAge), dto.AnimalAge, AnimalAge.Other, logger)
				},
				Sterilization = dto.AnimalSterilization switch
				{
					"T" => TriState.Yes,
					"F" => TriState.No,
					"N" => TriState.Unknown,
					_ => EnumMappingHelper.LogUnexpectedValue(dto.AnimalSubId, nameof(dto.AnimalSterilization), dto.AnimalSterilization, TriState.Unknown, logger)
				},
				Bacterin = dto.AnimalBacterin switch
				{
					"T" => TriState.Yes,
					"F" => TriState.No,
					"N" => TriState.Unknown,
					_ => EnumMappingHelper.LogUnexpectedValue(dto.AnimalSubId, nameof(dto.AnimalBacterin), dto.AnimalBacterin, TriState.Unknown, logger)
				},
				Variety = dto.AnimalVariety.Trim(),
				Colour = dto.AnimalColour,
				FoundPlace = dto.AnimalFoundPlace,
				Remark = dto.AnimalRemark,
				OpenDate = string.IsNullOrWhiteSpace(dto.AnimalOpenDate) ? null : DateOnly.ParseExact(dto.AnimalOpenDate, "yyyy-MM-dd"),
				CreatedTime = DateOnly.ParseExact(dto.AnimalCreateTime, "yyyy/MM/dd"),
				AlbumFile = dto.AlbumFile,
				SyncedAt = timeProvider.GetUtcNow().UtcDateTime
			};
		}

		/// <summary>
		/// 收容所清單會隨時間浮動（決策22），若這批動物資料的 ShelterPkId 不在 Shelters 表裡，
		/// 先自動建立一筆座標留空的 placeholder 記錄，避免動物資料落地時觸發 FK 崩潰（決策21）。
		/// 範圍界線：只補「這筆收容所存不存在」，不做地理編碼（YAGNI，決策21）。
		/// </summary>
		internal static async Task EnsureSheltersExistAsync(
			PetDbContext dbPet,
			IEnumerable<ShelterAnimalDto> dtos,
			ILogger logger,
			string logPrefix,
			CancellationToken stoppingToken)
		{
			// 1. 同一間收容所會對應很多隻動物，用 GroupBy 找出「每個 ShelterPkId 各留一筆代表 DTO」
			//    （代表 DTO 用來拿 ShelterName/ShelterAddress/ShelterTel——MapToEntity 轉出的 ShelterAnimal 不會帶這些欄位）
			var representativeDtos = dtos
				.GroupBy(x => x.AnimalShelterPkId)
				.Select(g => g.First())
				.ToList();

			// 2. 查 Shelters 本身（不是 ShelterAnimals），找出這批 ShelterPkId 裡哪些已經存在
			//    ShelterPkId 是 Shelters 的 PK，天生唯一，不需要再 Distinct
			var incomingShelterPkIds = representativeDtos.Select(x => x.AnimalShelterPkId);
			var existingShelterPkIds = await dbPet.Shelters
				.Where(s => incomingShelterPkIds.Contains(s.ShelterPkId))
				.Select(s => s.ShelterPkId)
				.ToListAsync(stoppingToken);

			// 3. 篩出真正缺的收容所，保留整包 DTO（建 Shelter 需要 Name/Address/Tel）
			var missingDtos = representativeDtos
				.Where(x => !existingShelterPkIds.Contains(x.AnimalShelterPkId))
				.ToList();

			if (missingDtos.Count == 0) return;

			// 4. 建立座標留空的 placeholder Shelter：
			//    Name/Address/Tel 有真實資料就用真實資料；County 目前無任何可解讀來源
			//    （animal_area_pkid 是代碼、無對照表，決策12已排除），一律待補。
			var newShelters = missingDtos.Select(dto => new Shelter
			{
				ShelterPkId = dto.AnimalShelterPkId,
				Name = string.IsNullOrWhiteSpace(dto.ShelterName) ? "新增收容所，資料待補" : dto.ShelterName,
				Address = string.IsNullOrWhiteSpace(dto.ShelterAddress) ? "資料待補" : dto.ShelterAddress,
				Tel = string.IsNullOrWhiteSpace(dto.ShelterTel) ? "資料待補" : dto.ShelterTel,
				County = "資料待補",
				Latitude = null,
				Longitude = null
			}).ToList();

			await dbPet.Shelters.AddRangeAsync(newShelters, stoppingToken);
			await dbPet.SaveChangesAsync(stoppingToken);

			logger.LogWarning("{LogPrefix} 自動建立 {Count} 筆座標留空的收容所，ShelterPkId: {Ids}",
				logPrefix, newShelters.Count, string.Join(",", newShelters.Select(s => s.ShelterPkId)));
		}
	}
}