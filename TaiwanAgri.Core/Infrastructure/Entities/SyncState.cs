using System.ComponentModel.DataAnnotations;

namespace TaiwanAgri.Core.Infrastructure.Entities
{
	public class SyncState
	{
		[Key]
		public int Id { get; set; }
		[Required, MaxLength(100)]
		public string SyncKey { get; set; } = string.Empty;
		public DateOnly LastSyncedDate { get; set; }
		public DateTime UpdatedAt { get; set; }

	}
}
