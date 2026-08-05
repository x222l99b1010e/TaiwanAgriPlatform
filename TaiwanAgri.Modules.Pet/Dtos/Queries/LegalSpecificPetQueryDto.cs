using TaiwanAgri.Modules.Pet.Entities.Enums;

namespace TaiwanAgri.Modules.Pet.Dtos.Queries
{
	public class LegalSpecificPetQueryDto
	{
		public string? County { get; set; }
		public LegalPetAnimalType? AnimalType { get; set; }
		public LegalPetRankGrade? RankGrade { get; set; }
		public LegalPetStateFlag? StateFlag { get; set; }

		/// <summary>比對 BusinessItems（如 "ABC"）是否包含這個代碼字元；A=繁殖 B=買賣 C=寄養，單一字元</summary>
		public string? BusinessItem { get; set; }

		public LegalSpecificPetSortBy SortBy { get; set; } = LegalSpecificPetSortBy.Name;
		public bool SortDescending { get; set; } = false;

		public int Page { get; set; } = 1;
		public int PageSize { get; set; } = 20;
	}
}
