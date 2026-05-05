using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace TaiwanAgri.Modules.Market.Entities
{
	public class DebrisAlertRecord
	{
		public int Id { get; set; }
		[MaxLength(200)]
		public string DisasterID { get; set; } = string.Empty;
		[MaxLength(200)]
		public string DisasterName { get; set; } = string.Empty;
		[MaxLength(200)]
		public string AlertType { get; set; } = string.Empty;
		[MaxLength(200)]
		public string? DebrisNo { get; set; }
		[MaxLength(200)]
		public string? LandslideID { get; set; }
		[MaxLength(200)]
		public string? LandslideName { get; set; }
		[MaxLength(200)]
		public string County { get; set; } = string.Empty;
		[MaxLength(200)]
		public string Town { get; set; } = string.Empty;
		[MaxLength(200)]
		public string? Vill { get; set; }
		[MaxLength(200)]
		public string AlertLevel { get; set; } = string.Empty;
		public DateTime LastUpdateDate { get; set; }
		[MaxLength(300)]
		public string ReportID { get; set; } = string.Empty;
		[MaxLength(200)]
		public string? CountyCode { get; set; }
		[MaxLength(200)]
		public string? AreaCode { get; set; }
		public DateTime CreatedAt { get; set; }
	}
}
