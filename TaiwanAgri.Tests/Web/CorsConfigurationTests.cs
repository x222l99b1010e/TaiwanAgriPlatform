using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using TaiwanAgri.Web.Extensions;

namespace TaiwanAgri.Tests.Web
{
	/// <summary>
	/// CORS 來源設定的啟動檢查。
	/// 這組界限值得釘住的理由是它的失效方式：設定漏了不會有任何伺服器端訊號，
	/// 症狀只出現在使用者的瀏覽器裡，而本機因為走同源 proxy 永遠測不出來——
	/// 也就是說，只有這裡的測試能證明「漏設定會被擋下來」
	/// </summary>
	public class CorsConfigurationTests
	{
		private sealed class FakeHostEnvironment : IHostEnvironment
		{
			public string EnvironmentName { get; set; } = Environments.Production;
			public string ApplicationName { get; set; } = "TaiwanAgri.Web";
			public string ContentRootPath { get; set; } = string.Empty;
			public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
		}

		private static IHostEnvironment Environment(string name) =>
			new FakeHostEnvironment { EnvironmentName = name };

		private static IConfiguration Config(params (string Key, string Value)[] entries) =>
			new ConfigurationBuilder()
				.AddInMemoryCollection(entries.Select(e => new KeyValuePair<string, string?>(e.Key, e.Value)))
				.Build();

		[Theory]
		[InlineData("Production")]
		[InlineData("Staging")]
		public void 非開發環境未設定來源時啟動失敗(string environmentName)
		{
			var ex = Assert.Throws<InvalidOperationException>(
				() => InfrastructureExtensions.ValidateCorsConfiguration(Config(), Environment(environmentName)));

			Assert.Contains("Cors:AllowedOrigins", ex.Message);
			Assert.Contains("Cors:SameOriginOnly", ex.Message);
		}

		[Fact]
		public void 非開發環境宣告只走同源時啟動成功()
		{
			InfrastructureExtensions.ValidateCorsConfiguration(
				Config(("Cors:SameOriginOnly", "true")),
				Environment(Environments.Production));
		}

		[Fact]
		public void 非開發環境填了來源時啟動成功()
		{
			InfrastructureExtensions.ValidateCorsConfiguration(
				Config(("Cors:AllowedOrigins:0", "https://example.test")),
				Environment(Environments.Production));
		}

		[Fact]
		public void 開發環境未設定來源時啟動成功()
		{
			InfrastructureExtensions.ValidateCorsConfiguration(
				Config(),
				Environment(Environments.Development));
		}

		[Fact]
		public void 未設定來源且未宣告同源時回報缺設定()
		{
			Assert.True(InfrastructureExtensions.IsCorsOriginsMissing(Config()));
		}

		[Theory]
		[InlineData("Cors:SameOriginOnly", "true")]
		[InlineData("Cors:AllowedOrigins:0", "https://example.test")]
		public void 設定齊備時不回報缺設定(string key, string value)
		{
			Assert.False(InfrastructureExtensions.IsCorsOriginsMissing(Config((key, value))));
		}
	}
}
