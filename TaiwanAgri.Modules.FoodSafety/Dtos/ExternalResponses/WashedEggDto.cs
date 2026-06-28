using System.Text.Json.Serialization;

namespace TaiwanAgri.Modules.FoodSafety.Dtos.ExternalResponses
{
	public class WashedEggApiResponse
	{
		[JsonPropertyName("RS")] public string RS { get; set; } = string.Empty;
		[JsonPropertyName("Data")] public List<WashedEggDto> Data { get; set; } = new();
		[JsonPropertyName("Next")] public bool Next { get; set; }
	}

	public class WashedEggDto
	{
		[JsonPropertyName("Traceno_Start")] public string TracenoStart { get; set; } = string.Empty;
		[JsonPropertyName("Traceno_End")] public string TracenoEnd { get; set; } = string.Empty;
		[JsonPropertyName("Sel_Name")] public string SelName { get; set; } = string.Empty;
		[JsonPropertyName("Sel_Addr")] public string SelAddr { get; set; } = string.Empty;
		[JsonPropertyName("Sel_Boss")] public string SelBoss { get; set; } = string.Empty;
		[JsonPropertyName("Egg_Name1")] public string EggName1 { get; set; } = string.Empty;
		[JsonPropertyName("Far_Town_Name1")] public string FarTownName1 { get; set; } = string.Empty;
		[JsonPropertyName("Egg_Name2")] public string EggName2 { get; set; } = string.Empty;
		[JsonPropertyName("Far_Town_Name2")] public string FarTownName2 { get; set; } = string.Empty;
		[JsonPropertyName("Egg_Name3")] public string EggName3 { get; set; } = string.Empty;
		[JsonPropertyName("Far_Town_Name3")] public string FarTownName3 { get; set; } = string.Empty;
	}
}