using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace TaiwanAgri.Modules.Market.Dtos
{
	public class DebrisAlertRecordDto
	{
		[JsonPropertyName("DisasterID")]
		public string DisasterID { get; set; } = string.Empty;
		[JsonPropertyName("DisasterName")]
		public string DisasterName { get; set; } = string.Empty;
		[JsonPropertyName("AlertType")]
		public string AlertType { get; set; } = string.Empty;
		[JsonPropertyName("DebrisNo")]
		public string DebrisNo { get; set; } = string.Empty;
		[JsonPropertyName("LandslideID")]
		public string LandslideID { get; set; } = string.Empty;
		[JsonPropertyName("LandslideName")]
		public string LandslideName { get; set; } = string.Empty;
		[JsonPropertyName("County")]
		public string County { get; set; } = string.Empty;
		[JsonPropertyName("Town")]
		public string Town { get; set; } = string.Empty;
		[JsonPropertyName("Vill")]
		public string Vill { get; set; } = string.Empty;
		[JsonPropertyName("AlertLevel")]
		public string AlertLevel { get; set; } = string.Empty;
		[JsonPropertyName("LastUpdateDate")]
		public string LastUpdateDate { get; set; } = string.Empty;
		[JsonPropertyName("ReportID")]
		public string ReportID { get; set; } = string.Empty;
		[JsonPropertyName("CountyCode")]
		public string CountyCode { get; set; } = string.Empty;
		[JsonPropertyName("AreaCode")]
		public string AreaCode { get; set; } = string.Empty;
	}
}
