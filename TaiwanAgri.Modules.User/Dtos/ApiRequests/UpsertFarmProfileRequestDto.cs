using System.ComponentModel.DataAnnotations;

namespace TaiwanAgri.Modules.User.Dtos.ApiRequests
{
	/// <summary>
	/// 前端 PUT 時送來的 JSON 結構。長度上限對齊資料表欄位；作物最多 5 項
	/// （服務層「全刪全插」的前提就是數量少，原本註解寫了上限、實際沒有，存得進 200 筆，2026-11 總檢查 3-B7）。
	/// 清單裡每一項的欄位由 [ApiController] 的模型驗證逐項檢查
	/// </summary>
	public class UpsertFarmProfileRequestDto
	{
		/// <summary>主要作物的上限，前端表單（ProfileView 的 MAX_FARM_CROPS）用同一個數字</summary>
		public const int MaxCrops = 5;

		[MaxLength(20, ErrorMessage = "縣市最多 20 個字元")]
		public string? FarmCity { get; set; }
		[MaxLength(20, ErrorMessage = "農場類型最多 20 個字元")]
		public string? FarmType { get; set; }
		[MaxLength(MaxCrops, ErrorMessage = "主要作物最多 5 項")]
		public List<CropItem> Crops { get; set; } = new();
	}
}
