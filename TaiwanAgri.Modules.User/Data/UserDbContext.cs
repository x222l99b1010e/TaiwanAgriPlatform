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
				// UserId 是邏輯 FK，資料庫層沒有外鍵約束——與 UserNotifications／PestRuleConfigs／
				// LostPetPosts 同一種處理：它們指向的 AspNetUsers 在 ApplicationDbContext，
				// EF 不能跨 DbContext 建外鍵。
				//
				// ⚠ 這張表原本有外鍵指向 UserFarmProfiles.UserId。那不是業務規則，是「只有那裡指得到」
				// ——UserDbContext 裡唯一以使用者為鍵的表就是 UserFarmProfiles。後果是沒填過
				// 「農場設定」的帳號一新增監看就違反外鍵、拿到 500，而農場檔案本來就是選填的偏好設定
				// （系統只有 Guest／Admin 兩個角色，沒有「農民」這個身分）。監看清單是「我關心哪些作物」，
				// 與農場在哪、種什麼無關，所以改成邏輯 FK。
				//
				// 代價要記得：刪除帳號時 UserWatchlists 不再被 UserFarmProfiles 連帶刪除，
				// 必須自己清（UserFarmCrops 仍由 UserFarmProfiles 連帶刪除，那一條的歸屬是對的）。
				entity.HasIndex(c => c.UserId);
			});
		}
	}
}