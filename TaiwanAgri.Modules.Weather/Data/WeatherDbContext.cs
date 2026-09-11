using Microsoft.EntityFrameworkCore;
using TaiwanAgri.Modules.Weather.Entities;

namespace TaiwanAgri.Modules.Weather.Data
{
	public class WeatherDbContext : DbContext
	{
		public WeatherDbContext(DbContextOptions<WeatherDbContext> options) : base(options)
		{
		}
		public DbSet<WeatherObservation> WeatherObservations =>Set<WeatherObservation>();
		public DbSet<PestAlert> PestAlerts => Set<PestAlert>();
		public DbSet<PestAlertCity> PestAlertCities => Set<PestAlertCity>();
		public DbSet<PestAlertCrop> PestAlertCrops => Set<PestAlertCrop>();
		public DbSet<RainfallStation> RainfallStations => Set<RainfallStation>();
		public DbSet<RainfallObservation> RainfallObservations => Set<RainfallObservation>();
		public DbSet<PestDecadeSummary> PestDecadeSummaries => Set<PestDecadeSummary>();
		public DbSet<PestRuleConfig> PestRuleConfigs => Set<PestRuleConfig>();
		public DbSet<UserNotification> UserNotifications => Set<UserNotification>();
		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);

			modelBuilder.Entity<WeatherObservation>(entity =>
			{
				entity.ToTable("WeatherObservations", schema:"weather");
				// 複合索引：依「縣市 + 時間」查詢
				entity.HasIndex(e => new { e.CityCode, e.ObservedAt })
					  .HasDatabaseName("IX_WeatherObservations_CityCode_ObservedAt");

				// StationId 本身也常單獨查詢
				entity.HasIndex(e => e.StationId)
					  .HasDatabaseName("IX_WeatherObservations_StationId");

				// 數值型通知規則每次評估都以「落地時刻大於水位」開頭掃這張表，而它是每小時寫入
				// 約 876 筆、保留 30 天的滾動視窗（穩定狀態約六十萬列）。沒有這個索引，每條規則
				// 每輪評估都要全表掃描
				entity.HasIndex(e => e.SyncedAt)
					  .HasDatabaseName("IX_WeatherObservations_SyncedAt");
			});
			// PestAlert 設定
			modelBuilder.Entity<PestAlert>(entity =>
			{
				entity.ToTable("PestAlerts", schema: "weather");
				// SourceHash 是用來判斷是否重複的關鍵欄位，應該建立唯一索引
				entity.HasIndex(e => e.SourceHash)
					  .IsUnique()
					  .HasDatabaseName("IX_PestAlerts_SourceHash");
				// 宣告一對多關係
				entity.HasMany(a => a.Cities)
					  .WithOne(c => c.Alert)
					  .HasForeignKey(c => c.AlertId)
					  .OnDelete(DeleteBehavior.Cascade);

				entity.HasMany(a => a.Crops)
					  .WithOne(c => c.Alert)
					  .HasForeignKey(c => c.AlertId)
					  .OnDelete(DeleteBehavior.Cascade);
			});
			// PestAlertCity 設定
			modelBuilder.Entity<PestAlertCity>(entity =>
			{
				entity.ToTable("PestAlertCities", schema: "weather");
				entity.HasIndex(e => e.CityName)
					  .HasDatabaseName("IX_PestAlertCities_CityName");
			});
			// PestAlertCrop 設定
			modelBuilder.Entity<PestAlertCrop>(entity =>
			{
				entity.ToTable("PestAlertCrops", schema: "weather");
				entity.HasIndex(e => e.CropName)
					  .HasDatabaseName("IX_PestAlertCrops_CropName");
			});
			// RainfallStation：StationId 設為 Unique
			modelBuilder.Entity<RainfallStation>(entity =>
			{
				entity.ToTable("RainfallStations", schema: "weather");
				// StationId 是唯一識別碼，應該建立唯一索引
				entity.HasIndex(e => e.StationId)
					  .IsUnique()
					  .HasDatabaseName("IX_RainfallStations_StationId");
			});
			// RainfallObservation：建立自然鍵的 Unique Index
			modelBuilder.Entity<RainfallObservation>(entity =>
			{
				entity.ToTable("RainfallObservations", schema: "weather");
				// 自然鍵去重：同一個站台 + 同一個時間點只能有一筆
				entity.HasIndex(e => new { e.StationId, e.ObservedAt })
					  .IsUnique()
					  .HasDatabaseName("IX_RainfallObservations_StationId_ObservedAt");
			});
			//PestDecadeSummary：建立自然鍵的 Unique Index
			modelBuilder.Entity<PestDecadeSummary>(entity =>
			{
				entity.ToTable("PestDecadeSummaries", schema: "weather");
				// 自然鍵去重：同一個害蟲 + 同一年 + 同一月 + 同一旬 + 同一城市 + 同一鄉鎮只能有一筆
				entity.HasIndex(e => new { e.PestName, e.Year, e.Month, e.TenDays, e.City, e.Town })
					  .IsUnique()
					  .HasDatabaseName("IX_PestDecadeSummaries_Unique");
				entity.Property(e => e.Average)
					  .HasPrecision(10, 2);
				entity.Property(e => e.ProportionIsland)
					  .HasPrecision(10, 2);
			});

			modelBuilder.Entity<PestRuleConfig>(entity =>
			{
				entity.ToTable("PestRuleConfigs", schema: "weather");
				entity.HasIndex(p => new { p.RuleName })
				.HasDatabaseName("IX_PestRuleConfigs_RuleName");
				entity.HasIndex(p => new { p.UserId,p.IsActive })
				.HasDatabaseName("IX_PestRuleConfigs_UserId_IsActive");
				// 門檻收到小數點後一位：氣溫 32.5、雨量 100.0 都是使用者會填的真實值。
				// 精度 4 位容得下 -999.9～999.9，涵蓋高山測站的負溫與數百 mm 的日雨量
				entity.Property(p => p.Threshold)
					  .HasPrecision(4, 1);
			});

			modelBuilder.Entity<UserNotification>(entity => {
				entity.ToTable("UserNotifications", schema: "weather");
				entity.HasIndex(u => new { u.UserId, u.IsRead })
				.HasDatabaseName("IX_UserNotifications_UserId_IsRead");
				// 刪除規則時連帶刪除它產生的通知，維持「每筆通知都對應到一條存在的規則」這個不變式。
				// 這裡的設定與 EF 對必要關聯的預設行為相同，寫出來是因為刪除行為原本只能靠查資料庫
				// 或翻初始 Migration 才看得出來，而它決定了使用者刪規則時會不會連帶失去歷史通知
				entity.HasOne(u => u.PestRuleConfig)
					  .WithMany()
					  .HasForeignKey(u => u.PestRuleConfigId)
					  .OnDelete(DeleteBehavior.Cascade);
			});
		}
	}
}
