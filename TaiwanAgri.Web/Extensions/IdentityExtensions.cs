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
			var secretKey = configuration["Jwt:SecretKey"]
				?? throw new InvalidOperationException("Jwt:SecretKey 未設定");
			var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));

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