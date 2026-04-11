using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiwanAgri.Modules.Weather.Entities
{
	public class RainfallStation
	{
		// ── 識別 ──────────────────────────────────────
		[Key]
		public int Id { get; set; }

		[Required, MaxLength(20)]
		public string StationId { get; set; } = string.Empty;      // Station_ID (Unique Index)

		[Required, MaxLength(100)]
		public string StationName { get; set; } = string.Empty;    // Station_name

		// ── 地理 ──────────────────────────────────────
		[Column(TypeName = "decimal(10,6)")]
		public decimal? Latitude { get; set; }                     // Station_Latitude

		[Column(TypeName = "decimal(10,6)")]
		public decimal? Longitude { get; set; }                    // Station_Longitude

		public int? Elevation { get; set; }                        // ELEV

		// ── 行政區 ────────────────────────────────────
		[Required, MaxLength(50)]
		public string CityName { get; set; } = string.Empty;      // CITY

		[Required, MaxLength(20)]
		public string CityCode { get; set; } = string.Empty;      // CITY_SN

		[MaxLength(50)]
		public string? TownName { get; set; }                      // TOWN

		[MaxLength(20)]
		public string? TownCode { get; set; }                      // TOWN_SN

		// ── 系統 ──────────────────────────────────────
		public bool IsActive { get; set; } = true;
		public DateTime CreatedAt { get; set; }
		public DateTime UpdatedAt { get; set; }
	}
}
