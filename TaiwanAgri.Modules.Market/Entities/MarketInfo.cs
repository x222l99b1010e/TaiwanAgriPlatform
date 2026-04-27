using System.ComponentModel.DataAnnotations;

namespace TaiwanAgri.Modules.Market.Entities
{
	public class MarketInfo
	{
		[Key]
		public int Id { get; set; }
		[Required, MaxLength(20)]
		public string MarketCode { get; set; } = string.Empty;
		[Required, MaxLength(50)]
		public string MarketName { get; set; } = string.Empty;
		[Required, MaxLength(20)]
		public string MarketType { get; set; } = string.Empty;
		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
		public DateTime UpdatedAt { get; set; }
	}
}
