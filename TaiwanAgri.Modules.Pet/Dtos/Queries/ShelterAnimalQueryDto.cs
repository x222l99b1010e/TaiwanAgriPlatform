using TaiwanAgri.Modules.Pet.Entities.Enums;

namespace TaiwanAgri.Modules.Pet.Dtos.Queries
{
	/// <summary>
	/// 地圖用查詢，刻意不分頁——MarkerCluster 需要一次拿到篩選後的完整清單才能正確聚合，
	/// 分頁會讓聚合數字失真（地圖端點不套用 Skip/Take，改用篩選條件＋防禦性上限限縮結果量）
	/// </summary>
	public class ShelterAnimalQueryDto
	{
		public string? County { get; set; }
		public AnimalKind? Kind { get; set; }
	}
}
