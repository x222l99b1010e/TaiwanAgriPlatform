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
				// 文件 6.4 節要求的複合索引：依縣市 + 時間查詢
				entity.HasIndex(e => new { e.CityCode, e.ObservedAt })
					  .HasDatabaseName("IX_WeatherObservations_CityCode_ObservedAt");

				// StationId 本身也常單獨查詢
				entity.HasIndex(e => e.StationId)
					  .HasDatabaseName("IX_WeatherObservations_StationId");
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
			});

			modelBuilder.Entity<UserNotification>(entity => {
				entity.ToTable("UserNotifications", schema: "weather");
				entity.HasIndex(u => new { u.UserId, u.IsRead })
				.HasDatabaseName("IX_UserNotifications_UserId_IsRead");
			});
		}
	}
}
