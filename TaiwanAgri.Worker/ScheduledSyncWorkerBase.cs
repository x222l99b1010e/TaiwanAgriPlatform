namespace TaiwanAgri.Worker
{
	/// <summary>
	/// 定時同步 Worker 的共用排程外殼：「同步 → 失敗記 log 不中斷 → 等待下一輪」。
	/// 子類別實作 SyncAsync（單輪工作）、Interval（輪距）與 LogPrefix（日誌前綴）。
	/// 首輪同步前隨機延遲 0–30 秒（jitter）：17 支 Worker 都在程序啟動瞬間
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

		/// <summary>
		/// 首輪同步前的就緒等待（如相依資料尚未落地時輪詢），預設不等待。
		/// 拋出的例外由基底類記 log 後直接進入同步迴圈，不會終止 Worker
		/// </summary>
		protected virtual Task WaitUntilReadyAsync(CancellationToken stoppingToken) => Task.CompletedTask;

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			await Task.Delay(TimeSpan.FromSeconds(Random.Shared.Next(0, 30)), stoppingToken);

			try
			{
				await WaitUntilReadyAsync(stoppingToken);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				return;
			}
			catch (Exception ex)
			{
				// 就緒等待本身會碰外部資源（輪詢相依資料時要建 scope 查 DB），
				// 而它執行的時機正是程序剛啟動、DB 最可能還沒就緒的時候。
				// 不接住的話這裡拋出的例外會直接離開 ExecuteAsync，該 Worker 永久停止且不重試，
				// 症狀是「這一支完全不動、其他都正常」，難以與其他成因區分。
				// 接住後直接進入同步迴圈：相依資料若真的還沒好，SyncAsync 會失敗、記 log，
				// 由迴圈既有的重試機制在下一輪重來。
				_logger.LogError(ex, "{LogPrefix} 就緒等待失敗，直接進入同步迴圈", LogPrefix);
			}

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
