namespace TaiwanAgri.Modules.Pet.Dtos.ApiResponses
{
	public class ShelterAnimalResponseDto
	{
		public int Id { get; set; }
		public string AnimalSubId { get; set; } = string.Empty;

		// 收容所資訊（透過 Include(Shelter) 帶出，前端地圖標記彈出視窗用，不需要再查一次）。
		// ShelterPkId 是收容所人工維護的真實 PK（見 Shelter entity），不掛週次分支新增：
		// 前端收容所詳情頁 /pet/shelter-map/:shelterId 需要一個穩定識別碼組連結，
		// 座標字串不適合當路由參數（浮點數精度、URL 可讀性都不理想）
		public int ShelterPkId { get; set; }
		public string ShelterName { get; set; } = string.Empty;
		public string ShelterAddress { get; set; } = string.Empty;
		public string County { get; set; } = string.Empty;
		public decimal? Latitude { get; set; }
		public decimal? Longitude { get; set; }

		// enum 一律轉字串輸出：專案未全域註冊 JsonStringEnumConverter，
		// 直接回傳 enum 會被序列化成數字，前端無法直接讀
		public string Kind { get; set; } = string.Empty;
		public string Sex { get; set; } = string.Empty;
		public string BodyType { get; set; } = string.Empty;
		public string Age { get; set; } = string.Empty;
		public string Sterilization { get; set; } = string.Empty;
		public string Bacterin { get; set; } = string.Empty;

		public string Variety { get; set; } = string.Empty;
		public string Colour { get; set; } = string.Empty;
		public string FoundPlace { get; set; } = string.Empty;
		public string Remark { get; set; } = string.Empty;
		public DateOnly? OpenDate { get; set; }
		public DateOnly CreatedTime { get; set; }
		public string AlbumFile { get; set; } = string.Empty;
	}
}
