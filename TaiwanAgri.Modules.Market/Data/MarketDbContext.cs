using Microsoft.EntityFrameworkCore;
using TaiwanAgri.Modules.Market.Entities;

namespace TaiwanAgri.Modules.Market.Data
{
	public class MarketDbContext : DbContext
	{
		public MarketDbContext(DbContextOptions<MarketDbContext> options) : base(options)
		{

		}
		protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
		{
			// 預設將專案中所有的 decimal 映射為 (8, 2)
			configurationBuilder.Properties<decimal>().HavePrecision(8, 2);
		}
		public DbSet<MarketRestDay> MarketRestDays => Set<MarketRestDay>();
		public DbSet<AgriProductsTrans> AgriProductsTrans => Set<AgriProductsTrans>();
		public DbSet<MarketInfo> MarketInfos => Set<MarketInfo>();	
		public DbSet<CropInfo> CropInfos => Set<CropInfo>();
		public DbSet<DebrisAlertRecord> DebrisAlertRecords => Set<DebrisAlertRecord>();
		public DbSet<PorkTrans> PorkTrans => Set<PorkTrans>();
		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);

			modelBuilder.Entity<MarketRestDay>(entity =>
			{
				entity.ToTable("MarketRestDays", schema: "market");

				entity.HasIndex(e => new { e.MarketCode, e.MarketType, e.Year, e.Month, e.RestDay })
					  .HasDatabaseName("IX_MarketRestDays_MarketCode_MarketType_Year_Month_RestDay")
					  .IsUnique();
			});

			modelBuilder.Entity<AgriProductsTrans>(entity =>
			{
				entity.ToTable("AgriProductsTrans", schema: "market");
				entity.HasIndex(e => new { e.TransDate, e.TcType, e.CropCode, e.MarketCode })
					  .HasDatabaseName("IX_AgriProductsTrans_TransDate_TcType_CropCode_MarketCode")
					  .IsUnique();
				entity.HasIndex(e => new { e.CropCode, e.TransDate })
					  .HasDatabaseName("IX_AgriProductsTrans_CropCode_TransDate");
				entity.HasIndex(e => new { e.MarketCode, e.TransDate })
					  .HasDatabaseName("IX_AgriProductsTrans_MarketCode_TransDate");
				// 新增：讓 GetCropsAsync 的 TcType 篩選可以走索引
				entity.HasIndex(e => e.TcType)
					  .HasDatabaseName("IX_AgriProductsTrans_TcType");
				// 支撐 GetLatestPricesAsync「每組 (CropCode, MarketCode) 取最新一筆」的
				// ROW_NUMBER 視窗查詢：partition 鍵開頭＋TransDate DESC；
				// 既有索引皆不以 (CropCode, MarketCode) 開頭，監看清單一多就掃全部歷史列
				entity.HasIndex(e => new { e.CropCode, e.MarketCode, e.TransDate })
					  .HasDatabaseName("IX_AgriProductsTrans_CropCode_MarketCode_TransDate")
					  .IsDescending(false, false, true);

				entity.Property(e => e.UpperPrice).HasPrecision(8, 2);
				entity.Property(e => e.MiddlePrice).HasPrecision(8, 2);
				entity.Property(e => e.LowerPrice).HasPrecision(8, 2);
				entity.Property(e => e.AvgPrice).HasPrecision(8, 2);
				entity.Property(e => e.TransQuantity).HasPrecision(8, 2);
			});

			modelBuilder.Entity<MarketInfo>(entity =>
			{
				entity.ToTable("MarketInfos", schema: "market");

				entity.HasIndex(e => new { e.MarketCode, e.MarketName })
						.HasDatabaseName("IX_MarketInfos_MarketCode_MarketName")
						.IsUnique();

				entity.HasIndex(e => new { e.MarketType })
						.HasDatabaseName("IX_MarketInfos_MarketType");
			});
			modelBuilder.Entity<CropInfo>(entity =>
			{
				entity.ToTable("CropInfos", schema: "market");
			});
			modelBuilder.Entity<DebrisAlertRecord>(entity =>
			{
				entity.ToTable("DebrisAlertRecords", schema: "market");
				entity.HasIndex(e => new { e.ReportID, e.DebrisNo, e.LandslideID })
					  .HasDatabaseName("IX_DebrisAlertRecords_ReportID_DebrisNo_LandslideID")
					  .HasFilter(null)  // 強制覆蓋 EF Core 的預設行為，不使用任何篩選器
					  .IsUnique();
			});

			modelBuilder.Entity<PorkTrans>(entity =>
			{
				// 設定資料表名稱與 Schema
				entity.ToTable("PorkTrans", schema: "market");

				// 1. 設定 (TransDate, MarketName) 的 UNIQUE constraint
				entity.HasIndex(e => new { e.TransDate, e.MarketName })
					  .HasDatabaseName("IX_PorkTrans_TransDate_MarketName")
					  .IsUnique();
			});
		}
	}
}
