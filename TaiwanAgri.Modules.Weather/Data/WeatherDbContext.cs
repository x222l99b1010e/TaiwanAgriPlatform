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
		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);

			modelBuilder.Entity<WeatherObservation>(entity =>
			{
				entity.ToTable("WeatherObservations");
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
				entity.ToTable("PestAlerts");
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
				entity.ToTable("PestAlertCities");
				entity.HasIndex(e => e.CityName)
					  .HasDatabaseName("IX_PestAlertCities_CityName");
			});
			// PestAlertCrop 設定
			modelBuilder.Entity<PestAlertCrop>(entity =>
			{
				entity.ToTable("PestAlertCrops");
				entity.HasIndex(e => e.CropName)
					  .HasDatabaseName("IX_PestAlertCrops_CropName");
			});
		}
	}
}
