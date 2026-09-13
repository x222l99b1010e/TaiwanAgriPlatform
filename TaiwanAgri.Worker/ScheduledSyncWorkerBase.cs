namespace TaiwanAgri.Worker
{
	/// <summary>
	/// 定時同步 Worker 的共用排程外殼：「同步 → 失敗記 log 不中斷 → 等待下一輪」。
	/// 子類別實作 SyncAsync（單輪工作）、Interval（輪距）與 LogPrefix（日誌前綴）。
	/// 首輪同步前有隨機延遲（見 StartupJitter）：17 支 Worker 都在程序啟動瞬間
	/// 註冊為 HostedService，不錯開的話首輪會同時打農業部 API 與 DB（啟動風暴）。
	/// </summary>
	public abstract class ScheduledSyncWorkerBase : BackgroundService
	{
		private readonly ILogger _logger;

		/// <summary>
		/// 「第一輪同步已經跑過一次」的訊號，給一次性執行模式（RunOnceCoordinator）用。
		/// <para>
		/// 用 TaskCompletionSource 而不是把「跑幾輪」做成建構式參數：後者要改 17 支子類的
		/// 建構式，而這個訊號只有協調器需要，跟每一支 Worker 自己的職責無關。
		/// </para>
		/// <para>
		/// ⚠ 語意是「試過一次」而不是「成功一次」——同步失敗、就緒等待失敗、
		/// 甚至還沒開始就被取消，都算數。做成「成功才算」的話，一支連不上外部 API 的
		/// Worker 會讓整個一次性工作永遠等下去
		/// </para>
		/// </summary>
		private readonly TaskCompletionSource _firstRoundAttempted =
			new(TaskCreationOptions.RunContinuationsAsynchronously);

		/// <summary>第一輪同步已經試過一次（成功或失敗都算）</summary>
		public Task FirstRoundAttempted => _firstRoundAttempted.Task;

		/// <summary>
		/// 第一輪同步有沒有拋例外。一次性執行模式要靠它回報「這一趟到底同步到東西沒有」——
		/// 只看「跑完了」的話，17 支全部失敗的一輪也會回報成功，那是綠燈說謊
		/// </summary>
		public bool FirstRoundFailed { get; private set; }

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

		/// <summary>
		/// 首輪同步前的隨機延遲。17 支 Worker 都在程序啟動瞬間註冊，不錯開的話
		/// 首輪會同時打農業部 API 與 DB。
		/// 開放覆寫是為了讓 Worker 層的測試不必真的等 0–30 秒——
		/// 隨機延遲一旦寫死在流程裡，任何驗證「跑完一輪」的測試都會被它拖住
		/// </summary>
		protected virtual TimeSpan StartupJitter => TimeSpan.FromSeconds(Random.Shared.Next(0, 30));

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			// 不論從哪一條路徑離開，都要把「試過一輪」點亮，否則一次性執行模式會永遠等下去
			using var cancellationSignal = stoppingToken.Register(() => _firstRoundAttempted.TrySetResult());

			try
			{
				await Task.Delay(StartupJitter, stoppingToken);
			}
			catch (OperationCanceledException)
			{
				return;
			}

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
					if (!_firstRoundAttempted.Task.IsCompleted)
					{
						FirstRoundFailed = true;
					}
					_logger.LogError(ex, "{LogPrefix} 同步失敗", LogPrefix);
				}

				_firstRoundAttempted.TrySetResult();

				// 等待要接住取消，而且是 break 不是往外拋。原本這一行在 try 之外，
				// 停機時 TaskCanceledException 會直接離開 ExecuteAsync；平常一個行程只會發生一次
				// 所以看不出來，但一次性執行模式每一輪都會走到這裡
				try
				{
					await Task.Delay(Interval, stoppingToken);
				}
				catch (OperationCanceledException)
				{
					break;
				}
			}
		}
	}
}
