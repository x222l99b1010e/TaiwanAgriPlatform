using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace TaiwanAgri.Modules.Weather.Entities
{
	public class PestAlertCrop
	{
		public int Id { get; set; }

		public int AlertId { get; set; }

		[MaxLength(100)]
		public string CropName { get; set; } = string.Empty;

		// 導覽屬性：這筆作物記錄屬於哪一個警示
		public PestAlert Alert { get; set; } = null!;
	}
}
