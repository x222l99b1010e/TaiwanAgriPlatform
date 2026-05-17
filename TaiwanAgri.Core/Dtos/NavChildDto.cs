using System;
using System.Collections.Generic;
using System.Text;

namespace TaiwanAgri.Core.Dtos
{
	// TaiwanAgri.Core/Dtos/NavChildDto.cs
	public class NavChildDto
	{
		public string Name { get; set; } = string.Empty;
		public string Route { get; set; } = string.Empty;
		public string Icon { get; set; } = string.Empty;
		public int SortOrder { get; set; }
	}
}
