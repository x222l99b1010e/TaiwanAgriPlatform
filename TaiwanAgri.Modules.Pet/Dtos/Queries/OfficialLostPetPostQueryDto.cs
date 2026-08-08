using TaiwanAgri.Modules.Pet.Entities.Enums;

namespace TaiwanAgri.Modules.Pet.Dtos.Queries
{
	public class OfficialLostPetPostQueryDto
	{
		public AnimalKind? Category { get; set; }
		public AnimalSex? Sex { get; set; }

		// 預設值刻意對齊改動前的固定行為（依走失時間新到舊排序），沒帶排序參數的既有呼叫端行為不變
		public OfficialLostPetPostSortBy SortBy { get; set; } = OfficialLostPetPostSortBy.LostTime;
		public bool SortDescending { get; set; } = true;

		public int Page { get; set; } = 1;
		public int PageSize { get; set; } = 20;
	}
}
