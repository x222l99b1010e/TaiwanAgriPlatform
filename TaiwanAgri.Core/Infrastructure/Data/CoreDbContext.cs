using Microsoft.EntityFrameworkCore;
using TaiwanAgri.Core.Entities;
using TaiwanAgri.Core.Infrastructure.Entities;

namespace TaiwanAgri.Core.Infrastructure.Data
{
	public class CoreDbContext : DbContext
	{
		public CoreDbContext(DbContextOptions<CoreDbContext> options) : base(options)
		{
		}
		public DbSet<SyncState> SyncStates => Set <SyncState>();
		public DbSet<NavModule> NavModules => Set<NavModule>();
		public DbSet<RoleModulePermission> RoleModulePermissions => Set<RoleModulePermission>();
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

			modelBuilder.Entity<NavModule>(entity =>
			{
				entity.ToTable("NavModules", schema: "core");
				entity.HasOne(n => n.Parent)
					  .WithMany(p => p.Children)
					  .HasForeignKey(n => n.ParentId)
					  .OnDelete(DeleteBehavior.Restrict);
			});

			modelBuilder.Entity<RoleModulePermission>(entity =>
			{
				entity.ToTable("RoleModulePermissions", schema: "core");
				entity.HasKey(r => new { r.RoleId, r.ModuleId });
				entity.HasOne(r => r.NavModule)
					  .WithMany(n => n.RoleModulePermissions)
					  .HasForeignKey(r => r.ModuleId)
					  .OnDelete(DeleteBehavior.Cascade);
			});
		}
	}
}
