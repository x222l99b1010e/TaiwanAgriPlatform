using Microsoft.EntityFrameworkCore;
using TaiwanAgri.Core.Constants;
using TaiwanAgri.Core.Helpers;
using TaiwanAgri.Core.Infrastructure.Data;
using TaiwanAgri.Core.Infrastructure.Entities;
using TaiwanAgri.Modules.Market.Constants;
using TaiwanAgri.Modules.Market.Data;
using TaiwanAgri.Modules.Market.Dtos.WorkerResponses;
using TaiwanAgri.Modules.Market.Entities;
using TaiwanAgri.Modules.Market.Entities.Enums;
using TaiwanAgri.Modules.Market.Helpers;
using TaiwanAgri.Worker;

namespace TaiwanAgri.Worker.Market
{
	/// <summary>
	/// 同步農業部四支家禽交易行情 API，落地為 market.PoultryTrans 長表。
	///
	/// 與其他同步 Worker 的結構性差異：這是全案第一支「一個 Worker 服務四條獨立資料流」的
	/// 實作。四支來源的欄位集、歷史起點、單頁上限都不同，但「年度切塊 → 抓取 → 攤平 →
	/// 落地 → 推進游標」的流程完全一致，因此把流程抽成 SyncSourceAsync 共用一份，
	/// 四支各自只保留「怎麼抓、怎麼把寬表 DTO 攤平成長表列」這段本質不同的邏輯。
	/// 若四支各自複製一份流程，年度邊界、游標推進時機這類容易出差一錯誤的程式碼會有四份，
	/// 修其中一處而漏改其餘三處是可預期的錯誤模式。
	/// </summary>
	public class PoultryTransSyncWorker : ScheduledSyncWorkerBase
	{
		// 四支來源各配一組 SyncState。共用單一游標會漏資料：白肉雞／鵝鴨的歷史從 2010 起，
		// 紅羽／黑羽從 2014 起，共用游標若起於 2010，紅羽黑羽會空打四年；若起於 2014，
		// 白肉雞與鵝鴨 2010-2013 的資料則永遠不會被回填（實測起點見下方 Seed 常數）。
		private const string SyncKeyPrefix = "Market_Poultry_";

		// 各支來源實測的真實最早交易日（2026-08-24 以逐年視窗往前掃到空視窗確認）。
		// 這些日期同時是 SyncState 不存在時的種子值——存進去的是「起始日的前一天」，
		// 因為下方 startDate = LastSyncedDate.AddDays(1)（比照 PetLoseListSyncWorker 慣例）。
		private static readonly DateOnly BoiledChickenEggsSeed = new(2010, 10, 7);
		private static readonly DateOnly RedFeatherSeed = new(2014, 4, 1);
		private static readonly DateOnly BlackFeatherSeed = new(2014, 4, 1);
		private static readonly DateOnly GooseDuckDuckeggSeed = new(2010, 10, 7);

		private readonly ILogger<PoultryTransSyncWorker> _logger;
		private readonly HttpClient _httpClient;
		private readonly IServiceScopeFactory _scopeFactory;
		private readonly TimeProvider _timeProvider;

		public PoultryTransSyncWorker(
			ILogger<PoultryTransSyncWorker> logger,
			IHttpClientFactory httpClientFactory,
			IServiceScopeFactory scopeFactory,
			TimeProvider timeProvider)
			: base(logger)
		{
			_logger = logger;
			_httpClient = httpClientFactory.CreateClient("MoaApi");
			_scopeFactory = scopeFactory;
			_timeProvider = timeProvider;
		}

		protected override TimeSpan Interval => TimeSpan.FromHours(12); // 比照 PorkTransSyncWorker
		protected override string LogPrefix => "[PoultryTransSyncWorker]";

		protected override async Task SyncAsync(CancellationToken stoppingToken)
		{
			using var scope = _scopeFactory.CreateScope();
			var dbMarket = scope.ServiceProvider.GetRequiredService<MarketDbContext>();
			var dbCore = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

			// 四支依序執行（不平行）：農業部 API 的禮貌性節流，且四支合計每輪最多數十次請求，
			// 回填完成後日常每輪只有 4 次，沒有平行化的必要
			await SyncSourceAsync(dbMarket, dbCore, "BoiledChicken_Eggs", BoiledChickenEggsSeed,
				PoultryMetrics.BoiledChickenEggsMetrics, FetchBoiledChickenEggsAsync, stoppingToken);

			await SyncSourceAsync(dbMarket, dbCore, "RedFeather", RedFeatherSeed,
				PoultryMetrics.RedFeatherMetrics, FetchRedFeatherAsync, stoppingToken);

			await SyncSourceAsync(dbMarket, dbCore, "BlackFeather", BlackFeatherSeed,
				PoultryMetrics.BlackFeatherMetrics, FetchBlackFeatherAsync, stoppingToken);

			await SyncSourceAsync(dbMarket, dbCore, "Goose_Duck_Duckegg", GooseDuckDuckeggSeed,
				PoultryMetrics.GooseDuckDuckeggMetrics, FetchGooseDuckDuckeggAsync, stoppingToken);
		}

		/// <summary>
		/// 單一資料來源的完整同步流程，四支共用。
		/// 切塊粒度是「一個西元年」：來源 API 吃 Start_time/End_time 區間參數（PorkTrans 只吃
		/// 單一日期，才被迫逐日呼叫），而一年最多 366 天遠低於四支中最小的單頁上限 500 筆，
		/// 全歷史 20614 天實測沒有任何一年觸發 Next=true。年度邊界同時是 checkpoint：
		/// 一年成功寫入才推進 SyncState，中途失敗則例外往外拋、由基底類記錄，下一輪從該年重跑
		/// （重跑無害，InsertNewByKeyAsync 會濾掉已存在的鍵）。
		/// 回填追平後，startDate 會逼近昨天，年度區間自然收斂成一兩天，
		/// 不需要為「回填」與「日常增量」寫兩套分支。
		/// </summary>
		/// <param name="sourceName">來源識別，用於組 SyncKey 與日誌</param>
		/// <param name="seedDate">SyncState 不存在時的回填起始日（該來源實測最早交易日）</param>
		/// <param name="metricCodes">該來源產出的 MetricCode 全集，用於限縮既有鍵查詢範圍</param>
		/// <param name="fetchAndFlatten">抓取指定區間並攤平成長表列，四支各自實作</param>
		private async Task SyncSourceAsync(
			MarketDbContext dbMarket,
			CoreDbContext dbCore,
			string sourceName,
			DateOnly seedDate,
			string[] metricCodes,
			Func<DateOnly, DateOnly, CancellationToken, Task<List<PoultryTrans>>> fetchAndFlatten,
			CancellationToken stoppingToken)
		{
			var syncKey = SyncKeyPrefix + sourceName;

			var syncState = await dbCore.SyncStates
				.FirstOrDefaultAsync(s => s.SyncKey == syncKey, cancellationToken: stoppingToken);

			if (syncState == null)
			{
				syncState = new SyncState
				{
					SyncKey = syncKey,
					LastSyncedDate = seedDate.AddDays(-1),
					UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime
				};
				dbCore.SyncStates.Add(syncState);
				await dbCore.SaveChangesAsync(stoppingToken);
			}

			var startDate = syncState.LastSyncedDate.AddDays(1);
			// 只追到「昨天」：當天行情可能尚未公告完整，明天再跑時它已經是昨天
			var yesterday = TaiwanTime.Today(_timeProvider).AddDays(-1);

			if (startDate > yesterday)
			{
				_logger.LogInformation("{LogPrefix} {Source} 已同步至 {Last}，無新資料",
					LogPrefix, sourceName, syncState.LastSyncedDate);
				return;
			}

			for (var chunkStart = startDate; chunkStart <= yesterday;)
			{
				// 這一塊的結束日＝該年 12/31 與昨天取較早者。兩個邊界都用「含」的語意：
				// 下一塊起點是 chunkEnd.AddDays(1)，確保既不重疊也不跳過任何一天
				var yearEnd = new DateOnly(chunkStart.Year, 12, 31);
				var chunkEnd = yearEnd < yesterday ? yearEnd : yesterday;

				_logger.LogInformation("{LogPrefix} {Source} 抓取區間 {Start} ~ {End}",
					LogPrefix, sourceName, chunkStart, chunkEnd);

				var rows = await fetchAndFlatten(chunkStart, chunkEnd, stoppingToken);

				// 批次內去重：InsertNewByKeyAsync 只過濾「DB 已存在的鍵」，不處理本批內部重複，
				// 來源若同一天回兩筆會直接撞 Unique Index（比照專案既有慣例先自行去重）
				var incoming = rows
					.DistinctBy(r => new { r.TransDate, r.MetricCode })
					.ToList();

				// 未知寫法要主動示警：這是 PriceStatus.Unrecognized 存在的目的，
				// 沉默地存進 DB 等於沒有這個狀態
				var unrecognized = incoming.Where(r => r.PriceStatus == PriceStatus.Unrecognized).ToList();
				if (unrecognized.Count > 0)
				{
					_logger.LogWarning("{LogPrefix} {Source} {Start}~{End} 出現 {Count} 筆無法分類的價格字串：{Samples}",
						LogPrefix, sourceName, chunkStart, chunkEnd, unrecognized.Count,
						string.Join(", ", unrecognized.Take(5).Select(r => $"{r.TransDate:yyyy/MM/dd} {r.MetricCode}=\"{r.RawValue}\"")));
				}

				if (incoming.Count > 0)
				{
					// 既有鍵查詢限縮在「這一塊的日期範圍 × 這支來源的指標」：
					// 長表全量約 8.8 萬列，四支各自掃自己的範圍即可，不需要整表掃描
					var existingKeys = dbMarket.PoultryTrans
						.Where(p => p.TransDate >= chunkStart
								 && p.TransDate <= chunkEnd
								 && metricCodes.Contains(p.MetricCode))
						.Select(p => new { p.TransDate, p.MetricCode });

					await DbSyncHelper.InsertNewByKeyAsync(
						dbMarket,
						existingKeys,
						incoming,
						r => new { r.TransDate, r.MetricCode },
						_logger,
						$"{LogPrefix} {sourceName}",
						stoppingToken);
				}
				else
				{
					_logger.LogInformation("{LogPrefix} {Source} {Start}~{End} 無資料",
						LogPrefix, sourceName, chunkStart, chunkEnd);
				}

				// 走到這裡代表這一整塊都成功了，才推進游標
				syncState.LastSyncedDate = chunkEnd;
				syncState.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
				await dbCore.SaveChangesAsync(stoppingToken);

				chunkStart = chunkEnd.AddDays(1);
			}
		}

		// --- 以下四支：各自的抓取 + 攤平。共通結構相同但欄位對照本質不同，刻意不再抽象 ---

		private async Task<List<PoultryTrans>> FetchBoiledChickenEggsAsync(
			DateOnly start, DateOnly end, CancellationToken stoppingToken)
		{
			var dtos = await FetchAsync<PoultryBoiledChickenEggsDto>(
				MoaApiEndpoints.PoultryBoiledChickenEggs, start, end, stoppingToken);

			var rows = new List<PoultryTrans>();
			foreach (var dto in dtos)
			{
				if (!TryParseTransDate(dto.TransDate, out var date)) continue;

				rows.Add(Row(date, PoultryMetrics.BoiledChicken_2_0KgUp, dto.BoiledChicken2_0KgUp));
				rows.Add(Row(date, PoultryMetrics.BoiledChicken_1_75To1_95Kg, dto.BoiledChicken1_75To1_95Kg));
				rows.Add(Row(date, PoultryMetrics.BoiledChicken_Store_KaoPing, dto.StoreKaoPing));
				rows.Add(Row(date, PoultryMetrics.Egg_Transport, dto.EggTransport));
				rows.Add(Row(date, PoultryMetrics.Egg_Producer, dto.EggProducer));
			}
			return rows;
		}

		private async Task<List<PoultryTrans>> FetchRedFeatherAsync(
			DateOnly start, DateOnly end, CancellationToken stoppingToken)
		{
			var dtos = await FetchAsync<PoultryRedFeatherDto>(
				MoaApiEndpoints.PoultryRedFeather, start, end, stoppingToken);

			var rows = new List<PoultryTrans>();
			foreach (var dto in dtos)
			{
				if (!TryParseTransDate(dto.TransDate, out var date)) continue;

				rows.Add(Row(date, PoultryMetrics.RedFeather_North_Male, dto.NorthMale));
				rows.Add(Row(date, PoultryMetrics.RedFeather_North_Female, dto.NorthFemale));
				rows.Add(Row(date, PoultryMetrics.RedFeather_Central_Male, dto.CentralMale));
				rows.Add(Row(date, PoultryMetrics.RedFeather_Central_Female, dto.CentralFemale));
				rows.Add(Row(date, PoultryMetrics.RedFeather_South_Male, dto.SouthMale));
				rows.Add(Row(date, PoultryMetrics.RedFeather_South_Female, dto.SouthFemale));
			}
			return rows;
		}

		private async Task<List<PoultryTrans>> FetchBlackFeatherAsync(
			DateOnly start, DateOnly end, CancellationToken stoppingToken)
		{
			var dtos = await FetchAsync<PoultryBlackFeatherDto>(
				MoaApiEndpoints.PoultryBlackFeather, start, end, stoppingToken);

			var rows = new List<PoultryTrans>();
			foreach (var dto in dtos)
			{
				if (!TryParseTransDate(dto.TransDate, out var date)) continue;

				rows.Add(Row(date, PoultryMetrics.BlackFeather_South_Male, dto.SouthMale));
				rows.Add(Row(date, PoultryMetrics.BlackFeather_South_Female, dto.SouthFemale));
			}
			return rows;
		}

		private async Task<List<PoultryTrans>> FetchGooseDuckDuckeggAsync(
			DateOnly start, DateOnly end, CancellationToken stoppingToken)
		{
			var dtos = await FetchAsync<PoultryGooseDuckDuckeggDto>(
				MoaApiEndpoints.PoultryGooseDuckDuckegg, start, end, stoppingToken);

			var rows = new List<PoultryTrans>();
			foreach (var dto in dtos)
			{
				if (!TryParseTransDate(dto.TransDate, out var date)) continue;

				rows.Add(Row(date, PoultryMetrics.Goose_WhiteRoman, dto.GooseWhiteRoman));
				rows.Add(Row(date, PoultryMetrics.Duck_Male, dto.DuckMale));
				rows.Add(Row(date, PoultryMetrics.Duck_75Days, dto.Duck75Days));
				rows.Add(Row(date, PoultryMetrics.Duckegg_Tainan, dto.DuckeggTainan));
			}
			return rows;
		}

		// --- 四支共用的小工具 ---

		/// <summary>
		/// 帶日期區間打單一端點。分頁交給 MoaPagedFetcher 處理。
		///
		/// ⚠ 已知限制：MoaPagedFetcher 組換頁網址時固定用 "?page=N" 串接（見該類別實作），
		/// 對已帶查詢字串的 endpoint 會組出兩個問號的非法網址。這裡安全的前提是
		/// 「年度切塊後單次結果 ≤ 366 筆 < 最小單頁上限 500」，Next 恆為 false、
		/// 第二頁的程式路徑不會被執行到。若日後把切塊粒度改大（例如改成一次抓多年），
		/// 這個前提就不成立，必須連同 MoaPagedFetcher 的網址組法一起重新檢視。
		/// </summary>
		private async Task<List<TDto>> FetchAsync<TDto>(
			string basePath, DateOnly start, DateOnly end, CancellationToken stoppingToken)
		{
			// 日期格式是西元 yyyy/MM/dd，不是民國——與 PorkTrans 不同，不可用 DateHelper 轉 ROC
			var endpoint = $"{basePath}?Start_time={start:yyyy/MM/dd}&End_time={end:yyyy/MM/dd}";

			return await MoaPagedFetcher.FetchAllPagesAsync<PoultryTransApiResponse<TDto>, TDto>(
				_httpClient, endpoint, _logger, LogPrefix, stoppingToken);
		}

		/// <summary>回傳的 TransDate 是西元 yyyy/MM/dd；解析失敗只跳過該筆並記錄，不中斷整批</summary>
		private bool TryParseTransDate(string raw, out DateOnly date)
		{
			if (DateOnly.TryParseExact(raw, "yyyy/MM/dd", out date))
				return true;

			_logger.LogWarning("{LogPrefix} 無法解析 TransDate「{Raw}」，略過該筆", LogPrefix, raw);
			return false;
		}

		/// <summary>把單一價格儲存格組成一列長表資料，價格分類邏輯統一走 PoultryPriceParser</summary>
		private PoultryTrans Row(DateOnly transDate, string metricCode, string? rawPrice)
		{
			var (price, status, rawValue) = PoultryPriceParser.Parse(rawPrice);

			return new PoultryTrans
			{
				TransDate = transDate,
				MetricCode = metricCode,
				Price = price,
				PriceStatus = status,
				RawValue = rawValue,
				SyncedAt = _timeProvider.GetUtcNow().UtcDateTime
			};
		}
	}
}
