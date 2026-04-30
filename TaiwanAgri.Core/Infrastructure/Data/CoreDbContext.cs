using Microsoft.EntityFrameworkCore;
using TaiwanAgri.Core.Infrastructure.Entities;

namespace TaiwanAgri.Core.Infrastructure.Data
{
	public class CoreDbContext : DbContext
	{
		public CoreDbContext(DbContextOptions<CoreDbContext> options) : base(options)
		{
		}
		public DbSet<SyncState> SyncStates => Set <SyncState>();
		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);
			modelBuilder.Entity<SyncState>(entity =>
			{
				entity.ToTable("SyncStates", schema: "core");
				entity.HasIndex(s => s.SyncKey)
					  .HasDatabaseName("IX_SyncStates_SyncKey")
					  .IsUnique();
			});
		}
	}
}
