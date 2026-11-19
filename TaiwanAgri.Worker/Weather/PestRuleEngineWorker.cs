using TaiwanAgri.Modules.Weather.Services;
using TaiwanAgri.Worker;

namespace TaiwanAgri.Worker.Weather
{
	public class PestRuleEngineWorker : ScheduledSyncWorkerBase
	{
		/// <summary>規則引擎要等的同步：數值型規則比的是氣象觀測、事件型比的是疫情警報</summary>
		internal static readonly Type[] DependencyTypes = [typeof(WeatherSyncWorker), typeof(PestAlertSyncWorker)];

		/// <summary>
		/// 最多等多久。等的是「試過一輪」，那兩支同步正常幾分鐘內就跑完；
		/// 上限是給「同步卡住」的情況（共用的 MoaApi client 是無限逾時），時間到了就照樣評估
		/// </summary>
		internal static readonly TimeSpan MaxDependencyWait = TimeSpan.FromMinutes(10);

		private readonly ILogger<PestRuleEngineWorker> _logger;
		private readonly PestRuleEngine _pestRuleEngine;
		private readonly IServiceProvider _services;

		public PestRuleEngineWorker(ILogger<PestRuleEngineWorker> logger, PestRuleEngine pestRuleEngine, IServiceProvider services)
			: base(logger)
		{
			_logger = logger;
			_pestRuleEngine = pestRuleEngine;
			_services = services;
		}

		protected override TimeSpan Interval => TimeSpan.FromDays(1); // 每天執行一次
		protected override string LogPrefix => "[PestRuleEngineWorker]";

		/// <summary>
		/// 第一輪評估之前，先等氣象觀測與疫情警報兩支同步跑完第一輪（2026-11 總檢查 3-B2）。
		/// 17 支 Worker 同時啟動、各自隨機延遲 0–30 秒，規則引擎若比它們先跑，當天剛同步進來的資料
		/// 要等下一輪才會被評估——線上是一次性模式、每天只跑一輪，通知就晚一天。
		/// 等的是「試過一輪」（失敗也算），相依的那支壞掉不會讓引擎永遠等下去
		/// </summary>
		protected override Task WaitUntilReadyAsync(CancellationToken stoppingToken)
		{
			// 在這裡才解析，不在建構式：自己也是 IHostedService，建構式裡要 IEnumerable<IHostedService> 會循環相依
			// （RunOnceCoordinator 同一個做法）
			var firstRounds = _services.GetServices<IHostedService>()
				.OfType<ScheduledSyncWorkerBase>()
				.Where(w => DependencyTypes.Contains(w.GetType()))
				.Select(w => w.FirstRoundAttempted)
				.ToList();
			return WaitForFirstRoundsAsync(firstRounds, MaxDependencyWait, _logger, stoppingToken);
		}

		/// <summary>等這幾支的第一輪都試過；超過上限就記一則警告、照樣往下走（不拋例外）</summary>
		internal static async Task WaitForFirstRoundsAsync(
			IReadOnlyCollection<Task> firstRounds, TimeSpan maxWait, ILogger logger, CancellationToken stoppingToken)
		{
			try
			{
				await Task.WhenAll(firstRounds).WaitAsync(maxWait, stoppingToken);
			}
			catch (TimeoutException)
			{
				logger.LogWarning(
					"[PestRuleEngineWorker] 氣象觀測或疫情警報的同步 {Minutes} 分鐘內沒有跑完第一輪，先評估現有資料",
					maxWait.TotalMinutes);
			}
		}

		// userId 傳 null＝評估全系統所有人的規則，這是排程的職責。
		// 使用者手動觸發的那條路徑會帶自己的 userId，把範圍與例外的影響收在呼叫者身上
		protected override Task SyncAsync(CancellationToken stoppingToken)
			=> _pestRuleEngine.EvaluateAsync(null, stoppingToken);
	}
}
