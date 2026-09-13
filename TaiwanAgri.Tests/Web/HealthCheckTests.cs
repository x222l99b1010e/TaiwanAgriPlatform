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
	/// 標籤掉了的後果不會有任何人發現：/health 仍然回 200，只是從此每一次
	/// 存活探測都會去碰資料庫。而部署形態是 Azure SQL serverless——閒置自動暫停、
	/// 每月只有 100,000 vCore 秒（＝最省狀態下約 55.6 小時），
	/// 被定時 ping 一直叫醒的話額度會在無人察覺的情況下被燒光
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
		public void 資料庫檢查被標成readiness而不是跟著存活探測一起跑()
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
