using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace TaiwanAgri.Modules.Weather.Entities
{
	public class PestAlertCity
	{
		public int Id { get; set; }

		public int AlertId { get; set; }

		[MaxLength(50)]
		public string CityName { get; set; } = string.Empty;

		// 導覽屬性：這筆城市記錄屬於哪一個警示
		public PestAlert Alert { get; set; } = null!;
	}
}
