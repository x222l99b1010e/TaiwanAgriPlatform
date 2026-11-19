using System.ComponentModel.DataAnnotations;

namespace TaiwanAgri.Modules.User.Dtos.ApiRequests
{
	/// <summary>
	/// 新增監看項目。長度上限對齊資料表欄位：原本沒有任何驗證，作物代碼超長在寫入時變成 500、
	/// 送一個空物件 {} 會存進一筆代碼與類別都是空字串的列（2026-11 總檢查 3-B7）。
	/// MarketType 是否為 Veg／Fruit／Flower 由 Controller 檢查（對照表在行情模組，本模組不引用它）
	/// </summary>
	public class AddWatchlistRequestDto
	{
		[Required(ErrorMessage = "作物代碼為必填"), StringLength(10, ErrorMessage = "作物代碼最多 10 個字元")]
		public string CropCode { get; set; } = string.Empty;
		[Required(ErrorMessage = "作物名稱為必填"), StringLength(50, ErrorMessage = "作物名稱最多 50 個字元")]
		public string CropName { get; set; } = string.Empty;
		[StringLength(10, ErrorMessage = "市場代碼最多 10 個字元")]
		public string? MarketCode { get; set; }
		[StringLength(100, ErrorMessage = "市場名稱最多 100 個字元")]
		public string? MarketName { get; set; }
		[Required(ErrorMessage = "作物類別為必填")]
		public string MarketType { get; set; } = string.Empty;
	}
}
