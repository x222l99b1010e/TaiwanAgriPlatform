using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaiwanAgri.Core.Extensions;
using TaiwanAgri.Core.Infrastructure.Data;
using TaiwanAgri.Web.Extensions;
using TaiwanAgri.Worker;

namespace TaiwanAgri.Tests.Core
{
	/// <summary>
	/// 資料庫連線「遇到暫時性錯誤自動重試」這件事，要在每一個 DbContext 上都成立。
	/// <para>
	/// 漏掉一個的後果只在資料庫剛被叫醒的那 30～60 秒內出現：本機開發時資料庫永遠醒著，
	/// 建置、測試、在本機跑都不會發現。所以這裡不只測共用方法本身，還把 Web 與 Worker
	/// 實際註冊的 DbContext 逐一解析出來檢查——日後新增的 DbContext 若直接呼叫 UseSqlServer，
	/// 這裡會變紅。
	/// </para>
	/// <para>
	/// 連線字串給假的沒關係：建構 DbContext 與建立執行策略都不會真的連上資料庫。
	/// </para>
	/// </summary>
	public class SqlServerRetryTests
	{
		private const string FakeConnection = "Server=(local);Database=TestOnly;Trusted_Connection=True;";

		private static IConfiguration BuildConfiguration() =>
			new ConfigurationBuilder()
				.AddInMemoryCollection(new Dictionary<string, string?>
				{
					["ConnectionStrings:DefaultConnection"] = FakeConnection,
					// AddIdentityModule 在註冊當下就檢查 JWT 設定，缺了會直接丟例外
					["Jwt:SecretKey"] = new string('k', IdentityExtensions.MinSecretKeyBytes),
					["Jwt:Issuer"] = "TestIssuer",
					["Jwt:Audience"] = "TestAudience",
					["Jwt:ExpiresInDays"] = "7",
				})
				.Build();

		/// <summary>
		/// 先數出容器裡註冊了幾個 DbContext 再逐一檢查。數量寫死是刻意的：
		/// 找到 0 個的話「每一個都有重試」會空洞地成立，這條檢查就在通過的同時什麼都沒檢查到
		/// </summary>
		private static void AssertEveryDbContextRetries(IServiceCollection services, int expectedCount)
		{
			var contextTypes = services
				.Select(d => d.ServiceType)
				.Where(t => t != typeof(DbContext) && typeof(DbContext).IsAssignableFrom(t))
				.Distinct()
				.ToList();

			Assert.Equal(expectedCount, contextTypes.Count);

			using var provider = services.BuildServiceProvider();
			using var scope = provider.CreateScope();

			foreach (var type in contextTypes)
			{
				var context = (DbContext)scope.ServiceProvider.GetRequiredService(type);
				Assert.True(
					context.Database.CreateExecutionStrategy().RetriesOnFailure,
					$"{type.Name} 沒有開啟自動重試");
			}
		}

		[Fact]
		public void 共用設定建出來的是SqlServer的重試執行策略()
		{
			var builder = new DbContextOptionsBuilder<CoreDbContext>();
			builder.UseSqlServerWithRetry(FakeConnection);

			using var context = new CoreDbContext(builder.Options);
			var strategy = context.Database.CreateExecutionStrategy();

			Assert.IsType<SqlServerRetryingExecutionStrategy>(strategy);
			Assert.True(strategy.RetriesOnFailure);
		}

		/// <summary>
		/// 對照組：直接呼叫 UseSqlServer、沒開重試的 DbContext 必須被上面那支檢查抓出來。
		/// 少了這一則，就無法分辨「全部都有重試」與「檢查本身沒在檢查」
		/// </summary>
		[Fact]
		public void 對照組_沒開重試的DbContext會被抓出來()
		{
			var services = new ServiceCollection();
			services.AddDbContext<CoreDbContext>(options => options.UseSqlServer(FakeConnection));

			Assert.Throws<Xunit.Sdk.TrueException>(() => AssertEveryDbContextRetries(services, expectedCount: 1));
		}

		[Fact]
		public void Web註冊的七個DbContext都會自動重試()
		{
			var configuration = BuildConfiguration();
			var services = new ServiceCollection();
			services.AddLogging();
			services.AddSingleton(TimeProvider.System);
			services.AddIdentityModule(configuration);
			services.AddCoreModule(configuration);
			services.AddWeatherModule(configuration);
			services.AddMarketModule(configuration);
			services.AddUserModule(configuration);
			services.AddFoodSafetyModule(configuration);
			services.AddPetModule(configuration);

			AssertEveryDbContextRetries(services, expectedCount: 7);
		}

		[Fact]
		public void Worker註冊的五個DbContext都會自動重試()
		{
			var services = new ServiceCollection();
			services.AddWorkerDbContexts(BuildConfiguration());

			AssertEveryDbContextRetries(services, expectedCount: 5);
		}
	}
}
