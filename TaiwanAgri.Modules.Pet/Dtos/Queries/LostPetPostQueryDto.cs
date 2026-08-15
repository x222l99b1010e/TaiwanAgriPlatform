using TaiwanAgri.Modules.Pet.Entities.Enums;

namespace TaiwanAgri.Modules.Pet.Dtos.Queries
{
	public class LostPetPostQueryDto
	{
		public LostPetPostStatus? Status { get; set; }
		public string? County { get; set; }

		/// <summary>個人管理頁用（不掛週次分支新增）：true 時只回傳目前登入者自己的貼文。
		/// 必須搭配已登入的呼叫才有意義，Controller 端在未登入時直接回 401，不會走到這裡；
		/// Service 層仍多一層 currentUserId != null 的防呆，不完全依賴呼叫端已經擋過</summary>
		public bool OnlyMine { get; set; }

		// 預設值刻意對齊改動前的固定行為（依張貼時間新到舊排序），沒帶排序參數的既有呼叫端行為不變
		public LostPetPostSortBy SortBy { get; set; } = LostPetPostSortBy.CreatedAt;
		public bool SortDescending { get; set; } = true;

		public int Page { get; set; } = 1;
		public int PageSize { get; set; } = 20;
	}
}
