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
	/// <summary>
	/// 同步農業部「寵物遺失啟事」(PetLoseList) 官方資料。
	/// 設計比 AnimalRecognitionSyncWorker 單純：只有新制端點、單一迴圈，沒有回填/增量雙分支
	/// （找到舊制端點但只多拿約 20 個月資料，評估後否決，不值得多寫一套解析邏輯）。
	/// </summary>
	public class PetLoseListSyncWorker : ScheduledSyncWorkerBase
	{
		// SyncState 用這個 key 在 CoreDbContext.SyncStates 表裡認出「這是哪一支 Worker 的進度」
		private const string SyncKey = "Pet_PetLoseList";

		// 固定起始日：2018/01/01 之前的走失資料對「現在還在找的走失寵物」地圖沒有實用價值，
		// 但保留較長歷史可以累積更完整的資料與照片，取捨後選這個起點。
		// 2018~今天約 3131 天，屬一次性背景回填，遠低於「回到 1999/2000 年」那種
		// 近 10000 天、需要認真設計斷點續跑的量級。
		private static readonly DateOnly FixedStartDate = new(2018, 1, 1);

		private readonly ILogger<PetLoseListSyncWorker> _logger;
		private readonly HttpClient _httpClient;
		private readonly IServiceScopeFactory _scopeFactory;
		private readonly TimeProvider _timeProvider;
		private readonly IConfiguration _configuration;

		public PetLoseListSyncWorker(
			ILogger<PetLoseListSyncWorker> logger,
			IHttpClientFactory httpClientFactory,
			IServiceScopeFactory scopeFactory,
			TimeProvider timeProvider,
			IConfiguration configuration)
			: base(logger)
		{
			_logger = logger;
			// "MoaApi" 是 Core 的 AddMoaApiClient 註冊的具名 HttpClient，全案農業部 API 共用同一份設定
			_httpClient = httpClientFactory.CreateClient("MoaApi");
			_scopeFactory = scopeFactory;
			_timeProvider = timeProvider;
			_configuration = configuration;
		}

		protected override TimeSpan Interval => TimeSpan.FromDays(1); // 每天執行一次
		protected override string LogPrefix => "[PetLoseListSyncWorker]";

		protected override async Task SyncAsync(CancellationToken stoppingToken)
		{
			// BackgroundService 本身是單一長駐個體，但 DbContext 不是執行緒安全、也不該長期存活，
			// 所以每次 SyncAsync 執行都自己開一個 scope 拿一份新的 DbContext，用完隨 using 釋放
			// （比照 AnimalRecognitionSyncWorker／AgriProductsTransSyncWorker 既有慣例）。
			using var scope = _scopeFactory.CreateScope();
			var dbPet = scope.ServiceProvider.GetRequiredService<PetDbContext>();
			var dbCore = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

			// 查這支 Worker 上次同步到哪一天。SyncState 存在 CoreDbContext（跨模組共用同一張表），
			// 用 SyncKey 字串分辨「這筆進度記錄屬於哪一支 Worker」。
			var lastSyncState = await dbCore.SyncStates
				.FirstOrDefaultAsync(s => s.SyncKey == SyncKey, cancellationToken: stoppingToken);

			if (lastSyncState == null)
			{
				// 第一次執行（全新環境，或這支 Worker 從沒有成功跑完一輪）。
				// 比照 AgriProductsTransSyncWorker 的固定起始日模式：直接建立一筆 SyncState、立刻存檔，
				// 接下來跟平常同一套迴圈邏輯走下去——不像 AnimalRecognitionSyncWorker 那樣需要
				// 另外呼叫一次舊制 API 做「回填分支」，因為 PetLoseList 不採用舊制端點（評估後否決）。
				// LastSyncedDate 刻意存「起始日的前一天」，因為下面 startDate = LastSyncedDate.AddDays(1)，
				// 這樣建立後第一輪迴圈的第一天就會正好等於 FixedStartDate。
				lastSyncState = new SyncState
				{
					SyncKey = SyncKey,
					LastSyncedDate = FixedStartDate.AddDays(-1),
					UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime
				};
				dbCore.SyncStates.Add(lastSyncState);
				await dbCore.SaveChangesAsync(stoppingToken);
			}

			// 這一輪要跑的日期區間：[上次同步完成的隔天, 昨天]
			DateOnly startDate = lastSyncState.LastSyncedDate.AddDays(1);
			DateOnly yesterdayDate = TaiwanTime.Today(_timeProvider).AddDays(-1);
			// 只追到「昨天」、不含「今天」：今天的走失啟事可能還在陸續登記中，資料還不完整；
			// 明天這支 Worker 再跑一次時，「今天」已經變成「昨天」，那時候抓到的資料才完整
			// （跟 AnimalRecognitionSyncWorker 的 yesterdayDate 慣例一致）。

			// 一批同時打幾天。實測這支端點單次呼叫固定要 28-35 秒（伺服器端處理時間，
			// 跟資料量無關，換日期測都一樣），逐日序列跑完 2018 至今約 3131 天要 26 小時；
			// 併發實測有效（獨立 session 下 3-5 個同時打，總時間仍約 30 秒 ≒ 單次時間），
			// 證明這 30 秒是每個請求各自獨立的伺服器端處理延遲，不是對單一 IP 的節流，
			// 所以分批平行可以近似等比例縮短總時間（N 併發 ≒ 總時間除以 N）——前提是關掉
			// cookie（見 MoaApiClientExtensions.cs 的 UseCookies=false：同一個 session
			// 的平行請求會被伺服器排隊序列化，關 cookie 才是真平行）。
			// 這個值同時決定「併發數」與「checkpoint 粒度」（見下方迴圈說明），調大跑得快、
			// 但失敗時要重跑的天數也變多。預設 5 是唯一有實測驗證過的併發數（3-5 個
			// 獨立 session 平行都測過，結果乾淨）；開發時可能暫時調高做實驗，
			// 但更高的併發數沒有實測驗證過，屬已知風險而非確認安全，正式使用建議維持 5。
			var batchSize = _configuration.GetValue<int>("PetLoseListSyncWorker:BatchSizeInDays", 5);

			for (var batchStart = startDate; batchStart <= yesterdayDate; batchStart = batchStart.AddDays(batchSize))
			{
				// 組出這一批要處理的日期清單。batchSize 是「這批最多同時打幾天」的上限，
				// 不是「一定要湊滿才打」——d <= yesterdayDate 這個條件保證絕對不會超抓還沒發生
				// 的「未來」天數。回填期間 startDate 跟 yesterdayDate 差了幾千天，所以每批理所當然
				// 湊滿 batchSize；追平之後的日常運作 Interval=1 天，startDate 通常就等於
				// yesterdayDate（只欠昨天），這裡實際只會跑出 1 天，不會憑空多打其他 batchSize-1 天。
				// 這種多天一起打的情境只會在「曾經斷過超過 batchSize 天沒跑」時出現，
				// 那正是設計本來要追平的情境。
				var batchDates = new List<DateOnly>();
				for (var d = batchStart; d <= yesterdayDate && batchDates.Count < batchSize; d = d.AddDays(1))
					batchDates.Add(d);

				var batchEnd = batchDates[^1];
				_logger.LogInformation("{LogPrefix} 開始同步日期區間: {Start} ~ {End}（{Count} 天平行抓取）",
					LogPrefix, batchStart, batchEnd, batchDates.Count);

				// 同一批的日期平行送出。用 LostTime 精確篩選單一天，可以繞過農業部 API
				// 對未登入使用者「查詢只回第一頁 1000 筆」的限制
				// （已實測驗證：不需要 api_key 也能用這個參數拿到近期資料）。
				//
				// 刻意不在這裡包 try/catch：任何一天失敗（網路失敗、API 回應格式跑掉等），
				// Task.WhenAll 會把例外往外拋，中斷整個 SyncAsync。
				// 外層 ScheduledSyncWorkerBase.ExecuteAsync 有全域 try/catch 會接住並記 log，
				// 但「不會」執行到下方推進 LastSyncedDate 那一段，所以下一輪會從 batchStart 整批重跑
				// ——即使這批裡有幾天其實已經抓成功了，也一併重抓。
				// 這是刻意的取捨：寧可重抓幾天（重跑無害，見下方 InsertNewByKeyAsync 說明），
				// 也不要讓 LastSyncedDate 停在一個「有些天做了、有些天沒做」的模糊狀態。
				// 跟 AgriProductsTransSyncWorker「只有全部市場成功才推進 LastSyncedDate」是同一個原子性原則，
				// 只是把「一批市場」換成「一批日期」。
				var responses = await Task.WhenAll(batchDates.Select(async date =>
				{
					var url = $"{MoaApiEndpoints.PetLoseList}?LostTime={date:yyyy/MM/dd}";
					return await _httpClient.GetFromJsonAsync<PetLoseListApiResponse>(url, stoppingToken);
				}));

				// 把整批各天的資料攤平成一份清單再落地（不逐天各寫一次 DB，減少來回次數）。
				// RS 不是 OK 或 Data 是空的那幾天會被 Where 濾掉——這是正常情況不是錯誤
				// （那天剛好沒有任何走失啟事），一樣要往下走到推進 LastSyncedDate。
				//
				// DistinctBy 用 KeyNo（官方序號，全域唯一）去重：理論上不同日期的資料不會撞號，
				// 但比照專案既有慣例（曾踩過的坑：InsertNewByKeyAsync 只過濾「DB 已存在的鍵」，
				// 不處理本批次內部的重複），落地前一律自己先去重，不依賴外部資料一定乾淨。
				var incoming = responses
					.Where(r => r?.RS == "OK" && r.Data != null && r.Data.Count > 0)
					.SelectMany(r => r!.Data)
					.Select(dto => MapToEntity(dto, _timeProvider, _logger))
					.DistinctBy(x => x.KeyNo)
					.ToList();

				if (incoming.Count == 0)
				{
					_logger.LogInformation("{LogPrefix} {Start} ~ {End} 無新資料", LogPrefix, batchStart, batchEnd);
				}
				else
				{
					// 落地策略：只新增、不更新。因為這是「已發生的遺失事件」的一次性登記快照，
					// 內容登記後不會再變動（不像 LegalSpecificPet 的評鑑等級/營業狀態會隨時間改變，
					// 那支才需要另外設計 upsert）。
					// DbSyncHelper 內部會把 existingKeys 轉成 HashSet 再比對 incoming，
					// 過濾掉 DB 裡已經存在的 KeyNo，只留下真正需要新增的資料再寫入
					// ——這也是上面「整批重跑無害」的依據：重抓到的舊資料會在這裡被安靜濾掉。
					//
					// 【已定案，code review 不需再提】這裡刻意查全表（不像 AgriProductsTransSyncWorker
					// 用 .Where(x => x.TransDate == currentDate) 縮小成當天視窗）。原因：
					// 縮小成 LostTime 落在本批日期範圍內的窗口，前提是「用 ?LostTime=X 查到的資料，
					// 自己的 LostTime 欄位一定等於 X」——這個假設沒有驗證過，一旦不成立會導致同一筆
					// 資料被誤判成新資料、撞 KeyNo 唯一索引，且整批重跑會反覆卡在同一個錯，不會自己好。
					// 全表掃描沒有這個風險（不管 API 語意如何都一定抓得到現存的鍵），而 KeyNo 有
					// Unique Index，就算回填完約 15 萬筆，查詢＋建 HashSet 也是毫秒等級，跟單次
					// API 呼叫要 30 秒比起來完全不是瓶頸。這是「用零風險換一個不存在的效能問題」的
					// 交易，不划算，2026-07-30 討論後決定維持全表掃描。除非之後出現全表掃描
					// 真的量測到效能問題、或 LostTime 語意被正式驗證過，否則不要重新拿出來討論。
					var existingKeys = dbPet.OfficialLostPetPosts.Select(x => x.KeyNo);
					await DbSyncHelper.InsertNewByKeyAsync(
						dbPet,
						existingKeys,
						incoming,
						x => x.KeyNo,
						_logger,
						LogPrefix,
						stoppingToken);
				}

				// 走到這裡代表這一批「每一天都完全處理成功」（打 API、映射、寫入全部沒拋例外），
				// 才可以把 LastSyncedDate 推進到這批的最後一天並存檔。
				// checkpoint 的粒度從「一天」變成「一批」，但保證的性質不變：
				// 只會有「這批徹底做完」或「完全沒做完、例外往外炸」兩種結果，沒有模糊的中間狀態。
				lastSyncState.LastSyncedDate = batchEnd;
				lastSyncState.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
				await dbCore.SaveChangesAsync(stoppingToken);

				_logger.LogInformation("{LogPrefix} {Start} ~ {End} 同步完成", LogPrefix, batchStart, batchEnd);
			}
		}

		/// <summary>
		/// 把 API 回應的原始 DTO 轉成要存進 DB 的 Entity。
		/// internal static：不依賴任何實例欄位，方便單元測試直接呼叫驗證映射邏輯，
		/// 比照 AnimalRecognitionSyncWorker.MapToEntity 的既有寫法。
		/// </summary>
		internal static OfficialLostPetPost MapToEntity(PetLoseListDto dto, TimeProvider timeProvider, ILogger logger)
		{
			return new OfficialLostPetPost
			{
				KeyNo = dto.KeyNo,
				ChipNum = dto.ChipNum,
				PetName = dto.PetName,

				// PetCategory 原始值是中文字面值「狗」／「貓」，用 switch 轉成 enum；
				// 樣本中約 15 筆是空字串，會落入 default 分支，fallback 成 Other（不會讓整批同步失敗）。
				// EnumMappingHelper.LogUnexpectedValue 除了回傳 fallback，還會記一筆 warning log，
				// 方便日後回頭檢查「這個 fallback 到底是正常的空值、還是 API 出現了新的未知值」。
				Category = dto.PetCategory switch
				{
					"狗" => AnimalKind.Dog,
					"貓" => AnimalKind.Cat,
					_ => EnumMappingHelper.LogUnexpectedValue(dto.KeyNo, nameof(dto.PetCategory), dto.PetCategory, AnimalKind.Other, logger)
				},

				// Gender 原始值只有「公」／「母」兩種（樣本顯示 0 筆空值），
				// 不像 ShelterAnimal.AnimalSex 需要煩惱第三態 "N"，但 switch 仍保留 fallback 分支保險。
				Sex = dto.Gender switch
				{
					"公" => AnimalSex.Male,
					"母" => AnimalSex.Female,
					_ => EnumMappingHelper.LogUnexpectedValue(dto.KeyNo, nameof(dto.Gender), dto.Gender, AnimalSex.Other, logger)
				},

				Variety = dto.Variety,
				Coat = dto.Coat,
				Exterior = dto.Exterior,
				Feature = dto.Feature,

				// LostTime 原始格式是 "2024/01/01" 這種帶斜線的字串（DTO 保留原始字串不轉型，
				// 型別轉換統一延後到這裡做），用 ParseExact 明確指定格式解析，避免文化設定不同造成誤判。
				LostTime = DateOnly.ParseExact(dto.LostTime, "yyyy/MM/dd"),

				LostPlace = dto.LostPlace,
				FeederName = dto.FeederName,
				PhoneNum = dto.PhoneNum,
				EMail = dto.EMail,

				// Picture 是走失啟事附的照片網址（例如 pet.gov.tw 的 XMLRequest/PET_PIC.ashx?File_No=...），
				// 原樣存起來即可，前端地圖 marker 之後可以直接拿這個網址顯示照片。
				PictureUrl = dto.Picture,

				// 時鐘一律走 TimeProvider 注入（不直接呼叫 DateTime.UtcNow），測試時可固定時間點驗證行為
				SyncedAt = timeProvider.GetUtcNow().UtcDateTime
			};
		}
	}
}
