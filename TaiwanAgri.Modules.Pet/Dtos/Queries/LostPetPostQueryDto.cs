using TaiwanAgri.Modules.Pet.Entities.Enums;

namespace TaiwanAgri.Modules.Pet.Dtos.Queries
{
	public class LostPetPostQueryDto
	{
		public LostPetPostStatus? Status { get; set; }
		public string? County { get; set; }

		// 預設值刻意對齊改動前的固定行為（依張貼時間新到舊排序），沒帶排序參數的既有呼叫端行為不變
		public LostPetPostSortBy SortBy { get; set; } = LostPetPostSortBy.CreatedAt;
		public bool SortDescending { get; set; } = true;

		public int Page { get; set; } = 1;
		public int PageSize { get; set; } = 20;
	}
}
