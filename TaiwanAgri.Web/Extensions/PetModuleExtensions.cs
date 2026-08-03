using Microsoft.EntityFrameworkCore;
using TaiwanAgri.Modules.Pet.Data;
using TaiwanAgri.Modules.Pet.Services;

namespace TaiwanAgri.Web.Extensions
{
	public static class PetModuleExtensions
	{
		public static IServiceCollection AddPetModule(this IServiceCollection services, IConfiguration configuration)
		{
			services.AddDbContext<PetDbContext>(options =>
				options.UseSqlServer(
					configuration.GetConnectionString("DefaultConnection")));

			services.AddScoped<IPetService, PetService>();

			return services;
		}
	}
}
