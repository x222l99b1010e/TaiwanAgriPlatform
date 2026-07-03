using Microsoft.EntityFrameworkCore;
using TaiwanAgri.Modules.FoodSafety.Entities;

namespace TaiwanAgri.Modules.FoodSafety.Data
{
	public class FoodSafetyDbContext : DbContext
	{
		public FoodSafetyDbContext(DbContextOptions<FoodSafetyDbContext> options) : base(options)
		{
		}
		protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
		{
			// 預設將專案中所有的 decimal 映射為 (8, 2)
			configurationBuilder.Properties<decimal>().HavePrecision(8, 2);
		}

		public DbSet<PesticideViolation> PesticideViolations => Set<PesticideViolation>();
		public DbSet<OrganicCertification> OrganicCertifications => Set<OrganicCertification>();

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);
			modelBuilder.Entity<PesticideViolation>(entity =>
			{
				entity.ToTable("PesticideViolations", schema: "foodsafety");
				entity.HasIndex(e => e.Number)
					  .HasDatabaseName("IX_PesticideViolations_Number")
					  .IsUnique();
				entity.HasIndex(e => e.SamplingDate)
					  .HasDatabaseName("IX_PesticideViolations_SamplingDate");
			});

			modelBuilder.Entity<OrganicCertification>(entity =>
			{
				entity.ToTable("OrganicCertifications", schema: "foodsafety");
				entity.HasIndex(e => e.CertOrganicSn)
					  .HasDatabaseName("IX_OrganicCertifications_CertOrganicSn")
					  .IsUnique();
			});
		}
	}
}
