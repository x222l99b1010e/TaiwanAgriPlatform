using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Reflection;
using TaiwanAgri.Web.Controllers;
using TaiwanAgri.Web.Extensions;

namespace TaiwanAgri.Tests.Web
{
	/// <summary>
	/// 公開查詢端點的限流設定。
	/// 這組值得釘住的理由與 CORS 同源：限流靠一個屬性掛上去，
	/// 而漏掉屬性的端點在任何測試、任何日誌、任何回應裡都跟有掛的長得一模一樣——
	/// 唯一的差別要等到有人真的濫用時才顯現。
	/// 另外設定值也要驗，因為讀進來卻沒接上使用點是這類選項最常見的錯
	/// </summary>
	public class RateLimitingConfigurationTests
	{
		private static IConfiguration Config(params (string Key, string Value)[] entries) =>
			new ConfigurationBuilder()
				.AddInMemoryCollection(entries.Select(e => new KeyValuePair<string, string?>(e.Key, e.Value)))
				.Build();

		/// <summary>
		/// 屬性掛在控制器類別上，所以新增端點時自動涵蓋。
		/// 這條測的是「有沒有掛」與「掛的是哪一個策略」——策略名稱打錯的話，
		/// ASP.NET Core 會在請求進來時才拋例外，而那是部署之後的事了
		/// </summary>
		[Fact]
		public void 行情控制器要套用公開查詢限流策略()
		{
			var attribute = typeof(MarketController).GetCustomAttribute<EnableRateLimitingAttribute>();

			Assert.NotNull(attribute);
			Assert.Equal(RateLimitingExtensions.PublicQueryPolicy, attribute.PolicyName);
		}

		/// <summary>
		/// 沒有設定檔時要有可用的預設值。預設成零的話所有請求都會被擋，
		/// 而那看起來會像是服務掛了
		/// </summary>
		[Fact]
		public void 未設定時使用可用的預設值()
		{
			var services = new ServiceCollection();
			services.AddPublicQueryRateLimiting(Config());

			var options = services.BuildServiceProvider().GetRequiredService<IOptions<RateLimitOptions>>().Value;

			Assert.True(options.PermitLimit > 0);
			Assert.True(options.WindowSeconds > 0);
		}

		/// <summary>
		/// 設定檔的值要真的被讀進來。綁定區段名稱打錯時不會有任何錯誤——
		/// 選項物件只是靜靜地維持預設值，而運維以為自己調過了
		/// </summary>
		[Fact]
		public void 設定檔的限流參數要被讀取()
		{
			var services = new ServiceCollection();
			services.AddPublicQueryRateLimiting(Config(
				("RateLimiting:PermitLimit", "120"),
				("RateLimiting:WindowSeconds", "30")));

			var options = services.BuildServiceProvider().GetRequiredService<IOptions<RateLimitOptions>>().Value;

			Assert.Equal(120, options.PermitLimit);
			Assert.Equal(30, options.WindowSeconds);
		}

		/// <summary>
		/// 預設額度要容得下一般操作。行情頁一次載入會打兩支 API，
		/// 使用者連續切換條件時很容易在一分鐘內累積十幾次請求；
		/// 上限設得太低的話真正被擋到的會是正常使用者
		/// </summary>
		[Fact]
		public void 預設額度要容得下一般頁面操作()
		{
			var options = new RateLimitOptions();

			// 每次頁面操作兩支請求，一分鐘內至少要容得下二十次操作
			Assert.True(options.PermitLimit >= 40, $"預設額度 {options.PermitLimit} 對正常操作偏緊");
		}
	}
}
