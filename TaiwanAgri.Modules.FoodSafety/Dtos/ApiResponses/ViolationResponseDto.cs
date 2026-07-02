using System;
using System.Collections.Generic;
using System.Text;

namespace TaiwanAgri.Modules.FoodSafety.Dtos.ApiResponses
{
	public class ViolationResponseDto
	{
		public string Number { get; set; } = string.Empty;
		public DateOnly SamplingDate { get; set; }
		public string ProductName { get; set; } = string.Empty;
		public string ProducerName { get; set; } = string.Empty;
		public string SamplingLocation { get; set; } = string.Empty;
		public string InspectResult { get; set; } = string.Empty;
		public string Note { get; set; } = string.Empty;
	}
}
