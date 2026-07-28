using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiwanAgri.Modules.Pet.Entities
{
	/// <summary>
	/// 收容所主檔，人工維護種子資料（37 筆，對應 MOA animal_shelter_pkid）
	/// ShelterPkId 直接作為 PK，不另設代理鍵——與 ShelterAnimal 不同，
	/// 這張表沒有「外部系統值被竄改」的風險（人工維護，非 Worker 同步）
	/// </summary>
	public class Shelter
	{
		[Key]
		public int ShelterPkId { get; set; }

		[MaxLength(200)]
		public string Name { get; set; } = string.Empty;

		[MaxLength(300)]
		public string Address { get; set; } = string.Empty;

		[MaxLength(100)]
		public string Tel { get; set; } = string.Empty;

		[MaxLength(20)]
		public string County { get; set; } = string.Empty;

		[Column(TypeName = "decimal(10,6)")]
		public decimal? Latitude { get; set; }

		[Column(TypeName = "decimal(10,6)")]
		public decimal? Longitude { get; set; }

		public ICollection<ShelterAnimal> ShelterAnimals { get; set; } = new List<ShelterAnimal>();
	}
}
