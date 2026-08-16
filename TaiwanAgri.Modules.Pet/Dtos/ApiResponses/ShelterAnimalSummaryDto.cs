namespace TaiwanAgri.Modules.Pet.Dtos.ApiResponses
{
	/// <summary>
	/// 收容動物地圖聚合端點用：一間收容所一筆，取代原本「一隻動物一個標記」的 ShelterAnimalResponseDto 清單。
	/// 上萬筆動物只落在約 30 個收容所座標上，這支端點回傳的就是這 30 筆左右的摘要，不是分頁後的一部分。
	/// </summary>
	public class ShelterAnimalSummaryDto
	{
		public int ShelterPkId { get; set; }
		public string ShelterName { get; set; } = string.Empty;
		public string ShelterAddress { get; set; } = string.Empty;
		public string County { get; set; } = string.Empty;
		public decimal? Latitude { get; set; }
		public decimal? Longitude { get; set; }

		public int TotalCount { get; set; }

		// AnimalKind 只有固定三個成員（Dog/Cat/Other），用明確欄位而非 Dictionary<string,int>——
		// 跟前端既有 popup 摘要邏輯 counts = { Dog: 0, Cat: 0, Other: 0 } 是同一個形狀，
		// 消費端不需要多一層 key 存在性判斷
		public int DogCount { get; set; }
		public int CatCount { get; set; }
		public int OtherCount { get; set; }
	}
}
