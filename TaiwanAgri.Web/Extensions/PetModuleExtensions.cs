using Microsoft.EntityFrameworkCore;
using TaiwanAgri.Modules.Pet.Data;

namespace TaiwanAgri.Web.Extensions
{
	public static class PetModuleExtensions
	{
		public static IServiceCollection AddPetModule(this IServiceCollection services, IConfiguration configuration)
		{
			services.AddDbContext<PetDbContext>(options =>
				options.UseSqlServer(
					configuration.GetConnectionString("DefaultConnection")));

			return services;
		}
	}
}
