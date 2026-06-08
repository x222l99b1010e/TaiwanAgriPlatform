using Microsoft.EntityFrameworkCore;
using TaiwanAgri.Modules.User.Entities;

namespace TaiwanAgri.Modules.User.Data
{
	public class UserDbContext(DbContextOptions<UserDbContext> options) : DbContext(options)
	{
		public DbSet<UserFarmProfile> UserFarmProfiles => Set<UserFarmProfile>();
		public DbSet<UserFarmCrop> UserFarmCrops => Set<UserFarmCrop>();

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);

			modelBuilder.Entity<UserFarmProfile>(entity =>
			{
				// schema 不指定，走 dbo（跟 ApplicationDbContext 同 schema）
				// 原因：User 模組是使用者業務，語意上接近 Identity，
				// 不像 Weather/Market 有獨立的業務 schema
				entity.ToTable("UserFarmProfiles");
				
			});

			modelBuilder.Entity<UserFarmCrop>(entity =>
			{
				entity.ToTable("UserFarmCrops");

				entity.HasOne(c => c.UserFarmProfile)
					  .WithMany()
					  .HasForeignKey(c => c.UserId)
					  // 使用者刪除農場設定時，作物清單跟著刪除
					  .OnDelete(DeleteBehavior.Cascade);
				// 查詢某使用者的所有作物走索引
				entity.HasIndex(c => c.UserId);
				
			});
		}
	}
}