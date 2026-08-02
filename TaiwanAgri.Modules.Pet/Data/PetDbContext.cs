using Microsoft.EntityFrameworkCore;
using TaiwanAgri.Modules.Pet.Entities;

namespace TaiwanAgri.Modules.Pet.Data
{
	public class PetDbContext : DbContext
	{
		public PetDbContext(DbContextOptions<PetDbContext> options) : base(options)
		{
		}
		public DbSet<Shelter> Shelters => Set<Shelter>();
		public DbSet<ShelterAnimal> ShelterAnimals => Set<ShelterAnimal>();
		public DbSet<OfficialLostPetPost> OfficialLostPetPosts => Set<OfficialLostPetPost>();
		public DbSet<LegalSpecificPet> LegalSpecificPets => Set<LegalSpecificPet>();
		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);
			modelBuilder.Entity<ShelterAnimal>(entity =>
			{
				entity.ToTable("ShelterAnimals", schema: "pet");
				entity.HasIndex(e => new { e.ShelterPkId, e.AnimalSubId })
					  .HasDatabaseName("IX_ShelterAnimals_ShelterPkId_AnimalSubId")
					  .IsUnique();
				entity.HasOne(e => e.Shelter)
					  .WithMany(s => s.ShelterAnimals)
					  .HasForeignKey(e => e.ShelterPkId)
					  .OnDelete(DeleteBehavior.Restrict);
				entity.Property(e => e.Kind).HasConversion<string>();
				entity.Property(e => e.Sex).HasConversion<string>();
				entity.Property(e => e.BodyType).HasConversion<string>();
				entity.Property(e => e.Age).HasConversion<string>();
				entity.Property(e => e.Sterilization).HasConversion<string>();
				entity.Property(e => e.Bacterin).HasConversion<string>();
			});

			modelBuilder.Entity<Shelter>(entity =>
			{
				entity.ToTable("Shelters", schema: "pet");
				entity.Property(e => e.ShelterPkId).ValueGeneratedNever();
			});

			modelBuilder.Entity<OfficialLostPetPost>(entity =>
			{
				entity.ToTable("OfficialLostPetPosts", schema: "pet");
				// KeyNo 是官方資料自帶的序號，全域唯一（不像 ShelterAnimal 要搭配收容所才唯一）
				entity.HasIndex(e => e.KeyNo).IsUnique();
				entity.Property(e => e.Category).HasConversion<string>();
				entity.Property(e => e.Sex).HasConversion<string>();
			});

			modelBuilder.Entity<LegalSpecificPet>(entity =>
			{
				entity.ToTable("LegalSpecificPets", schema: "pet");
				entity.HasIndex(e => e.ExternalId).IsUnique();
				entity.Property(e => e.AnimalType).HasConversion<string>();
				entity.Property(e => e.RankGrade).HasConversion<string>();
				entity.Property(e => e.RankDataConfirmed).HasConversion<string>();
				entity.Property(e => e.RankDescriptionConfirmed).HasConversion<string>();
				entity.Property(e => e.StateFlag).HasConversion<string>();
			});
		}
	}
}
