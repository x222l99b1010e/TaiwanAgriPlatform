using System.ComponentModel.DataAnnotations;

namespace TaiwanAgri.Modules.User.Entities
{
	public class UserWatchlist
	{
		[Key]
		public int Id { get; set; }
		[Required, StringLength(450)]
		public string UserId { get; set; } = string.Empty;
		[Required, StringLength(10)]
		public string CropCode { get; set; } = string.Empty;

		[Required, StringLength(50)]
		public string CropName { get; set; } = string.Empty;
		[Required, MaxLength(20)]
		public string MarketType { get; set; } = string.Empty;

		[StringLength(10)]
		public string? MarketCode { get; set; }

		[StringLength(100)]
		public string? MarketName { get; set; }
	}
}
