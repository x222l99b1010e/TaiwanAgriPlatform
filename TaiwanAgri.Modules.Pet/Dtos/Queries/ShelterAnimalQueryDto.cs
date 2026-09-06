using TaiwanAgri.Modules.Pet.Entities.Enums;

namespace TaiwanAgri.Modules.Pet.Dtos.Queries
{
	/// <summary>
	/// 地圖用查詢，刻意不分頁——MarkerCluster 需要一次拿到篩選後的完整清單才能正確聚合，
	/// 分頁會讓聚合數字失真。不需要防禦性上限：回應是「一間收容所一筆」的聚合摘要，
	/// 結果集本身只有約 30 筆
	/// </summary>
	public class ShelterAnimalQueryDto
	{
		public string? County { get; set; }
		public AnimalKind? Kind { get; set; }
	}
}
