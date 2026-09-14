using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using TaiwanAgri.Worker;

namespace TaiwanAgri.Tests.Worker
{
	/// <summary>
	/// 一次性執行模式：跑完一輪就讓程序結束，給外部排程（GitHub Actions cron）觸發用。
	/// <para>
	/// 這一組要釘的是「收工判斷不會說謊」。最容易寫錯而且不會被發現的兩種：
	/// ①用固定等待時間代替真正的完成訊號（同步變慢的那天會安靜地少同步幾支）；
	/// ②不論結果一律回 exit code 0（排程每天顯示成功，實際上什麼都沒同步到）。
	/// 兩者的共同點都是綠燈，所以只有測試看得出差別
	/// </para>
	/// </summary>
	/// <summary>
	/// 這一組不與其他測試集合並行。
	/// <para>
	/// 理由是實測出來的，不是預防性的：這一組量的是 IHostedService 的生命週期時序
	/// （StartAsync 起跑、StopAsync 取消、ExecuteAsync 收尾），而那完全取決於執行緒集區
	/// 什麼時候把延續交出來。並行跑全套 450 條時，「取消之後訊號會不會亮」這一則
	/// 十次會失敗九次——連逾時放寬到 30 秒都還是不亮，不是斷言寫得太嚴，是延續根本排不到。
	/// </para>
	/// <para>
	/// ⚠ 不要改成調高逾時了事。這條路走過一次：第一版逾時 5 秒、被讀成「機器忙」，
	/// 差點就把它當 flaky test 處理掉——而當時底下藏的是一個真的 bug
	/// （用 CancellationToken.Register 當「一定會發生」的保證，回呼會被同一次取消
	/// 所喚醒的路徑搶先 Dispose）。調高逾時會把那個 bug 一起藏起來
	/// </para>
	/// </summary>
	[CollectionDefinition(WorkerLifecycleCollection.Name, DisableParallelization = true)]
	public sealed class WorkerLifecycleCollection
	{
		public const string Name = "Worker lifecycle";
	}

	[Collection(WorkerLifecycleCollection.Name)]
	public class RunOnceCoordinatorTests
	{
		/// <summary>
		/// 可控的假 Worker。jitter 歸零、輪距拉長——真的等 0–30 秒隨機延遲的話，
		/// 這一組測試會慢到沒有人願意跑
		/// </summary>
		private sealed class FakeSyncWorker : ScheduledSyncWorkerBase
		{
			private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
			private readonly bool _throwOnSync;

			public FakeSyncWorker(bool throwOnSync = false) : base(NullLogger.Instance)
				=> _throwOnSync = throwOnSync;

			protected override TimeSpan StartupJitter => TimeSpan.Zero;
			protected override TimeSpan Interval => TimeSpan.FromHours(1);
			protected override string LogPrefix => "[Fake]";

			/// <summary>讓測試決定這一輪同步什麼時候結束</summary>
			public void FinishSync() => _release.TrySetResult();

			protected override async Task SyncAsync(CancellationToken stoppingToken)
			{
				await _release.Task.WaitAsync(stoppingToken);
				if (_throwOnSync)
				{
					throw new InvalidOperationException("模擬同步失敗");
				}
			}
		}

		private sealed class FakeLifetime : IHostApplicationLifetime
		{
			private readonly CancellationTokenSource _stopping = new();
			public CancellationToken ApplicationStarted => CancellationToken.None;
			public CancellationToken ApplicationStopping => _stopping.Token;
			public CancellationToken ApplicationStopped => CancellationToken.None;
			public bool StopRequested { get; private set; }
			public void StopApplication()
			{
				StopRequested = true;
				_stopping.Cancel();
			}
		}

		private static (RunOnceCoordinator Coordinator, FakeLifetime Lifetime) Build(
			IEnumerable<FakeSyncWorker> workers, TimeSpan? timeout = null)
		{
			var services = new ServiceCollection();
			foreach (var worker in workers)
			{
				services.AddSingleton<IHostedService>(worker);
			}

			var lifetime = new FakeLifetime();
			var coordinator = new RunOnceCoordinator(
				services.BuildServiceProvider(),
				lifetime,
				NullLogger<RunOnceCoordinator>.Instance,
				timeout ?? TimeSpan.FromSeconds(10));

			return (coordinator, lifetime);
		}

		/// <summary>
		/// 起跑一律走 <see cref="Task.Run(Func{Task})"/>，讓 Worker 的延續落在執行緒集區上。
		/// 直接在測試執行緒上呼叫的話，ExecuteAsync 裡每一個 await 都會捕捉測試框架的同步內容，
		/// 之後「取消之後收尾」那條延續就得排在其他測試後面，量起來會變成排程速度而不是行為
		/// </summary>
		private static async Task StartAsync(params FakeSyncWorker[] workers)
		{
			foreach (var worker in workers)
			{
				await Task.Run(() => worker.StartAsync(CancellationToken.None));
			}
		}

		[Fact]
		public async Task 全部跑完一輪之後才收工()
		{
			var slow = new FakeSyncWorker();
			var quick = new FakeSyncWorker();
			await StartAsync(slow, quick);
			var (coordinator, lifetime) = Build([slow, quick]);

			await coordinator.StartAsync(CancellationToken.None);
			quick.FinishSync();
			await quick.FirstRoundAttempted;

			// 只有一支跑完，另一支還卡在同步裡——這時候收工就是少同步一支
			Assert.False(lifetime.StopRequested);

			slow.FinishSync();
			await coordinator.ExecuteTask!;

			Assert.True(lifetime.StopRequested);
			Assert.Equal(0, coordinator.ResultExitCode);
		}

		[Fact]
		public async Task 部分同步失敗不算整趟失敗()
		{
			// 外部資料源偶爾不通是常態，每一支自己有隔天重跑的兜底。
			// 這種也回非零的話，排程會長期紅著，紅燈就失去意義
			var ok = new FakeSyncWorker();
			var broken = new FakeSyncWorker(throwOnSync: true);
			await StartAsync(ok, broken);
			var (coordinator, _) = Build([ok, broken]);

			await coordinator.StartAsync(CancellationToken.None);
			ok.FinishSync();
			broken.FinishSync();
			await coordinator.ExecuteTask!;

			Assert.True(broken.FirstRoundFailed);
			Assert.False(ok.FirstRoundFailed);
			Assert.Equal(0, coordinator.ResultExitCode);
		}

		[Fact]
		public async Task 全部同步失敗要回非零結束碼()
		{
			// 這一趟什麼都沒同步到，卻回報成功的話，排程每天都是綠燈而資料停在原地
			var first = new FakeSyncWorker(throwOnSync: true);
			var second = new FakeSyncWorker(throwOnSync: true);
			await StartAsync(first, second);
			var (coordinator, lifetime) = Build([first, second]);

			await coordinator.StartAsync(CancellationToken.None);
			first.FinishSync();
			second.FinishSync();
			await coordinator.ExecuteTask!;

			Assert.Equal(1, coordinator.ResultExitCode);
			Assert.True(lifetime.StopRequested);
		}

		[Fact]
		public async Task 逾時要回非零結束碼而不是安靜收工()
		{
			var stuck = new FakeSyncWorker();
			await StartAsync(stuck);
			var (coordinator, lifetime) = Build([stuck], TimeSpan.FromMilliseconds(200));

			await coordinator.StartAsync(CancellationToken.None);
			await coordinator.ExecuteTask!;

			Assert.Equal(1, coordinator.ResultExitCode);
			Assert.True(lifetime.StopRequested);
			Assert.False(stuck.FirstRoundAttempted.IsCompleted);
		}

		/// <summary>
		/// 卡在同步裡的 Worker 被停機取消時，訊號一定要亮——協調器就是在等這個訊號，
		/// 不亮的話它會等一個永遠不會來的東西。
		/// <para>
		/// ⚠ StopAsync 回來不代表 ExecuteAsync 已經收尾：.NET 10 的 BackgroundService 實測起來，
		/// StopAsync 回來當下 ExecuteTask 讀到的是 Canceled，而 ExecuteAsync 的 finally 還沒跑到。
		/// 所以這裡只能等訊號本身，不能改成等 ExecuteTask（await 一個取消狀態的 Task 會直接拋）。
		/// 逾時三十秒是看門狗、不是斷言的機制——讓它穩定的是 Worker 在測試執行緒之外起跑
		/// </para>
		/// </summary>
		[Fact]
		public async Task 停機取消時第一輪訊號也要亮否則協調器會永遠等下去()
		{
			var worker = new FakeSyncWorker();
			await StartAsync(worker);

			await worker.StopAsync(CancellationToken.None);

			await worker.FirstRoundAttempted.WaitAsync(TimeSpan.FromSeconds(30));
		}
	}
}
