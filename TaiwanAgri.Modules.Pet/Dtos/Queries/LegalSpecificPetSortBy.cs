namespace TaiwanAgri.Modules.Pet.Dtos.Queries
{
	/// <summary>
	/// 合法寵物業查詢的排序欄位。刻意不做「許可證效期是否過期」的布林篩選——
	/// PermitValidDate 是 DateOnly?，有些業者查無效期資料，null 該算過期還是未過期沒有
	/// 一翻兩瞪眼的答案；改成可依效期排序，過期的自然會排在最前或最後，使用者一眼看得出來，
	/// 不用回答 null 語意這個問題（owner 2026-08-06 裁示，選項 3）。
	/// </summary>
	public enum LegalSpecificPetSortBy
	{
		Name,
		PermitValidDate,
		RankGrade
	}
}
