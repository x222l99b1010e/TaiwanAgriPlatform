using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Moq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using TaiwanAgri.Core.Entities;
using TaiwanAgri.Tests.Helpers;
using TaiwanAgri.Web.Dtos;
using TaiwanAgri.Web.Services;

namespace TaiwanAgri.Tests.Web
{
	/// <summary>
	/// AuthService 的登入、註冊與內部的 JWT 簽發。
	/// 兩個主軸：一是「帳號不存在」與「密碼錯誤」必須給出完全相同的回應——
	/// 兩者能被區分開來就等於提供了一支帳號列舉介面；
	/// 二是建構子的 fail-fast——三個 JWT 設定缺任何一個都在建立服務時就失敗，
	/// 而不是等到第一個使用者嘗試登入才炸
	/// </summary>
	public class AuthServiceTests
	{
		private const string SecretKey = "這是一組長度足夠給 HmacSha256 使用的測試用金鑰不會用在任何真實環境";

		/// <summary>
		/// 用真的 Configuration 而不是 Mock：被測程式碼讀了五個 key，
		/// 逐個 Setup 的 Mock 比一份記憶體設定檔更長也更容易漏
		/// </summary>
		private static IConfiguration CreateConfiguration(
			string? secretKey = SecretKey, string? expiresInDays = "7") =>
			new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Jwt:SecretKey"] = secretKey,
				["Jwt:ExpiresInDays"] = expiresInDays,
				["Jwt:Issuer"] = "TaiwanAgri",
				["Jwt:Audience"] = "TaiwanAgriUsers"
			}).Build();

		/// <summary>
		/// SignInManager 的建構子要吃一個已經建好的 UserManager，所以兩者必須成對建立
		/// </summary>
		private static (Mock<UserManager<ApplicationUser>> Users, Mock<SignInManager<ApplicationUser>> SignIn) CreateManagers()
		{
			var users = IdentityTestHelpers.MockUserManager<ApplicationUser>();
			var signIn = new Mock<SignInManager<ApplicationUser>>(
				users.Object,
				Mock.Of<IHttpContextAccessor>(),
				Mock.Of<IUserClaimsPrincipalFactory<ApplicationUser>>(),
				null!, null!, null!, null!);
			return (users, signIn);
		}

		private static ApplicationUser User(string email = "farmer@example.com") => new()
		{
			Id = "user-id-1",
			Email = email,
			UserName = email,
			DisplayName = "測試農友"
		};

		/// <summary>登入成功需要三件事都成立：找得到人、密碼對、拿得到角色</summary>
		private static AuthService CreateServiceForSuccessfulLogin(
			ApplicationUser user, IList<string> roles, IConfiguration? configuration = null)
		{
			var (users, signIn) = CreateManagers();
			users.Setup(m => m.FindByEmailAsync(user.Email!)).ReturnsAsync(user);
			users.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(roles);
			signIn.Setup(m => m.CheckPasswordSignInAsync(user, It.IsAny<string>(), true))
				.ReturnsAsync(SignInResult.Success);
			return new AuthService(users.Object, signIn.Object, configuration ?? CreateConfiguration());
		}

		private static LoginRequestDto LoginRequest(string email = "farmer@example.com") =>
			new() { Email = email, Password = "P@ssw0rd!" };

		// ── 建構子的 fail-fast ───────────────────────────────────────────────

		/// <summary>
		/// JWT 設定缺漏時在建立服務的當下就失敗，而不是等到有人登入。
		/// 差別在於前者是部署時就發現，後者是第一個真實使用者發現——
		/// 而那個使用者看到的只會是一個 500
		/// </summary>
		[Theory]
		[InlineData(null, "7")]
		[InlineData(SecretKey, null)]
		public void 缺少JWT設定時在建構服務當下就失敗(string? secretKey, string? expiresInDays)
		{
			var (users, signIn) = CreateManagers();

			Assert.Throws<InvalidOperationException>(
				() => new AuthService(users.Object, signIn.Object, CreateConfiguration(secretKey, expiresInDays)));
		}

		// ── LoginAsync ───────────────────────────────────────────────────────

		/// <summary>
		/// 帳號不存在與密碼錯誤要拋出同一種例外、帶同一句訊息。
		/// 能被區分開來的話，攻擊者只要換 email 重打就能列舉出哪些帳號存在，
		/// 而這支端點是公開的。這與 NotificationService 那條「兩種找不到」是同一族問題：
		/// 內部原因不同，對外必須看起來一樣
		/// </summary>
		[Fact]
		public async Task 帳號不存在與密碼錯誤要給出完全相同的回應()
		{
			var user = User();
			var (usersA, signInA) = CreateManagers();
			usersA.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);
			var 帳號不存在 = new AuthService(usersA.Object, signInA.Object, CreateConfiguration());

			var (usersB, signInB) = CreateManagers();
			usersB.Setup(m => m.FindByEmailAsync(user.Email!)).ReturnsAsync(user);
			signInB.Setup(m => m.CheckPasswordSignInAsync(user, It.IsAny<string>(), true))
				.ReturnsAsync(SignInResult.Failed);
			var 密碼錯誤 = new AuthService(usersB.Object, signInB.Object, CreateConfiguration());

			var a = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => 帳號不存在.LoginAsync(LoginRequest()));
			var b = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => 密碼錯誤.LoginAsync(LoginRequest()));

			Assert.Equal(a.Message, b.Message);
			Assert.Equal("帳號或密碼錯誤", a.Message);
		}

		/// <summary>
		/// 密碼驗證要開啟鎖定計數（lockoutOnFailure: true），否則暴力破解不會被擋。
		/// 這個參數是布林值，設成 false 一樣會通過所有功能測試——
		/// 它唯一的效果是在連續失敗時才顯現，所以只能用互動驗證釘住
		/// </summary>
		[Fact]
		public async Task 密碼驗證要計入失敗鎖定次數()
		{
			var user = User();
			var (users, signIn) = CreateManagers();
			users.Setup(m => m.FindByEmailAsync(user.Email!)).ReturnsAsync(user);
			users.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(["Guest"]);
			signIn.Setup(m => m.CheckPasswordSignInAsync(user, It.IsAny<string>(), It.IsAny<bool>()))
				.ReturnsAsync(SignInResult.Success);

			await new AuthService(users.Object, signIn.Object, CreateConfiguration()).LoginAsync(LoginRequest());

			signIn.Verify(m => m.CheckPasswordSignInAsync(user, "P@ssw0rd!", true), Times.Once);
		}

		/// <summary>
		/// 登入成功回傳的四個欄位都要填對，前端拿它顯示使用者名稱與決定畫面權限
		/// </summary>
		[Fact]
		public async Task 登入成功回傳權杖與使用者資訊()
		{
			var user = User();

			var result = await CreateServiceForSuccessfulLogin(user, ["Admin"]).LoginAsync(LoginRequest());

			Assert.False(string.IsNullOrWhiteSpace(result.Token));
			Assert.Equal("farmer@example.com", result.Email);
			Assert.Equal("測試農友", result.DisplayName);
			Assert.Equal("Admin", result.Role);
		}

		/// <summary>
		/// 沒有任何角色的使用者退回 Guest，而不是拿到空字串的角色。
		/// 空字串會一路傳到 NavService，那裡再退一次 Guest——
		/// 兩層各退一次的話，真正壞掉時很難判斷是哪一層的問題
		/// </summary>
		[Fact]
		public async Task 沒有角色的使用者取得Guest角色()
		{
			var result = await CreateServiceForSuccessfulLogin(User(), []).LoginAsync(LoginRequest());

			Assert.Equal("Guest", result.Role);
		}

		/// <summary>
		/// 多重角色時取第一個。這是目前的實際契約——JWT 的角色宣告只放得下一個值，
		/// 而系統的權限模型本來就是單一角色制
		/// </summary>
		[Fact]
		public async Task 多重角色時取第一個()
		{
			var result = await CreateServiceForSuccessfulLogin(User(), ["Admin", "Guest"]).LoginAsync(LoginRequest());

			Assert.Equal("Admin", result.Role);
		}

		// ── JWT 內容 ─────────────────────────────────────────────────────────

		/// <summary>
		/// 權杖必須帶三個宣告：使用者 Id、Email、角色。少了 NameIdentifier 的症狀最隱蔽——
		/// 登入看起來成功，但所有「取得目前使用者的資料」的端點都會找不到人
		/// </summary>
		[Fact]
		public async Task 權杖要帶使用者Id與Email與角色三個宣告()
		{
			var user = User();

			var result = await CreateServiceForSuccessfulLogin(user, ["Admin"]).LoginAsync(LoginRequest());

			var token = new JwtSecurityTokenHandler().ReadJwtToken(result.Token);
			Assert.Equal(user.Id, token.Claims.Single(c => c.Type == ClaimTypes.NameIdentifier).Value);
			Assert.Equal(user.Email, token.Claims.Single(c => c.Type == ClaimTypes.Email).Value);
			Assert.Equal("Admin", token.Claims.Single(c => c.Type == ClaimTypes.Role).Value);
		}

		/// <summary>
		/// 有效期取自設定的天數。寫死或算錯的症狀是使用者被登出的時間點不如預期，
		/// 而那看起來只像是「偶爾要重新登入」
		/// </summary>
		[Fact]
		public async Task 權杖有效期依設定的天數計算()
		{
			var result = await CreateServiceForSuccessfulLogin(
				User(), ["Guest"], CreateConfiguration(expiresInDays: "30")).LoginAsync(LoginRequest());

			var token = new JwtSecurityTokenHandler().ReadJwtToken(result.Token);
			Assert.Equal(30, (token.ValidTo - DateTime.UtcNow).TotalDays, precision: 0);
		}

		/// <summary>
		/// 天數設定不是整數時明確拋例外，而不是靜默當成 0 天——
		/// 當成 0 的話簽出來的權杖立刻過期，症狀是「登入成功但馬上又被登出」
		/// </summary>
		[Fact]
		public async Task 有效期設定不是整數時拋例外()
		{
			var user = User();
			var service = CreateServiceForSuccessfulLogin(user, ["Guest"], CreateConfiguration(expiresInDays: "七天"));

			await Assert.ThrowsAsync<InvalidOperationException>(() => service.LoginAsync(LoginRequest()));
		}

		// ── RegisterAsync ────────────────────────────────────────────────────

		/// <summary>
		/// 註冊成功要指派 Guest 角色。漏掉的症狀是新使用者登入後側邊選單全空，
		/// 因為 NavService 查不到任何權限列
		/// </summary>
		[Fact]
		public async Task 註冊成功會指派Guest角色並直接簽發權杖()
		{
			var (users, signIn) = CreateManagers();
			users.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
				.ReturnsAsync(IdentityResult.Success);
			users.Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Guest"))
				.ReturnsAsync(IdentityResult.Success);
			var service = new AuthService(users.Object, signIn.Object, CreateConfiguration());

			var result = await service.RegisterAsync(new RegisterRequestDto
			{
				Email = "new@example.com",
				Password = "P@ssw0rd!",
				DisplayName = "新農友",
				UserType = "Farmer"
			});

			users.Verify(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Guest"), Times.Once);
			Assert.Equal("Guest", result.Role);
			Assert.Equal("new@example.com", result.Email);
			Assert.False(string.IsNullOrWhiteSpace(result.Token));
		}

		/// <summary>
		/// 註冊失敗時把 Identity 的每一條錯誤都串進訊息裡。
		/// 只取第一條的症狀是「密碼太短又缺特殊字元」只顯示其中一項，
		/// 使用者改完再送一次又被退，不知道還有第二個問題
		/// </summary>
		[Fact]
		public async Task 註冊失敗時訊息包含所有錯誤描述()
		{
			var (users, signIn) = CreateManagers();
			users.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
				.ReturnsAsync(IdentityResult.Failed(
					new IdentityError { Description = "密碼長度不足" },
					new IdentityError { Description = "密碼缺少特殊字元" }));
			var service = new AuthService(users.Object, signIn.Object, CreateConfiguration());

			var ex = await Assert.ThrowsAsync<InvalidOperationException>(
				() => service.RegisterAsync(new RegisterRequestDto { Email = "new@example.com", Password = "123" }));

			Assert.Contains("密碼長度不足", ex.Message);
			Assert.Contains("密碼缺少特殊字元", ex.Message);
		}

		/// <summary>
		/// 註冊失敗時不能繼續往下走去指派角色——建立失敗的使用者不存在，
		/// 對它指派角色的結果取決於 Identity 的實作，不該依賴
		/// </summary>
		[Fact]
		public async Task 註冊失敗時不指派角色()
		{
			var (users, signIn) = CreateManagers();
			users.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
				.ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "帳號已存在" }));
			var service = new AuthService(users.Object, signIn.Object, CreateConfiguration());

			await Assert.ThrowsAsync<InvalidOperationException>(
				() => service.RegisterAsync(new RegisterRequestDto { Email = "dup@example.com", Password = "P@ssw0rd!" }));

			users.Verify(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
		}

		/// <summary>
		/// 建立使用者時 UserName 要等於 Email。這個系統沒有獨立的帳號名稱概念，
		/// 兩者不一致會讓 FindByNameAsync 與 FindByEmailAsync 查到不同結果
		/// </summary>
		[Fact]
		public async Task 註冊時帳號名稱與電子郵件相同並保留使用者類型()
		{
			var (users, signIn) = CreateManagers();
			ApplicationUser? 建立的使用者 = null;
			users.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
				.Callback<ApplicationUser, string>((u, _) => 建立的使用者 = u)
				.ReturnsAsync(IdentityResult.Success);
			users.Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Guest"))
				.ReturnsAsync(IdentityResult.Success);
			var service = new AuthService(users.Object, signIn.Object, CreateConfiguration());

			await service.RegisterAsync(new RegisterRequestDto
			{
				Email = "new@example.com",
				Password = "P@ssw0rd!",
				DisplayName = "新農友",
				UserType = "Farmer"
			});

			Assert.NotNull(建立的使用者);
			Assert.Equal("new@example.com", 建立的使用者.UserName);
			Assert.Equal("new@example.com", 建立的使用者.Email);
			Assert.Equal("Farmer", 建立的使用者.UserType);
		}
	}
}
