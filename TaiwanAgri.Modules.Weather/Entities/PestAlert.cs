using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace TaiwanAgri.Modules.Weather.Entities
{
	public class PestAlert
	{
		public int Id { get; set; }

		[MaxLength(500)]
		public string Subject { get; set; } = string.Empty;

		public string Body { get; set; } = string.Empty;

		public string Prescription { get; set; } = string.Empty;

		[MaxLength(500)]
		public string CitiesRaw { get; set; } = string.Empty;

		public string PlantNamesRaw { get; set; } = string.Empty;

		public DateOnly PubDate { get; set; }

		[MaxLength(100)]
		public string Issue { get; set; } = string.Empty;

		[MaxLength(64)]
		public string SourceHash { get; set; } = string.Empty;

		[Column(TypeName = "datetime2")]
		public DateTime SyncedAt { get; set; }

		// 導覽屬性：一個警示對應多個城市記錄
		public ICollection<PestAlertCity> Cities { get; set; } = new List<PestAlertCity>();

		// 導覽屬性：一個警示對應多個作物記錄
		public ICollection<PestAlertCrop> Crops { get; set; } = new List<PestAlertCrop>();
	}
}
