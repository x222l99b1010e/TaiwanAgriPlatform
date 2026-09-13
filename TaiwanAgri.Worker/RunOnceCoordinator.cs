using Microsoft.Extensions.DependencyInjection;

namespace TaiwanAgri.Worker
{
	/// <summary>
	/// 一次性執行模式的收工判斷：等 17 支排程 Worker 各跑完第一輪，然後讓整個程序結束。
	/// <para>
	/// 為什麼需要它：Worker 平常是常駐服務——同步、睡到下一輪、再同步，設計上不會結束。
	/// 但部署形態撐不起常駐：Azure SQL 免費方案每月 100,000 vCore 秒
	/// （最省狀態約 55.6 小時），一直開著的話 24×30 天需要約 1,296,000 vCore 秒，
	/// 超出額度 13 倍。而 App Service F1 沒有 Always On，常駐 WebJob 本來就會被回收。
	/// 所以排程改由外部觸發（GitHub Actions 的 cron），程序跑完一輪就要自己結束。
	/// </para>
	/// <para>
	/// 「跑完了」的訊號來自每一支 Worker 的 FirstRoundAttempted，不是等固定時間——
	/// 「等 15 分鐘應該夠了吧」這種寫法在同步變慢的那天會安靜地少同步幾支，而且不會有任何訊號。
	/// </para>
	/// </summary>
	public sealed class RunOnceCoordinator : BackgroundService
	{
		private readonly IServiceProvider _services;
		private readonly IHostApplicationLifetime _lifetime;
		private readonly ILogger<RunOnceCoordinator> _logger;
		private readonly TimeSpan _timeout;

		public RunOnceCoordinator(
			IServiceProvider services,
			IHostApplicationLifetime lifetime,
			ILogger<RunOnceCoordinator> logger,
			TimeSpan timeout)
		{
			_services = services;
			_lifetime = lifetime;
			_logger = logger;
			_timeout = timeout;
		}

		/// <summary>
		/// 這一趟的結束碼。獨立成屬性而不是只寫 Environment.ExitCode：
		/// 後者是行程層級的全域狀態，測試同時跑好幾個時會互相汙染。
		/// 真正寫進 Environment 的動作只發生一次，在判斷全部做完之後
		/// </summary>
		public int ResultExitCode { get; private set; }

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			// 在 ExecuteAsync 裡才解析，不在建構式：這個服務自己也是 IHostedService，
			// 建構式裡要 IEnumerable<IHostedService> 會是循環相依。
			// 這時候全部都已經建好且是單例，拿到的就是正在跑的那幾個實例
			var workers = _services.GetServices<IHostedService>()
				.OfType<ScheduledSyncWorkerBase>()
				.ToList();

			if (workers.Count == 0)
			{
				_logger.LogWarning("[RunOnce] 找不到任何排程 Worker，直接結束");
				_lifetime.StopApplication();
				return;
			}

			_logger.LogInformation(
				"[RunOnce] 一次性執行模式：等 {Count} 支 Worker 各跑完一輪，上限 {Minutes} 分鐘",
				workers.Count, _timeout.TotalMinutes);

			var allAttempted = Task.WhenAll(workers.Select(w => w.FirstRoundAttempted));
			var timedOut = false;

			try
			{
				timedOut = await Task.WhenAny(allAttempted, Task.Delay(_timeout, stoppingToken)) != allAttempted;
			}
			catch (OperationCanceledException)
			{
				// 外部先停掉了（Ctrl+C、平台送 SIGTERM），不是我們判斷的結果，直接讓它走
				return;
			}

			if (timedOut)
			{
				var pending = workers
					.Where(w => !w.FirstRoundAttempted.IsCompleted)
					.Select(w => w.GetType().Name);

				_logger.LogError(
					"[RunOnce] 等了 {Minutes} 分鐘仍未跑完，未完成的有：{Pending}",
					_timeout.TotalMinutes, string.Join("、", pending));

				// 逾時要讓整個工作是紅的。回 0 的話排程看起來每天都成功，
				// 而實際上有幾支從來沒同步過——那是綠燈說謊
				ResultExitCode = 1;
			}
			else
			{
				var failed = workers.Where(w => w.FirstRoundFailed).Select(w => w.GetType().Name).ToList();

				if (failed.Count == workers.Count)
				{
					_logger.LogError("[RunOnce] {Count} 支全部同步失敗，這一輪沒有同步到任何資料", failed.Count);
					ResultExitCode = 1;
				}
				else if (failed.Count > 0)
				{
					// 部分失敗不讓整個工作變紅：外部資料源偶爾不通是常態，
					// 而每一支的重跑兜底本來就是隔天再來。要看是哪幾支就看這一行
					_logger.LogWarning(
						"[RunOnce] {Total} 支跑完，其中 {Failed} 支同步失敗：{Names}",
						workers.Count, failed.Count, string.Join("、", failed));
				}
				else
				{
					_logger.LogInformation("[RunOnce] {Count} 支全部跑完一輪且都成功", workers.Count);
				}
			}

			Environment.ExitCode = ResultExitCode;
			_lifetime.StopApplication();
		}
	}
}
