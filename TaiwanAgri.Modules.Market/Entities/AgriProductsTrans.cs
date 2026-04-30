using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiwanAgri.Modules.Market.Entities
{
	public class AgriProductsTrans
	{
		[Key]
		public int Id { get; set; }
		public DateOnly TransDate { get; set; }
		[Required, MaxLength(20)]
		public string TcType { get; set; } = string.Empty;
		[Required, MaxLength(20)]
		public string CropCode { get; set; } = string.Empty;
		[Required, MaxLength(20)]
		public string MarketCode { get; set; } = string.Empty;
		public decimal UpperPrice { get; set; }
		public decimal MiddlePrice { get; set; }
		public decimal LowerPrice { get; set; }
		public decimal AvgPrice { get; set ; }
		public decimal TransQuantity { get; set; }
		public DateTime CreatedAt { get; set; }	= DateTime.UtcNow;
	}
}
