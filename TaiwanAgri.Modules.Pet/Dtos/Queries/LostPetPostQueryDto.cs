using TaiwanAgri.Modules.Pet.Entities.Enums;

namespace TaiwanAgri.Modules.Pet.Dtos.Queries
{
	public class LostPetPostQueryDto
	{
		public LostPetPostStatus? Status { get; set; }
		public string? County { get; set; }
		public int Page { get; set; } = 1;
		public int PageSize { get; set; } = 20;
	}
}
