namespace TaiwanAgri.Modules.User.Dtos.ApiRequests
{
	// Request DTO：定義前端 PUT 時送來的 JSON 結構
	public class UpsertFarmProfileRequestDto
	{
		public string? FarmCity { get; set; }
		public string? FarmType { get; set; }
		public List<CropItem> Crops { get; set; } = new();
	}
}
