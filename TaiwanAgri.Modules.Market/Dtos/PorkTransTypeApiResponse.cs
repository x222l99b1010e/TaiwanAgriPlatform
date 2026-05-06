using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace TaiwanAgri.Modules.Market.Dtos
{
	public class PorkTransTypeApiResponse
	{
		[JsonPropertyName("RS")]
		public string RS { get; set; } = string.Empty;
		[JsonPropertyName("Data")]
		public List<PorkTransTypeDto> Data { get; set; } = new();
		[JsonPropertyName("Next")]
		public bool Next { get; set; }
	}
}
