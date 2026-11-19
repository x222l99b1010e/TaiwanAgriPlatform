namespace TaiwanAgri.Modules.Weather.Dtos.ApiResponses
{
	/// <summary>某個縣市被幾則病蟲害警報影響（病蟲害警報頁的縣市燈號用）。縣市一律是正式寫法「臺」</summary>
	public class PestAlertCityCountDto
	{
		public string CityName { get; set; } = string.Empty;
		public int Count { get; set; }
	}
}
