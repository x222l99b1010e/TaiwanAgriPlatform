using System;
using System.Collections.Generic;
using System.Text;

namespace TaiwanAgri.Modules.Weather.Dtos.ApiResponses
{
	public class PestDecadeSummaryResponseDto
	{
		public string PestName { get; set; } = string.Empty;
		public int Year { get; set; }
		public int Month { get; set; }
		public int TenDays { get; set; }
		public string City { get; set; } = string.Empty;
		public string Town { get; set; } = string.Empty;
		public decimal? Average { get; set; }
		public decimal? ProportionIsland { get; set; }
	}
}
