using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TaiwanAgri.Modules.Weather.Services;
using TaiwanAgri.Web.Extensions;

namespace TaiwanAgri.Tests.Web
{
	/// <summary>
	/// 通知規則這條線的 DI 註冊。
	/// <para>
	/// 值得釘住的理由是「漏註冊」這種錯只在執行期、而且只在有人真的呼叫那支端點時才現形：
	/// 建置會過、測試會過、應用程式會正常啟動，未登入呼叫甚至照樣回 401
	/// （授權在建構控制器之前就擋下來了，根本沒走到解析服務那一步）。
	/// 要發現它得先登入再打一次，而那是最後才會做的事。
	/// </para>
	/// <para>
	/// 這一支不去解析整個 Weather 模組的每一支服務：其中有些還需要具名的 HttpClient
	/// 這類由 Program.cs 另外註冊的東西，一起解析會因為不相干的原因失敗，
	/// 那樣的紅燈說不出「哪裡壞了」。
	/// </para>
	/// </summary>
	public class NotificationRuleRegistrationTests
	{
		/// <summary>
		/// 依 Program.cs 的組合方式建一個最小的容器。
		/// ValidateScopes 打開，才驗得到「Singleton 抓著一個 Scoped 服務」這種錯——
		/// 那種錯在正式環境是「第一個請求結束之後 DbContext 就被釋放了」，症狀離原因很遠。
		/// 連線字串給一個假的沒關係：註冊與建構 DbContext 都不會真的連上資料庫。
		/// </summary>
		private static ServiceProvider BuildProvider()
		{
			var configuration = new ConfigurationBuilder()
				.AddInMemoryCollection(new Dictionary<string, string?>
				{
					["ConnectionStrings:DefaultConnection"] = "Server=(local);Database=TestOnly;Trusted_Connection=True;",
				})
				.Build();

			var services = new ServiceCollection();
			services.AddLogging();
			services.AddSingleton(TimeProvider.System);
			services.AddWeatherModule(configuration);

			return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
		}

		[Fact]
		public void 規則服務要能從模組註冊解析出來()
		{
			using var provider = BuildProvider();
			using var scope = provider.CreateScope();

			Assert.NotNull(scope.ServiceProvider.GetRequiredService<INotificationRuleService>());
		}

		/// <summary>
		/// 規則引擎原本只註冊在 Worker 那個組合根。「立即檢查」端點在 Web 這一側，
		/// 少了這一行的話，規則服務會在解析時就失敗
		/// </summary>
		[Fact]
		public void 規則引擎在Web這一側也要註冊()
		{
			using var provider = BuildProvider();

			Assert.NotNull(provider.GetRequiredService<PestRuleEngine>());
		}
	}
}
