using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TaiwanAgri.Modules.Pet.Entities.Enums;

namespace TaiwanAgri.Modules.Pet.Entities
{
	public class ShelterAnimal
	{
		public int Id { get; set; }
		[MaxLength(60)]
		public string AnimalSubId { get; set; } = string.Empty;
		public int ShelterPkId { get; set; }
		public AnimalKind Kind { get; set; }
		public AnimalSex Sex { get; set; }
		public AnimalBodyType BodyType { get; set; }
		public AnimalAge Age { get; set; }
		public TriState Sterilization { get; set; }
		public TriState Bacterin { get; set; }
		[MaxLength(100)]
		public string Variety { get; set; } = string.Empty;
		[MaxLength(50)]
		public string Colour { get; set; } = string.Empty;
		[MaxLength(300)]
		public string FoundPlace { get; set; } = string.Empty;
		[MaxLength(500)]
		public string Remark { get; set; } = string.Empty;
		public DateOnly? OpenDate { get; set; }
		public DateOnly CreatedTime { get; set; }
		public DateOnly? SourceUpdatedAt { get; set; }
		[Column(TypeName = "nvarchar(max)")]
		public string AlbumFile { get; set; } = string.Empty;
		[Column(TypeName = "datetime2")]
		public DateTime SyncedAt { get; set; }

		public Shelter Shelter { get; set; } = null!;
	}
}