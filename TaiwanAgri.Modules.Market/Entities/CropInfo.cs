using System.ComponentModel.DataAnnotations;

namespace TaiwanAgri.Modules.Market.Entities
{
	public class CropInfo
	{
		[Key, MaxLength(20)]
		public string CropCode { get; set; } = string.Empty;
		[Required, MaxLength(100)]
		public string CropName { get; set; } = string.Empty;
		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
		public DateTime UpdatedAt { get; set; }

	}
}
