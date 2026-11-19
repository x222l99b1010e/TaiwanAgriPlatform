using System.ComponentModel.DataAnnotations;

namespace TaiwanAgri.Modules.User.Dtos.ApiRequests
{
	/// <summary>農場設定裡的一種作物。長度上限對齊資料表欄位（原本超長在寫入時變成 500，2026-11 總檢查 3-B7）</summary>
	public class CropItem
	{
		[Required(ErrorMessage = "作物代碼為必填"), MaxLength(10, ErrorMessage = "作物代碼最多 10 個字元")]
		public string CropCode { get; set; } = string.Empty;
		[MaxLength(50, ErrorMessage = "作物名稱最多 50 個字元")]
		public string CropName { get; set; } = string.Empty;
	}
}
