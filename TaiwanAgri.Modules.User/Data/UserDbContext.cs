using Microsoft.EntityFrameworkCore;
using TaiwanAgri.Modules.User.Entities;

namespace TaiwanAgri.Modules.User.Data
{
	public class UserDbContext(DbContextOptions<UserDbContext> options) : DbContext(options)
	{
		public DbSet<UserFarmProfile> UserFarmProfiles => Set<UserFarmProfile>();
		public DbSet<UserFarmCrop> UserFarmCrops => Set<UserFarmCrop>();
		public DbSet<UserWatchlist> UserWatchlists => Set<UserWatchlist>();

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
				// UserFarmCrop 這端：每一筆 UserFarmCrop 只屬於一個 UserFarmProfile
				// c => c.UserFarmProfile 是 UserFarmCrop 上的導覽屬性（指向主表）
				entity.HasOne(c => c.UserFarmProfile)
				// UserFarmProfile 那端：一個 UserFarmProfile 可以有很多 UserFarmCrop
				// p => p.Crops 是 UserFarmProfile 上的集合導覽屬性
				// 明確指定之後，EF Core 不會自己去猜或建立 shadow property
				.WithMany(p => p.Crops)
				// FK 欄位是 UserFarmCrop.UserId
				// 告訴 EF Core 用這個欄位做 JOIN，不要自己產生新欄位
				.HasForeignKey(c => c.UserId)
				// 主表端的 Key 是 UserFarmProfile.UserId（不是傳統的 int Id）
				// 因為 UserFarmProfile 的 PK 是字串，EF Core 需要明確告知
				.HasPrincipalKey(p => p.UserId)
				// 當 UserFarmProfile 被刪除時，底下所有 UserFarmCrop 跟著刪除
				// 避免孤兒資料（沒有對應主檔的作物清單）殘留在資料庫
				.OnDelete(DeleteBehavior.Cascade);

				// 查詢某使用者的所有作物走索引
				entity.HasIndex(c => c.UserId);
				
			});

			modelBuilder.Entity<UserWatchlist>(entity =>
			{
				entity.ToTable("UserWatchlists");
				// 查詢某使用者的所有追蹤作物走索引
				entity.HasOne<UserFarmProfile>()  // ← 用泛型指定關聯的 Entity 型別
				.WithMany()
				.HasForeignKey(c => c.UserId)
				.HasPrincipalKey(p => p.UserId)
				.OnDelete(DeleteBehavior.Cascade);

				entity.HasIndex(c => c.UserId);
			});
		}
	}
}