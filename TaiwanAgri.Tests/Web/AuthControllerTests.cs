using Microsoft.AspNetCore.Mvc;
using Moq;
using TaiwanAgri.Web.Controllers;
using TaiwanAgri.Web.Dtos;
using TaiwanAgri.Web.Services;

namespace TaiwanAgri.Tests.Web
{
	/// <summary>
	/// 註冊端點只把「被 Identity 擋下」翻成 400。原本接的是 InvalidOperationException，
	/// 框架與資料庫層的內部錯誤也常用這個型別，它們的原文會被當成註冊失敗的原因回給使用者（2026-11 總檢查資安 C5）
	/// </summary>
	public class AuthControllerTests
	{
		private static readonly RegisterRequestDto Request = new() { Email = "new@example.com", Password = "P@ssw0rd!" };

		[Fact]
		public async Task 註冊被擋下時回400並帶寫給使用者的原因()
		{
			var auth = new Mock<IAuthService>();
			auth.Setup(s => s.RegisterAsync(It.IsAny<RegisterRequestDto>(), It.IsAny<CancellationToken>()))
				.ThrowsAsync(new RegistrationRejectedException("Email 'new@example.com' is already taken."));

			var result = await new AuthController(auth.Object).Register(Request);

			var badRequest = Assert.IsType<BadRequestObjectResult>(result);
			Assert.Equal("Email 'new@example.com' is already taken.", badRequest.Value);
		}

		/// <summary>其他例外不在這裡接，往上交給 GlobalExceptionMiddleware 回 500，原文只進記錄</summary>
		[Fact]
		public async Task 註冊遇到其他例外時不翻成400()
		{
			var auth = new Mock<IAuthService>();
			auth.Setup(s => s.RegisterAsync(It.IsAny<RegisterRequestDto>(), It.IsAny<CancellationToken>()))
				.ThrowsAsync(new InvalidOperationException("內部錯誤的原文"));

			await Assert.ThrowsAsync<InvalidOperationException>(
				() => new AuthController(auth.Object).Register(Request));
		}
	}
}
