using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace TaiwanAgri.Modules.Weather.Dtos
{
	// 對應 Data 陣列裡的每一筆測站資料
	public class WeatherStationDto
	{
		[JsonPropertyName("Station_name")] public string StationName { get; set; } = string.Empty;
		[JsonPropertyName("Station_ID")] public string StationId { get; set; } = string.Empty;
		[JsonPropertyName("TIME")] public string Time { get; set; } = string.Empty;
		[JsonPropertyName("Station_Latitude")] public string Latitude { get; set; } = string.Empty;
		[JsonPropertyName("Station_Longitude")] public string Longitude { get; set; } = string.Empty;
		[JsonPropertyName("ELEV")] public string Elevation { get; set; } = string.Empty;
		[JsonPropertyName("WDIR")] public string WindDirection { get; set; } = string.Empty;
		[JsonPropertyName("WDSD")] public string WindSpeed { get; set; } = string.Empty;
		[JsonPropertyName("TEMP")] public string Temperature { get; set; } = string.Empty;
		[JsonPropertyName("HUMD")] public string Humidity { get; set; } = string.Empty;
		[JsonPropertyName("PRES")] public string Pressure { get; set; } = string.Empty;
		[JsonPropertyName("SUN")] public string Sunshine { get; set; } = string.Empty;
		[JsonPropertyName("H_24R")] public string Rainfall24h { get; set; } = string.Empty;
		[JsonPropertyName("H_FX")] public string MaxGust { get; set; } = string.Empty;
		[JsonPropertyName("H_XD")] public string MaxGustDirection { get; set; } = string.Empty;
		[JsonPropertyName("D_TX")] public string DailyMaxTemp { get; set; } = string.Empty;
		[JsonPropertyName("D_TN")] public string DailyMinTemp { get; set; } = string.Empty;
		[JsonPropertyName("CITY")] public string CityName { get; set; } = string.Empty;
		[JsonPropertyName("CITY_SN")] public string CityCode { get; set; } = string.Empty;
		[JsonPropertyName("TOWN")] public string TownName { get; set; } = string.Empty;
		[JsonPropertyName("TOWN_SN")] public string TownCode { get; set; } = string.Empty;
	}
}
