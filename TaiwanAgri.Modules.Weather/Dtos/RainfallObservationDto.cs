using System.Text.Json.Serialization;

namespace TaiwanAgri.Modules.Weather.Dtos
{
	public class RainfallObservationDto
	{
		[JsonPropertyName("Station_ID")]
		public string StationId { get; set; } = string.Empty;
		[JsonPropertyName("Station_name")]
		public string StationName { get; set; } = string.Empty;
		[JsonPropertyName("TIME")]
		public string Time { get; set; } = string.Empty;
		[JsonPropertyName("LAT")]
		public string Latitude { get; set; } = string.Empty;
		[JsonPropertyName("LON")]
		public string Longitude { get; set; } = string.Empty;
		[JsonPropertyName("ELEV")]
		public string Elevation { get; set; } = string.Empty;
		[JsonPropertyName("RAIN")]
		public string Rain { get; set; } = string.Empty;
		[JsonPropertyName("MIN_10")]
		public string Min10 { get; set; } = string.Empty;
		[JsonPropertyName("HOUR_3")]
		public string Hour3 { get; set; } = string.Empty;
		[JsonPropertyName("HOUR_6")]
		public string Hour6 { get; set; } = string.Empty;
		[JsonPropertyName("HOUR_12")]
		public string Hour12 { get; set; } = string.Empty;
		[JsonPropertyName("HOUR_24")]
		public string Hour24 { get; set; } = string.Empty;
		[JsonPropertyName("NOW")]
		public string NowTotal { get; set; } = string.Empty;
		[JsonPropertyName("ATTRIBUTE")]
		public string Attribute { get; set; } = string.Empty;
	}
}
