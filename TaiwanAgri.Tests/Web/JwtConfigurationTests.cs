using Microsoft.Extensions.Configuration;
using TaiwanAgri.Web.Extensions;

namespace TaiwanAgri.Tests.Web
{
	/// <summary>
	/// JWT 設定的啟動檢查。
	/// 值得釘住的理由是它的失效方式：四個鍵都只在註冊與登入時才被讀到，
	/// 漏填時服務照常啟動、健康檢查照樣回 200，第一個發現的人會是要登入的使用者——
	/// 這組測試證明的是「漏填與填錯在啟動時就被擋下」
	/// </summary>
	public class JwtConfigurationTests
	{
		/// <summary>剛好 32 個位元組，是金鑰長度的下限</summary>
		private const string ValidSecretKey = "0123456789abcdef0123456789abcdef";

		/// <summary>四個鍵齊全的設定，再用 overrides 蓋掉要測的那一個</summary>
		private static IConfiguration Config(params (string Key, string? Value)[] overrides)
		{
			var values = new Dictionary<string, string?>
			{
				["Jwt:SecretKey"] = ValidSecretKey,
				["Jwt:Issuer"] = "TaiwanAgriPlatform",
				["Jwt:Audience"] = "TaiwanAgriPlatformUsers",
				["Jwt:ExpiresInDays"] = "7"
			};

			foreach (var (key, value) in overrides)
			{
				values[key] = value;
			}

			return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
		}

		[Fact]
		public void 四個鍵齊全時通過()
		{
			IdentityExtensions.ValidateJwtConfiguration(Config());
		}

		[Theory]
		[InlineData("Jwt:SecretKey")]
		[InlineData("Jwt:Issuer")]
		[InlineData("Jwt:Audience")]
		[InlineData("Jwt:ExpiresInDays")]
		public void 缺任何一個鍵時啟動失敗並指出是哪一個(string key)
		{
			var ex = Assert.Throws<InvalidOperationException>(
				() => IdentityExtensions.ValidateJwtConfiguration(Config((key, null))));

			Assert.Contains(key, ex.Message);
		}

		/// <summary>
		/// 部署平台上建了環境變數卻沒填值，讀到的是空字串而不是 null，
		/// 只擋 null 的話這個情況會一路放行到第一次登入
		/// </summary>
		[Theory]
		[InlineData("")]
		[InlineData("   ")]
		public void 空白值視同未填(string blank)
		{
			var ex = Assert.Throws<InvalidOperationException>(
				() => IdentityExtensions.ValidateJwtConfiguration(Config(("Jwt:Issuer", blank))));

			Assert.Contains("Jwt:Issuer", ex.Message);
		}

		/// <summary>一次列出全部缺漏，不必補一個、重新部署一次、才看到下一個</summary>
		[Fact]
		public void 缺好幾個鍵時一次全部列出()
		{
			var ex = Assert.Throws<InvalidOperationException>(
				() => IdentityExtensions.ValidateJwtConfiguration(
					Config(("Jwt:Issuer", null), ("Jwt:Audience", null), ("Jwt:ExpiresInDays", null))));

			Assert.Contains("Jwt:Issuer", ex.Message);
			Assert.Contains("Jwt:Audience", ex.Message);
			Assert.Contains("Jwt:ExpiresInDays", ex.Message);
		}

		[Fact]
		public void 金鑰少於32個位元組時啟動失敗()
		{
			var ex = Assert.Throws<InvalidOperationException>(
				() => IdentityExtensions.ValidateJwtConfiguration(Config(("Jwt:SecretKey", ValidSecretKey[1..]))));

			Assert.Contains("Jwt:SecretKey", ex.Message);
		}

		/// <summary>
		/// 金鑰長度是以 UTF-8 位元組計：十一個中文字只有 11 個字元，卻是 33 個位元組，
		/// 夠 HMAC-SHA256 使用。以字元數判斷的話，這組合格的金鑰會被誤擋
		/// </summary>
		[Fact]
		public void 金鑰長度以位元組計而不是字元數()
		{
			IdentityExtensions.ValidateJwtConfiguration(Config(("Jwt:SecretKey", "十一個中文字的測試金鑰")));
		}

		[Theory]
		[InlineData("七天")]
		[InlineData("1.5")]
		[InlineData("0")]
		[InlineData("-1")]
		public void 有效天數不是正整數時啟動失敗(string expiresInDays)
		{
			var ex = Assert.Throws<InvalidOperationException>(
				() => IdentityExtensions.ValidateJwtConfiguration(Config(("Jwt:ExpiresInDays", expiresInDays))));

			Assert.Contains("Jwt:ExpiresInDays", ex.Message);
		}
	}
}
