using System;
using System.Collections.Generic;
using System.Text;

namespace TaiwanAgri.Modules.Weather.Dtos.ApiResponses
{
	public class RainfallResponseDto
	{
		public string StationName { get; set; } = string.Empty;
		public string CityName { get; set; } = string.Empty;
		public DateTime ObservedAt { get; set; }
		public decimal? Hour3 { get; set; }
		public decimal? Hour6 { get; set; }
		public decimal? Hour12 { get; set; }
		public decimal? Hour24 { get; set; }
	}
}
