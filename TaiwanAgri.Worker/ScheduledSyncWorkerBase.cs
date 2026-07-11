namespace TaiwanAgri.Worker
{
	/// <summary>
	/// 定時同步 Worker 的共用排程外殼：「同步 → 失敗記 log 不中斷 → 等待下一輪」。
	/// 子類別實作 SyncAsync（單輪工作）、Interval（輪距）與 LogPrefix（日誌前綴）。
	/// 首輪同步前隨機延遲 0–30 秒（jitter）：13 支 Worker 都在程序啟動瞬間
	/// 註冊為 HostedService，不錯開的話首輪會同時打農業部 API 與 DB（啟動風暴）。
	/// </summary>
	public abstract class ScheduledSyncWorkerBase : BackgroundService
	{
		private readonly ILogger _logger;

		protected ScheduledSyncWorkerBase(ILogger logger)
		{
			_logger = logger;
		}

		/// <summary>兩輪同步之間的等待時間</summary>
		protected abstract TimeSpan Interval { get; }

		/// <summary>日誌前綴（如 "[PesticideViolationSync]"），與各 Worker 內部日誌一致</summary>
		protected abstract string LogPrefix { get; }

		/// <summary>單輪同步工作；拋出的例外由基底類記 log 後繼續下一輪，不會終止 Worker</summary>
		protected abstract Task SyncAsync(CancellationToken stoppingToken);

		/// <summary>首輪同步前的就緒等待（如相依資料尚未落地時輪詢），預設不等待</summary>
		protected virtual Task WaitUntilReadyAsync(CancellationToken stoppingToken) => Task.CompletedTask;

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			await Task.Delay(TimeSpan.FromSeconds(Random.Shared.Next(0, 30)), stoppingToken);

			await WaitUntilReadyAsync(stoppingToken);

			while (!stoppingToken.IsCancellationRequested)
			{
				try
				{
					await SyncAsync(stoppingToken);
				}
				catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
				{
					// 停機取消不是同步失敗，不記 error
					break;
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "{LogPrefix} 同步失敗", LogPrefix);
				}
				await Task.Delay(Interval, stoppingToken);
			}
		}
	}
}
