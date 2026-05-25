using System;
using System.Collections.Generic;
using System.Text;

namespace TaiwanAgri.Modules.Weather.Dtos.ApiResponses
{
	public class PestAlertResponseDto
	{
		public int Id { get; set; }
		public string Subject { get; set; } = string.Empty;
		public string Body { get; set; } = string.Empty;
		public string Prescription { get; set; } = string.Empty;
		public DateOnly PubDate { get; set; }
		public string Issue { get; set; } = string.Empty;
		public List<string> Cities { get; set; } = new();
		public List<string> Crops { get; set; } = new();
	}
}
