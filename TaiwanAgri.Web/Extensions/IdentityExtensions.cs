using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TaiwanAgri.Core.Entities;
using TaiwanAgri.Web.Data;

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
					options.SignIn.RequireConfirmedAccount = true)
				.AddRoles<IdentityRole>()
				.AddEntityFrameworkStores<ApplicationDbContext>();

			return services;
		}
	}
}