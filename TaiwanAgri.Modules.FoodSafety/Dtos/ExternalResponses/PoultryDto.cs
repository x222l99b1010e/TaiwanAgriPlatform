using System.Text.Json.Serialization;

namespace TaiwanAgri.Modules.FoodSafety.Dtos.ExternalResponses
{
	public class PoultryApiResponse
	{
		[JsonPropertyName("RS")] public string RS { get; set; } = string.Empty;
		[JsonPropertyName("Data")] public List<PoultryDto> Data { get; set; } = new();
		[JsonPropertyName("Next")] public bool Next { get; set; }
	}

	public class PoultryDto
	{
		[JsonPropertyName("Kil_Name")] public string KilName { get; set; } = string.Empty;
		[JsonPropertyName("Kil_Addr")] public string KilAddr { get; set; } = string.Empty;
		[JsonPropertyName("Kil_Boss")] public string KilBoss { get; set; } = string.Empty;
		[JsonPropertyName("Traceno_Start")] public string TracenoStart { get; set; } = string.Empty;
		[JsonPropertyName("Traceno_End")] public string TracenoEnd { get; set; } = string.Empty;
		[JsonPropertyName("FarmersName1")] public string FarmersName1 { get; set; } = string.Empty;
		[JsonPropertyName("FarmersType1")] public string FarmersType1 { get; set; } = string.Empty;
		[JsonPropertyName("Farmersplace1")] public string Farmersplace1 { get; set; } = string.Empty;
		[JsonPropertyName("FarmersName2")] public string FarmersName2 { get; set; } = string.Empty;
		[JsonPropertyName("FarmersType2")] public string FarmersType2 { get; set; } = string.Empty;
		[JsonPropertyName("Farmersplace2")] public string Farmersplace2 { get; set; } = string.Empty;
		[JsonPropertyName("Cdate")] public string Cdate { get; set; } = string.Empty;
	}
}