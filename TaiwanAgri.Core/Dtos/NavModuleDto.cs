using System;
using System.Collections.Generic;
using System.Text;

namespace TaiwanAgri.Core.Dtos
{
	// TaiwanAgri.Core/Dtos/NavModuleDto.cs
	public class NavModuleDto
	{
		public string Name { get; set; } = string.Empty;
		public string Route { get; set; } = string.Empty;
		public string Icon { get; set; } = string.Empty;
		public int SortOrder { get; set; }
		public List<NavChildDto> Children { get; set; } = new();
	}
}
