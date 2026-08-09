namespace TaiwanAgri.Modules.Pet.Dtos.Queries
{
	/// <summary>收容所詳情頁排序選項。AnimalSubId 是農業部原始編號，字母數字混合但同一收容所內
	/// 大致隨建檔時間遞增，提供作為「依編號」排序的替代視角（跟 CreatedTime 常常同向但不保證）</summary>
	public enum ShelterAnimalSortBy
	{
		CreatedTime,
		AnimalSubId
	}
}
