using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using TaiwanAgri.Web.Extensions;
using TaiwanAgri.Web.Services;

namespace TaiwanAgri.Tests.Web
{
	/// <summary>
	/// Redis 與 RabbitMQ 作為可選相依的註冊行為。
	/// 釘住的不是「設定值讀得對不對」，而是部署面的一個保證：
	/// <b>只給資料庫連線字串與 CORS 來源，服務就能完整跑起來。</b>
	/// <para>
	/// 值得釘住的理由在於反面的代價不對稱。RabbitMQ 那一側尤其明顯：
	/// PriceUpdatedConsumer 在 StartAsync 裡建連線且不接例外，而
	/// IHostedService.StartAsync 拋例外的語意是「整個 host 起不來」——
	/// 「沒設主機就不註冊」這件事一旦被改掉，症狀是整站 503，不是少一個背景功能。
	/// Redis 那一側的代價小一些但同樣看不出來：GetStringAsync 的呼叫端只防了壞值、
	/// 沒防連線層例外，所以連不上時行情查詢是先卡數秒再回 500，不是慢一點
	/// </para>
	/// </summary>
	public class OptionalDependencyTests
	{
		private sealed class FakeHostEnvironment : IHostEnvironment
		{
			public string EnvironmentName { get; set; } = Environments.Production;
			public string ApplicationName { get; set; } = "TaiwanAgri.Web";
			public string ContentRootPath { get; set; } = string.Empty;
			public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
		}

		/// <summary>
		/// 一律帶 CORS 來源：AddInfrastructure 在非 Development 環境缺 CORS 設定會讓啟動失敗，
		/// 那是另一組測試（CorsConfigurationTests）的題目，不該在這裡混進來當雜訊
		/// </summary>
		private static IConfiguration Config(params (string Key, string? Value)[] entries)
		{
			var all = new List<KeyValuePair<string, string?>>
			{
				new("Cors:AllowedOrigins:0", "https://example.test")
			};
			all.AddRange(entries.Select(e => new KeyValuePair<string, string?>(e.Key, e.Value)));

			return new ConfigurationBuilder().AddInMemoryCollection(all).Build();
		}

		private static IServiceCollection Register(IConfiguration configuration)
		{
			var services = new ServiceCollection();
			services.AddInfrastructure(configuration, new FakeHostEnvironment());
			return services;
		}

		/// <summary>
		/// 看註冊描述元的實作型別，不建出實例：RedisCache 的建構固然不會連線，
		/// 但「測試會不會連到外部服務」這件事不該靠套件的實作細節保證。
		/// <para>
		/// 用「是不是這個型別或它的子類」而不是型別相等——AddStackExchangeRedisCache
		/// 實際註冊的是 RedisCache 的內部子類 <c>RedisCacheImpl</c>，那是套件內部的名字、
		/// 換版就可能改。要釘的是「走的是 Redis 那條路」，不是它叫什麼
		/// </para>
		/// </summary>
		private static void AssertDistributedCacheIs<TExpected>(IServiceCollection services)
		{
			var implementation = services
				.LastOrDefault(d => d.ServiceType == typeof(IDistributedCache))?.ImplementationType;

			Assert.NotNull(implementation);
			Assert.True(
				typeof(TExpected).IsAssignableFrom(implementation),
				$"預期 IDistributedCache 走 {typeof(TExpected).Name}，實際註冊的是 {implementation.Name}");
		}

		private static bool HasPriceUpdatedConsumer(IServiceCollection services) =>
			services.Any(d => d.ServiceType == typeof(IHostedService)
				&& d.ImplementationType == typeof(PriceUpdatedConsumer));

		[Fact]
		public void 未設定Redis連線字串時快取改用行程內記憶體實作()
		{
			var services = Register(Config());

			AssertDistributedCacheIs<MemoryDistributedCache>(services);
		}

		[Fact]
		public void 設定了Redis連線字串時快取走Redis實作()
		{
			var services = Register(Config(("ConnectionStrings:Redis", "localhost:6379")));

			AssertDistributedCacheIs<RedisCache>(services);
		}

		[Fact]
		public void 未設定RabbitMQ主機時不註冊事件消費者()
		{
			var services = Register(Config());

			Assert.False(HasPriceUpdatedConsumer(services));
		}

		[Fact]
		public void 設定了RabbitMQ主機時註冊事件消費者()
		{
			var services = Register(Config(("RabbitMQ:HostName", "localhost")));

			Assert.True(HasPriceUpdatedConsumer(services));
		}

		[Theory]
		[InlineData(null)]
		[InlineData("")]
		[InlineData("   ")]
		public void 連線字串留白視為未設定Redis(string? value)
		{
			Assert.False(InfrastructureExtensions.IsRedisConfigured(Config(("ConnectionStrings:Redis", value))));
		}

		[Fact]
		public void 連線字串有值時視為已設定Redis()
		{
			Assert.True(InfrastructureExtensions.IsRedisConfigured(Config(("ConnectionStrings:Redis", "localhost:6379"))));
		}

		[Theory]
		[InlineData(null)]
		[InlineData("")]
		[InlineData("   ")]
		public void 主機留白視為未設定訊息佇列(string? value)
		{
			Assert.False(InfrastructureExtensions.IsMessageBrokerConfigured(Config(("RabbitMQ:HostName", value))));
		}

		[Fact]
		public void 主機有值時視為已設定訊息佇列()
		{
			Assert.True(InfrastructureExtensions.IsMessageBrokerConfigured(Config(("RabbitMQ:HostName", "localhost"))));
		}

		/// <summary>
		/// 降級走的是正常路徑，所以唯一的痕跡就是這兩則警告。
		/// 訊息裡必須指名該填哪個設定鍵——只說「未設定」的警告，讀到的人還要回頭翻原始碼
		/// </summary>
		[Theory]
		[InlineData("ConnectionStrings:Redis")]
		[InlineData("RabbitMQ:HostName")]
		public void 降級警告指名該填的設定鍵(string configurationKey)
		{
			var warnings = InfrastructureExtensions.RedisNotConfiguredWarning
				+ InfrastructureExtensions.MessageBrokerNotConfiguredWarning;

			Assert.Contains(configurationKey, warnings);
		}
	}
}
