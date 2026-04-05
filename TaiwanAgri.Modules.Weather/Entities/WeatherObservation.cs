using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiwanAgri.Modules.Weather.Entities
{
	public class WeatherObservation
	{
		// ── 識別 ──────────────────────────────────────
		[Key]
		public int Id { get; set; }

		[Required, MaxLength(20)]
		public string StationId { get; set; } = string.Empty;      // Station_ID

		[Required, MaxLength(50)]
		public string StationName { get; set; } = string.Empty;    // Station_name

		// ── 時間 ──────────────────────────────────────
		public DateTime ObservedAt { get; set; }                   // TIME

		// ── 地理 ──────────────────────────────────────
		[Column(TypeName = "decimal(10,6)")]
		public decimal? Latitude { get; set; }                     // Station_Latitude

		[Column(TypeName = "decimal(10,6)")]
		public decimal? Longitude { get; set; }                    // Station_Longitude

		public int? Elevation { get; set; }                        // ELEV

		// ── 風 ────────────────────────────────────────
		[MaxLength(10)]
		public string? WindDirection { get; set; }                 // WDIR

		[Column(TypeName = "decimal(5,2)")]
		public decimal? WindSpeed { get; set; }                    // WDSD

		[Column(TypeName = "decimal(5,2)")]
		public decimal? MaxGust { get; set; }                      // H_FX（nullable：部分站回「儀器校驗中」）

		[MaxLength(10)]
		public string? MaxGustDirection { get; set; }              // H_XD

		// ── 溫濕壓 ────────────────────────────────────
		[Column(TypeName = "decimal(5,2)")]
		public decimal? Temperature { get; set; }                  // TEMP

		[Column(TypeName = "decimal(5,2)")]
		public decimal? Humidity { get; set; }                     // HUMD（規則引擎觸發來源）

		[Column(TypeName = "decimal(7,3)")]
		public decimal? Pressure { get; set; }                     // PRES

		[Column(TypeName = "decimal(5,2)")]
		public decimal? SunshineHours { get; set; }                // SUN

		// ── 雨量 ──────────────────────────────────────
		[Column(TypeName = "decimal(6,2)")]
		public decimal? Rainfall24h { get; set; }                  // H_24R

		// ── 今日最高/最低溫 ───────────────────────────
		[Column(TypeName = "decimal(5,2)")]
		public decimal? DailyMaxTemp { get; set; }                 // D_TX

		[Column(TypeName = "decimal(5,2)")]
		public decimal? DailyMinTemp { get; set; }                 // D_TN

		// ── 行政區 ────────────────────────────────────
		[MaxLength(20)]
		public string CityCode { get; set; } = string.Empty;      // CITY_SN

		[MaxLength(50)]
		public string CityName { get; set; } = string.Empty;      // CITY

		[MaxLength(20)]
		public string? TownCode { get; set; }                      // TOWN_SN

		[MaxLength(50)]
		public string? TownName { get; set; }                      // TOWN

		// ── 系統 ──────────────────────────────────────
		public DateTime SyncedAt { get; set; } = DateTime.UtcNow;
	}
}
