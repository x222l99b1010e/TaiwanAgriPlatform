using System.ComponentModel.DataAnnotations;

namespace TaiwanAgri.Modules.Weather.Entities
{
	public class PestDecadeSummary
	{
		[Key]
		public int Id { get; set; }
		[Required,MaxLength(50)]
		public string PestName { get; set; }
		[Required]
		public int Year { get; set; }
		[Required]
		public int Month { get; set; }
		[Required]
		public int TenDays { get; set; }
		[Required, MaxLength(10)]
		public string City { get; set; }
		[Required, MaxLength(10)]
		public string Town { get; set; }
		public decimal? Average { get; set; }
		public decimal? ProportionIsland { get; set; }
		public DateTime CreatedAt { get; set; }

	}
}
