using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using TaiwanAgri.Core.Entities;
using TaiwanAgri.Web.Data;
using TaiwanAgri.Web.Services;

namespace TaiwanAgri.Web.Extensions
{
	public static class IdentityExtensions
	{
		/// <summary>HMAC-SHA256 要求 256 bits 的金鑰</summary>
		public const int MinSecretKeyBytes = 32;

		private static readonly string[] RequiredJwtKeys =
			["Jwt:SecretKey", "Jwt:Issuer", "Jwt:Audience", "Jwt:ExpiresInDays"];

		/// <summary>
		/// JWT 設定的啟動檢查。四個鍵都只在簽發或驗證 token 時才被讀到，
		/// 缺了不會讓啟動失敗、健康檢查照樣是綠的，要等第一個人註冊或登入才回 500——
		/// 所以在註冊服務時就擋下，讓漏填出現在部署當下的啟動記錄裡。
		/// <para>
		/// 部署時特別容易漏的是 Issuer／Audience／ExpiresInDays：它們不是機密，
		/// 本機卻多半跟金鑰一起放在 User Secrets 或 appsettings.Development.json，
		/// 兩者在正式環境都不會被讀取。
		/// </para>
		/// <para>
		/// 金鑰長度以 UTF-8 位元組計、不是字元數：太短不會在建立金鑰時報錯，
		/// 而是簽第一張 token 時才拋例外，失效方式跟漏填一樣
		/// </para>
		/// </summary>
		public static void ValidateJwtConfiguration(IConfiguration configuration)
		{
			var missing = RequiredJwtKeys
				.Where(key => string.IsNullOrWhiteSpace(configuration[key]))
				.ToList();

			if (missing.Count > 0)
			{
				throw new InvalidOperationException(
					$"JWT 設定缺少：{string.Join("、", missing)}。" +
					"以環境變數提供時，階層改用雙底線表示（例：Jwt__Issuer）。");
			}

			if (Encoding.UTF8.GetByteCount(configuration["Jwt:SecretKey"]!) < MinSecretKeyBytes)
			{
				throw new InvalidOperationException(
					$"Jwt:SecretKey 至少要 {MinSecretKeyBytes} 個位元組（UTF-8 編碼後計算；英數字一個字元一個位元組）。");
			}

			if (!int.TryParse(configuration["Jwt:ExpiresInDays"], out var expiresInDays) || expiresInDays <= 0)
			{
				throw new InvalidOperationException("Jwt:ExpiresInDays 必須是正整數（token 有效天數）。");
			}
		}

		public static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration configuration)
		{
			var connectionString = configuration.GetConnectionString("DefaultConnection")
				?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

			services.AddDbContext<ApplicationDbContext>(options =>
				options.UseSqlServer(connectionString));

			services.AddDefaultIdentity<ApplicationUser>(options =>
			// 先不驗證信箱帳號
					options.SignIn.RequireConfirmedAccount = false)
				.AddRoles<IdentityRole>()
				.AddEntityFrameworkStores<ApplicationDbContext>();

			services.AddScoped<IAuthService, AuthService>();

			// JWT Middleware 設定
			ValidateJwtConfiguration(configuration);
			var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:SecretKey"]!));

			services.AddAuthentication(options =>
			{
				options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
				options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
			})
			.AddJwtBearer(options =>
			{
				options.TokenValidationParameters = new TokenValidationParameters
				{
					ValidateIssuer = true,
					ValidateAudience = true,
					ValidateLifetime = true,
					ValidateIssuerSigningKey = true,
					ValidIssuer = configuration["Jwt:Issuer"],
					ValidAudience = configuration["Jwt:Audience"],
					IssuerSigningKey = key
				};
			});

			return services;
		}
	}
}