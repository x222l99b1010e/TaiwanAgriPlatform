using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiwanAgri.Modules.Weather.Entities
{
	public class RainfallObservation
	{
		[Key]
		public int Id { get; set; }

		// ── FK ────────────────────────────────────────
		[Required, MaxLength(20)]
		public string StationId { get; set; } = string.Empty;      // 對應 RainfallStation.StationId

		// ── 時間 ──────────────────────────────────────
		public DateTime ObservedAt { get; set; }                   // TIME

		// ── 雨量 ──────────────────────────────────────
		[Column(TypeName = "decimal(6,2)")]
		public decimal? Rain { get; set; }                         // RAIN（當前10分鐘累積）

		[Column(TypeName = "decimal(6,2)")]
		public decimal? Min10 { get; set; }                        // MIN_10

		[Column(TypeName = "decimal(6,2)")]
		public decimal? Hour3 { get; set; }                        // HOUR_3

		[Column(TypeName = "decimal(6,2)")]
		public decimal? Hour6 { get; set; }                        // HOUR_6

		[Column(TypeName = "decimal(6,2)")]
		public decimal? Hour12 { get; set; }                       // HOUR_12

		[Column(TypeName = "decimal(6,2)")]
		public decimal? Hour24 { get; set; }                       // HOUR_24

		[Column(TypeName = "decimal(6,2)")]
		public decimal? Now { get; set; }                          // NOW（當前累積）

		// ── 站台狀態 ──────────────────────────────────
		[MaxLength(50)]
		public string? Attribute { get; set; }                     // ATTRIBUTE

		// ── 系統 ──────────────────────────────────────
		public DateTime SyncedAt { get; set; } = DateTime.UtcNow;
	}
}
