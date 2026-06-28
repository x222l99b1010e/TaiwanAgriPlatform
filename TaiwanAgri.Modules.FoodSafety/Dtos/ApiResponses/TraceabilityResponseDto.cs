namespace TaiwanAgri.Modules.FoodSafety.Dtos.ApiResponses
{
	public class TraceabilityResponseDto
	{
		// 使用者輸入的追溯碼
		public string TraceCode { get; set; } = string.Empty;

		// 農產品資訊（蔬果茶等）- 可能有多筆（同一碼對應多種產品）
		public List<AgriProductResultDto>? AgriProducts { get; set; }

		// 農產品生產者資訊
		public AgriProducerResultDto? Producer { get; set; }

		// 洗選蛋
		public WashedEggResultDto? WashedEgg { get; set; }

		// 禽肉
		public PoultryResultDto? Poultry { get; set; }
	}

	// 農產品產品資訊
	public class AgriProductResultDto
	{
		public string Product { get; set; } = string.Empty;
		public string Place { get; set; } = string.Empty;
		public string Mark { get; set; } = string.Empty;
	}

	// 農產品生產者
	public class AgriProducerResultDto
	{
		public string Producer { get; set; } = string.Empty;
		public string Address { get; set; } = string.Empty;
		public string Mark { get; set; } = string.Empty;
		public string Status { get; set; } = string.Empty;
		public string Description { get; set; } = string.Empty;
	}

	// 洗選蛋
	public class WashedEggResultDto
	{
		public string TracenoStart { get; set; } = string.Empty;
		public string TracenoEnd { get; set; } = string.Empty;
		public string SelName { get; set; } = string.Empty;
		public string SelAddr { get; set; } = string.Empty;
		public string SelBoss { get; set; } = string.Empty;  
		public string EggName1 { get; set; } = string.Empty;
		public string FarTownName1 { get; set; } = string.Empty;
		public string EggName2 { get; set; } = string.Empty;
		public string FarTownName2 { get; set; } = string.Empty;
		public string EggName3 { get; set; } = string.Empty;
		public string FarTownName3 { get; set; } = string.Empty;
	}

	// 禽肉
	public class PoultryResultDto
	{
		public string TracenoStart { get; set; } = string.Empty;
		public string TracenoEnd { get; set; } = string.Empty;  
		public string KilName { get; set; } = string.Empty;
		public string KilAddr { get; set; } = string.Empty;
		public string KilBoss { get; set; } = string.Empty; 
		public string FarmersName1 { get; set; } = string.Empty;
		public string FarmersType1 { get; set; } = string.Empty;
		public string Farmersplace1 { get; set; } = string.Empty;
		public string FarmersName2 { get; set; } = string.Empty; 
		public string FarmersType2 { get; set; } = string.Empty; 
		public string Farmersplace2 { get; set; } = string.Empty; 
		public string Cdate { get; set; } = string.Empty;
	}
}