using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using TaiwanAgri.Core.Entities;
using TaiwanAgri.Web.Dtos;

namespace TaiwanAgri.Web.Services
{
	public class AuthService : IAuthService
	{
		private readonly UserManager<ApplicationUser> _userManager;
		private readonly SignInManager<ApplicationUser> _signInManager;
		private readonly IConfiguration _configuration;

		public AuthService(
			UserManager<ApplicationUser> userManager,
			SignInManager<ApplicationUser> signInManager,
			IConfiguration configuration)
		{
			_userManager = userManager;
			_signInManager = signInManager;
			_configuration = configuration;
		}

		public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
		{
			// 第一步：查使用者是否存在
			var user = await _userManager.FindByEmailAsync(request.Email);
			if (user == null)
			{
				// 查無帳號也照樣算一次密碼雜湊，再回同一句話。不算的話這條路幾毫秒就回，
				// 「密碼錯」那條要先跑完雜湊（PBKDF2，數十毫秒）——訊息一樣，量回應時間卻分得出
				// 哪個信箱註冊過（2026-11 總檢查資安 C5）。用同一個 PasswordHasher，成本才跟真的比對一樣。
				// 兩條路仍不會完全一樣快（密碼錯還會多寫一次失敗次數），但差距從整段雜湊縮到一次資料庫寫入
				_userManager.PasswordHasher.HashPassword(new ApplicationUser(), request.Password);
				throw new UnauthorizedAccessException("帳號或密碼錯誤");
			}

			// 第二步：驗證密碼
			// 帳號已經被鎖住時，CheckPasswordSignInAsync 不比對密碼就回，比查無帳號快得多（本機實測約 30 ms 對 110 ms），
			// 一樣分得出這個信箱存在，所以先看鎖定狀態，鎖住就跟查無帳號一樣算一次雜湊再回同一句話。
			// 不能等框架回 IsLockedOut 才補算：連錯第 5 次那一次，框架是比對過密碼才把帳號鎖住，回的也是 IsLockedOut，
			// 再補一次就變成算兩次雜湊，那一次又比其他情況慢一截（本機實測約 270 ms 對 130–170 ms）。
			// 鎖定狀態讀的是上面已經查出來的這筆使用者資料，不會多查一次資料庫
			if (await _userManager.IsLockedOutAsync(user))
			{
				_userManager.PasswordHasher.HashPassword(new ApplicationUser(), request.Password);
				throw new UnauthorizedAccessException("帳號或密碼錯誤");
			}

			var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
			if (!result.Succeeded)
				throw new UnauthorizedAccessException("帳號或密碼錯誤");

			// 第三步：取得使用者角色
			var roles = await _userManager.GetRolesAsync(user);
			var role = roles.FirstOrDefault() ?? "Guest";

			// 第四步：產生 JWT token
			var token = GenerateJwtToken(user, role);

			return new AuthResponseDto
			{
				Token = token,
				Email = user.Email ?? string.Empty,
				DisplayName = user.DisplayName,
				Role = role
			};
		}

		public async Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default)
		{
			// 第一步：建立新使用者
			var user = new ApplicationUser
			{
				UserName = request.Email,
				Email = request.Email,
				DisplayName = request.DisplayName,
				UserType = request.UserType
			};

			// 第二步：寫入資料庫（Identity 自動 hash 密碼）
			var result = await _userManager.CreateAsync(user, request.Password);
			if (!result.Succeeded)
			{
				var errors = string.Join(", ", result.Errors.Select(e => e.Description));
				throw new RegistrationRejectedException(errors);
			}

			// 第三步：指派 Guest 角色。失敗時不能回成功——帳號已經建好卻沒有角色，回應照樣帶著登入憑證，
			// 資料庫留下一筆跟其他人不一樣的帳號（2026-11 總檢查 G-C10）。刪掉剛建的帳號再回錯，使用者重新註冊就好。
			// 原因是內部的（角色由啟動時的種子建立，會失敗只剩資料庫的暫時性錯誤），所以丟一般例外、
			// 由 GlobalExceptionMiddleware 回 500，不用 RegistrationRejectedException
			var roleResult = await _userManager.AddToRoleAsync(user, "Guest");
			if (!roleResult.Succeeded)
			{
				var roleErrors = string.Join(", ", roleResult.Errors.Select(e => e.Description));
				var deleted = await _userManager.DeleteAsync(user);
				throw new InvalidOperationException(deleted.Succeeded
					? $"註冊時指派 Guest 角色失敗，已刪除剛建立的帳號：{roleErrors}"
					: $"註冊時指派 Guest 角色失敗，刪除剛建立的帳號也失敗（帳號 {user.Id} 沒有角色）：{roleErrors}");
			}

			// 第四步：產生 JWT token（註冊完直接登入）
			var token = GenerateJwtToken(user, "Guest");

			return new AuthResponseDto
			{
				Token = token,
				Email = user.Email ?? string.Empty,
				DisplayName = user.DisplayName,
				Role = "Guest"
			};
		}

		private string GenerateJwtToken(ApplicationUser user, string role)
		{
			// 1. 準備密鑰
			var secretKey = _configuration["Jwt:SecretKey"]
					?? throw new InvalidOperationException("Jwt:SecretKey 未設定");

			var expiresInDaysStr = _configuration["Jwt:ExpiresInDays"]
					?? throw new InvalidOperationException("Jwt:ExpiresInDays 未設定");

			if (!int.TryParse(expiresInDaysStr, out var expiresInDays))
				throw new InvalidOperationException("Jwt:ExpiresInDays 必須是整數");

			var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
			var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

			// 2. 準備 Claims（手環上印的資料）
			var claims = new[]
			{
				new Claim(ClaimTypes.NameIdentifier, user.Id),
				new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
				new Claim(ClaimTypes.Role, role)
			};

			// 3. 組合 token
			var token = new JwtSecurityToken(
				issuer: _configuration["Jwt:Issuer"],
				audience: _configuration["Jwt:Audience"],
				claims: claims,
				expires: DateTime.UtcNow.AddDays(expiresInDays),
				signingCredentials: credentials
			);

			// 4. 序列化成字串
			return new JwtSecurityTokenHandler().WriteToken(token);
		}
	}
}