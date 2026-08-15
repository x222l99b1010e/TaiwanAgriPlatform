using TaiwanAgri.Modules.Pet.Entities.Enums;

namespace TaiwanAgri.Modules.Pet.Dtos.Queries
{
	/// <summary>
	/// 收容所詳情頁用，分頁查詢單一收容所的全部在養動物。
	/// 與 <see cref="ShelterAnimalQueryDto"/>（地圖用、不分頁、跨收容所）刻意分開：
	/// 兩者的查詢範圍與回應形狀不同（一個是全台完整清單給 MarkerCluster，一個是單一收容所的分頁列表），
	/// 混在同一個 DTO 裡會讓「有沒有分頁」這件事要看呼叫端傳了什麼參數才能判斷，不夠明確。
	/// </summary>
	public class ShelterAnimalsByShelterQueryDto
	{
		public AnimalKind? Kind { get; set; }
		public AnimalSex? Sex { get; set; }

		// 預設「最新拾獲的在前」，跟其他分頁端點沒帶排序參數時的既有行為對齊（一律新到舊）
		public ShelterAnimalSortBy SortBy { get; set; } = ShelterAnimalSortBy.CreatedTime;
		public bool SortDescending { get; set; } = true;

		public int Page { get; set; } = 1;
		public int PageSize { get; set; } = 20;
	}
}
