using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TaiwanAgri.Worker.Market;
using Xunit;

namespace TaiwanAgri.Tests.Worker
{
	/// <summary>
	/// 行情同步完成後發布 <c>agri.market.priceUpdated</c> 事件的那一段。
	/// <para>
	/// 這一組釘的是「沒有 broker 的環境不會被記成同步失敗」。它只在外部排程上現形：
	/// 本機把環境變數設成空字串等於刪掉它，於是沿用設定檔裡的 localhost、
	/// 本機 docker 的 RabbitMQ 有開就發布成功，所以本機怎麼跑都看不到這個問題
	/// </para>
	/// </summary>
	public class AgriProductsTransSyncWorkerTests
	{
		/// <summary>記下每一則 log，讓測試能斷言「有沒有記、記在哪個等級」</summary>
		private sealed class CapturingLogger<T> : ILogger<T>
		{
			public List<(LogLevel Level, string Message)> Entries { get; } = [];

			IDisposable? ILogger.BeginScope<TState>(TState state) => null;

			public bool IsEnabled(LogLevel logLevel) => true;

			public void Log<TState>(
				LogLevel logLevel,
				EventId eventId,
				TState state,
				Exception? exception,
				Func<TState, Exception?, string> formatter)
				=> Entries.Add((logLevel, formatter(state, exception)));
		}

		private static IConfiguration CreateConfiguration(string? hostName) =>
			new ConfigurationBuilder()
				.AddInMemoryCollection(new Dictionary<string, string?> { ["RabbitMQ:HostName"] = hostName })
				.Build();

		private static AgriProductsTransSyncWorker CreateWorker(
			string? hostName,
			ILogger<AgriProductsTransSyncWorker>? logger = null)
		{
			var httpClientFactory = new Mock<IHttpClientFactory>();
			httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient());

			return new AgriProductsTransSyncWorker(
				logger ?? NullLogger<AgriProductsTransSyncWorker>.Instance,
				httpClientFactory.Object,
				Mock.Of<IServiceScopeFactory>(),
				CreateConfiguration(hostName),
				TimeProvider.System);
		}

		// ===== 有沒有設定 broker =====

		[Theory]
		[InlineData("localhost", true)]
		[InlineData("rabbitmq", true)]
		[InlineData(null, false)]      // 本機漏設：沒有 broker
		[InlineData("", false)]        // 排程環境刻意設空字串：這裡沒有 broker
		[InlineData("   ", false)]     // 只有空白，同上
		public void 有沒有設定訊息代理_空字串與只有空白都算沒設定(string? hostName, bool expected)
		{
			var configured = AgriProductsTransSyncWorker.IsMessageBrokerConfigured(CreateConfiguration(hostName));

			// 空字串這一格是重點：寫成 ?? "localhost" 時它會回 true，然後去連一個不存在的 broker
			Assert.Equal(expected, configured);
		}

		// ===== 沒有 broker 時的發布行為 =====

		[Theory]
		[InlineData(null)]
		[InlineData("")]
		[InlineData("   ")]
		public async Task 發布事件_沒有設定主機名_不連線不拋例外並記一則資訊(string? hostName)
		{
			var logger = new CapturingLogger<AgriProductsTransSyncWorker>();
			var worker = CreateWorker(hostName, logger);

			// 這一行本身就是斷言的一半：會拋例外的話，例外會冒出同步方法、那一支被記成失敗
			await worker.PublishPriceUpdatedEventAsync();

			var entry = Assert.Single(logger.Entries);
			// 等級必須是 Information：這是正常的降級路徑，不是要人處理的警告
			Assert.Equal(LogLevel.Information, entry.Level);
			Assert.Contains("略過發布", entry.Message);
		}
	}
}
