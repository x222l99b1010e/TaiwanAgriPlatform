using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaiwanAgri.Core.Infrastructure.Data;
using TaiwanAgri.Modules.FoodSafety.Data;
using TaiwanAgri.Modules.Market.Data;
using TaiwanAgri.Modules.Pet.Data;
using TaiwanAgri.Modules.User.Data;
using TaiwanAgri.Modules.Weather.Data;
using TaiwanAgri.Web;
using TaiwanAgri.Web.Data;
using TaiwanAgri.Web.Extensions;

namespace TaiwanAgri.IntegrationTests
{
	/// <summary>同一個 collection 的測試共用一個後端與一個資料庫（建表要十幾秒，不每條測試各建一次）</summary>
	[CollectionDefinition(Name)]
	public sealed class SqlServerCollection : ICollectionFixture<SqlServerAppFixture>
	{
		public const string Name = "SqlServer";
	}

	/// <summary>
	/// 在 <see cref="ConnectionEnvVar"/> 指的那台 SQL Server 上，建一個這一輪專用的資料庫、套用七個 DbContext 的 migration，
	/// 再用 <see cref="WebApplicationFactory{TEntryPoint}"/> 起整個後端；跑完把資料庫刪掉。
	/// <para>
	/// 不碰既有的資料庫：資料庫名稱每一輪都是新的，連的是哪個資料庫在起後端之後再核對一次。
	/// </para>
	/// </summary>
	public sealed class SqlServerAppFixture : IAsyncLifetime
	{
		/// <summary>指向一台 SQL Server 的連線字串（不必含資料庫名稱，有的話會被換掉）</summary>
		public const string ConnectionEnvVar = "INTEGRATION_TEST_SQL";

		private readonly string _databaseName = $"TaiwanAgri_IT_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}";
		private WebApplicationFactory<Program>? _factory;
		private string? _connectionString;

		public WebApplicationFactory<Program> Factory => _factory
			?? throw new InvalidOperationException(
				$"沒有設定環境變數 {ConnectionEnvVar}，整合測試沒有資料庫可用（CI 一定要設；本機的做法見 README「Step 8：執行測試」）");

		public async Task InitializeAsync()
		{
			var server = Environment.GetEnvironmentVariable(ConnectionEnvVar);
			// 沒有資料庫時不在這裡丟例外：本機那種情況下測試已經被 SqlServerFact 略過；
			// CI 不略過，測試一碰 Factory 就會拿到上面那句說明而失敗
			if (string.IsNullOrWhiteSpace(server)) return;

			_connectionString = new SqlConnectionStringBuilder(server) { InitialCatalog = _databaseName }.ConnectionString;

			// 後端要的設定。金鑰每一輪隨機產生，不寫進 repo
			var settings = new Dictionary<string, string?>
			{
				["ConnectionStrings:DefaultConnection"] = _connectionString,
				["Cors:SameOriginOnly"] = "true",
				["Jwt:SecretKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)),
				["Jwt:Issuer"] = "TaiwanAgri.IntegrationTests",
				["Jwt:Audience"] = "TaiwanAgri.IntegrationTests",
				["Jwt:ExpiresInDays"] = "1",
			};

			// 先建表：Program.Main 在後端起來之前就會寫種子資料（角色、選單、收容所），表要先在
			await MigrateAllAsync(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());

			// 給後端的設定一律用環境變數蓋：環境變數排在 appsettings.json 之後，一定贏過它（appsettings.json 裡有本機開發資料庫的連線字串）；
			// 而 Program.Main 註冊服務時就把設定讀走了，工廠的 ConfigureAppConfiguration 來不及影響那些值
			foreach (var (key, value) in settings)
				Environment.SetEnvironmentVariable(key.Replace(":", "__"), value);

			// Testing 環境：不讀 User Secrets（那是 Development 才讀的，裡面有本機開發用的連線字串與 Redis）
			_factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseEnvironment("Testing"));

			// 起後端並核對：連的必須是這一輪建的資料庫；Redis 與 RabbitMQ 必須沒設定（走降級路徑，不連本機的快取與佇列）
			var configuration = _factory.Services.GetRequiredService<IConfiguration>();
			if (configuration.GetConnectionString("DefaultConnection") != _connectionString)
				throw new InvalidOperationException("後端讀到的連線字串不是這一輪建的測試資料庫，為了不寫到別的資料庫，停止");
			if (InfrastructureExtensions.IsRedisConfigured(configuration) || InfrastructureExtensions.IsMessageBrokerConfigured(configuration))
				throw new InvalidOperationException("整合測試的環境裡設了 Redis 或 RabbitMQ，結果會受外部狀態影響，停止");
		}

		public async Task DisposeAsync()
		{
			if (_factory is not null) await _factory.DisposeAsync();
			if (_connectionString is null) return;

			// 連線池裡還有連到測試資料庫的連線，不清掉的話 DROP 會被擋
			SqlConnection.ClearAllPools();
			var master = new SqlConnectionStringBuilder(_connectionString) { InitialCatalog = "master" }.ConnectionString;
			await using var connection = new SqlConnection(master);
			await connection.OpenAsync();
			await using var command = connection.CreateCommand();
			// 名稱是這個類別自己組的（英數字與底線），不是外部輸入
			command.CommandText = $"ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_databaseName}];";
			await command.ExecuteNonQueryAsync();
		}

		/// <summary>順序同 Dockerfile 的 migration bundle</summary>
		private static readonly Type[] DbContextsInMigrationOrder =
		[
			typeof(ApplicationDbContext), typeof(WeatherDbContext), typeof(MarketDbContext), typeof(CoreDbContext),
			typeof(UserDbContext), typeof(FoodSafetyDbContext), typeof(PetDbContext),
		];

		/// <summary>
		/// DbContext 從「用後端那幾支模組擴充方法註冊出來的 DI 容器」取，不自己 new：
		/// Identity 那張表的形狀取決於 DI 裡的 IdentityOptions，自己 new 出來的 DbContext 讀不到，
		/// EF 會判定模型跟 migration 對不上（PendingModelChangesWarning）而拒絕套用。
		/// 連線設定因此也跟後端完全一樣（同一支 UseSqlServerWithRetry）
		/// </summary>
		private static async Task MigrateAllAsync(IConfiguration configuration)
		{
			var services = new ServiceCollection();
			services.AddLogging();
			services.AddIdentityModule(configuration);
			services.AddWeatherModule(configuration);
			services.AddMarketModule(configuration);
			services.AddCoreModule(configuration);
			services.AddUserModule(configuration);
			services.AddFoodSafetyModule(configuration);
			services.AddPetModule(configuration);
			await using var provider = services.BuildServiceProvider();

			foreach (var contextType in DbContextsInMigrationOrder)
			{
				await using var scope = provider.CreateAsyncScope();
				var db = (DbContext)scope.ServiceProvider.GetRequiredService(contextType);
				await db.Database.MigrateAsync();
			}
		}
	}

	/// <summary>
	/// 要 SQL Server 的測試。沒有設定 <see cref="SqlServerAppFixture.ConnectionEnvVar"/> 時：
	/// 本機略過（列成 Skipped 並附原因，不會算成通過）；CI 不略過（GitHub Actions 一律設 <c>CI=true</c>），
	/// 讓測試失敗——CI 少了資料庫要變紅，不能整組安靜略過還顯示綠勾
	/// </summary>
	public sealed class SqlServerFactAttribute : FactAttribute
	{
		public SqlServerFactAttribute()
		{
			var hasDatabase = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(SqlServerAppFixture.ConnectionEnvVar));
			var isCi = string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase);
			if (!hasDatabase && !isCi)
				Skip = $"沒有設定 {SqlServerAppFixture.ConnectionEnvVar}，略過整合測試（執行方式見 README「Step 8：執行測試」）";
		}
	}
}
