using Microsoft.Extensions.Logging.Abstractions;
using TaiwanAgri.Worker.Weather;

namespace TaiwanAgri.Tests.Worker
{
	/// <summary>
	/// 規則引擎要等氣象觀測與疫情警報的同步跑完第一輪才評估（2026-11 總檢查 3-B2）：
	/// 引擎先跑的話，當天同步進來的資料要等隔天那一輪才會產生通知
	/// </summary>
	public class PestRuleEngineWorkerTests
	{
		[Fact]
		public void 要等的是氣象觀測與疫情警報兩支同步()
		{
			Assert.Contains(typeof(WeatherSyncWorker), PestRuleEngineWorker.DependencyTypes);
			Assert.Contains(typeof(PestAlertSyncWorker), PestRuleEngineWorker.DependencyTypes);
		}

		[Fact]
		public async Task 相依的同步都試過一輪之前不往下走()
		{
			var weather = new TaskCompletionSource();
			var pest = new TaskCompletionSource();

			var waiting = PestRuleEngineWorker.WaitForFirstRoundsAsync(
				[weather.Task, pest.Task], TimeSpan.FromMinutes(1), NullLogger.Instance, CancellationToken.None);

			weather.SetResult();
			await Task.Delay(50);
			Assert.False(waiting.IsCompleted); // 只完成一支，還要等

			pest.SetResult();
			await waiting.WaitAsync(TimeSpan.FromSeconds(5));
		}

		[Fact]
		public async Task 等超過上限就照樣往下走而不是拋例外()
		{
			var neverFinishes = new TaskCompletionSource();

			await PestRuleEngineWorker.WaitForFirstRoundsAsync(
				[neverFinishes.Task], TimeSpan.FromMilliseconds(50), NullLogger.Instance, CancellationToken.None)
				.WaitAsync(TimeSpan.FromSeconds(5));
		}
	}
}
