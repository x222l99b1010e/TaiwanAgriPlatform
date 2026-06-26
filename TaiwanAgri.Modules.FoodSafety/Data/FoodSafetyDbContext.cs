using Microsoft.EntityFrameworkCore;

namespace TaiwanAgri.Modules.FoodSafety.Data
{
	public class FoodSafetyDbContext : DbContext
	{
		public FoodSafetyDbContext(DbContextOptions<FoodSafetyDbContext> options) : base(options)
		{
		}
		protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
		{
			// 預設將專案中所有的 decimal 映射為 (8, 2)
			configurationBuilder.Properties<decimal>().HavePrecision(8, 2);
		}
	}
}
