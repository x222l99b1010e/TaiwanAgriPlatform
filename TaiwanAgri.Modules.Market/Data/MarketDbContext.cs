using Microsoft.EntityFrameworkCore;
using TaiwanAgri.Modules.Market.Entities;

namespace TaiwanAgri.Modules.Market.Data
{
	public class MarketDbContext : DbContext
	{
		public MarketDbContext(DbContextOptions<MarketDbContext> options) : base(options)
		{

		}
		public DbSet<MarketRestDay> MarketRestDays => Set<MarketRestDay>();
		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);

			modelBuilder.Entity<MarketRestDay>(entity =>
			{
				entity.ToTable("MarketRestDays");

				entity.HasIndex(e => new { e.MarketCode, e.MarketType, e.Year, e.Month, e.RestDay })
					  .HasDatabaseName("IX_MarketRestDays_MarketCode_MarketType_Year_Month_RestDay")
					  .IsUnique();
			});
		}

	}
}
