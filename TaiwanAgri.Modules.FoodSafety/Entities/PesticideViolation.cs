using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiwanAgri.Modules.FoodSafety.Entities
{
	public class PesticideViolation
	{
		[Key]
		public int Id { get; set; }
		[MaxLength(100)]
		public string Number { get; set; } = string.Empty;
		public DateOnly SamplingDate { get; set; }
		[MaxLength(100)]
		public string ProductName { get; set; } = string.Empty;
		[Column(TypeName = "nvarchar(max)")]
		public string ProductId { get; set; } = string.Empty;
		[MaxLength(200)]
		public string ProducerName { get; set; } = string.Empty;
		[Column(TypeName = "nvarchar(max)")]
		public string SamplingLocation { get; set; } = string.Empty;
		[MaxLength(50)]
		public string InspectResult { get; set; } = string.Empty;
		[MaxLength(200)]
		public string Note { get; set; } = string.Empty;
		[Column(TypeName = "datetime2")]
		public DateTime SyncedAt { get; set; }
	}
}
