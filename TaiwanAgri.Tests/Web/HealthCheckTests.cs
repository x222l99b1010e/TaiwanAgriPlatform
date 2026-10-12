using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using TaiwanAgri.Core.Infrastructure.Data;
using TaiwanAgri.Web.Extensions;
using TaiwanAgri.Web.HealthChecks;

namespace TaiwanAgri.Tests.Web
{
	/// <summary>
	/// 健康檢查的註冊方式。這裡最要緊的一則不是「檢查會不會過」，
	/// 而是<b>資料庫檢查有沒有被標成 readiness</b>。
	/// <para>
	/// /health/ready 只跑帶這個標籤的檢查。標籤掉了的後果不會有任何人發現：
	/// /health/ready 從此一項檢查都不跑、永遠回 200——部署後的驗收與展示前的暖機
	/// 都碰不到資料庫，前端的等待說明也會在資料庫還在醒的時候說成「仍在查詢中」
	/// </para>
	/// <para>
	/// /health 不碰資料庫是另一回事：它的篩選條件是「一項都不跑」，跟標籤無關（見 Program.cs）
	/// </para>
	/// </summary>
	public class HealthCheckTests
	{
		private sealed class FakeHostEnvironment : IHostEnvironment
		{
			public string EnvironmentName { get; set; } = Environments.Production;
			public string ApplicationName { get; set; } = "TaiwanAgri.Web";
			public string ContentRootPath { get; set; } = string.Empty;
			public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
		}

		private static HealthCheckRegistration DatabaseRegistration()
		{
			var configuration = new ConfigurationBuilder()
				.AddInMemoryCollection([new KeyValuePair<string, string?>("Cors:AllowedOrigins:0", "https://example.test")])
				.Build();

			var services = new ServiceCollection();
			services.AddInfrastructure(configuration, new FakeHostEnvironment());

			var options = services
				.BuildServiceProvider()
				.GetRequiredService<Microsoft.Extensions.Options.IOptions<HealthCheckServiceOptions>>()
				.Value;

			return Assert.Single(options.Registrations, r => r.Name == "database");
		}

		[Fact]
		public void 資料庫檢查要帶readiness標籤否則就緒檢查一項都不跑()
		{
			Assert.Contains(InfrastructureExtensions.ReadinessTag, DatabaseRegistration().Tags);
		}

		[Fact]
		public async Task 連得上資料庫時回報健康()
		{
			var options = new DbContextOptionsBuilder<CoreDbContext>()
				.UseInMemoryDatabase("TestDb_HealthCheck")
				.Options;

			var result = await new DatabaseHealthCheck(new CoreDbContext(options))
				.CheckHealthAsync(new HealthCheckContext());

			Assert.Equal(HealthStatus.Healthy, result.Status);
		}
	}
}
