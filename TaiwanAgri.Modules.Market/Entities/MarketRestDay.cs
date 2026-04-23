using System.ComponentModel.DataAnnotations;

namespace TaiwanAgri.Modules.Market.Entities
{
	public class MarketRestDay
	{
		[Key]
		public int Id { get; set; }
		[Required, MaxLength(15)]
		public string MarketCode { get; set; }
		[Required, MaxLength(15)]
		public string MarketName { get; set; }
		[Required, MaxLength(20)]
		public string MarketType { get; set; }

		public int  Year { get; set; }

		public int Month { get; set; }

		public int RestDay { get; set; }

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	}
}
