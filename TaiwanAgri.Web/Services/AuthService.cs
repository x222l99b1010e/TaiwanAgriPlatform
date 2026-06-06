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

		public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request)
		{
			// 第一步：查使用者是否存在
			var user = await _userManager.FindByEmailAsync(request.Email);
			if (user == null)
				throw new UnauthorizedAccessException("帳號或密碼錯誤");

			// 第二步：驗證密碼
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

		public async Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request)
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
				throw new InvalidOperationException(errors);
			}

			// 第三步：指派 Guest 角色
			await _userManager.AddToRoleAsync(user, "Guest");

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
			var secretKey = _configuration["Jwt:SecretKey"]!;
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
			var expiresInDays = int.Parse(_configuration["Jwt:ExpiresInDays"]!);
			var token = new JwtSecurityToken(
				issuer: _configuration["Jwt:Issuer"],
				audience: _configuration["Jwt:Issuer"],
				claims: claims,
				expires: DateTime.UtcNow.AddDays(expiresInDays),
				signingCredentials: credentials
			);

			// 4. 序列化成字串
			return new JwtSecurityTokenHandler().WriteToken(token);
		}
	}
}