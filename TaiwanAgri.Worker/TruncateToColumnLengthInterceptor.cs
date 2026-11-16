using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace TaiwanAgri.Worker
{
	/// <summary>
	/// 同步寫入資料庫之前，把超過欄位長度上限的字串截到上限，並記一則警告。
	/// <para>
	/// 上游的自由文字欄位（備註、地址、內文）偶爾會超長；原本這樣的一筆會讓整批的 SaveChanges 失敗，
	/// 隔天重抓同一批又是同一筆，會每天卡住（2026-11 總檢查 3-B8）。
	/// </para>
	/// <para>
	/// 只掛在 Worker（同步程式）的 DbContext：使用者送來的資料由請求 DTO 的驗證擋下、回 400，
	/// 不該被悄悄截掉。鍵、外鍵與唯一索引裡的欄位不截——截斷可能讓兩筆不同的資料撞成同一個鍵，
	/// 那種情況寧可讓它照原樣失敗、留在記錄裡
	/// </para>
	/// </summary>
	public sealed class TruncateToColumnLengthInterceptor(ILogger<TruncateToColumnLengthInterceptor> logger)
		: SaveChangesInterceptor
	{
		public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
		{
			Truncate(eventData.Context);
			return base.SavingChanges(eventData, result);
		}

		public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
			DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
		{
			Truncate(eventData.Context);
			return base.SavingChangesAsync(eventData, result, cancellationToken);
		}

		internal void Truncate(DbContext? db)
		{
			if (db is null) return;

			foreach (var entry in db.ChangeTracker.Entries())
			{
				if (entry.State is not (EntityState.Added or EntityState.Modified)) continue;

				foreach (var property in entry.Properties)
					TruncateIfTooLong(entry, property);
			}
		}

		private void TruncateIfTooLong(EntityEntry entry, PropertyEntry property)
		{
			var meta = property.Metadata;
			if (meta.ClrType != typeof(string) || meta.GetMaxLength() is not int max) return;
			if (property.CurrentValue is not string value || value.Length <= max) return;

			var entityName = entry.Metadata.ClrType.Name;
			if (meta.IsKey() || meta.IsForeignKey() || meta.GetContainingIndexes().Any(i => i.IsUnique))
			{
				logger.LogWarning(
					"[Sync] {Entity}.{Property} 超過欄位上限 {Max} 字（實際 {Length} 字），但它是鍵或唯一索引的一部分，不截斷",
					entityName, meta.Name, max, value.Length);
				return;
			}

			// 不要從一個「代理對」（表情符號、部分罕用漢字佔兩個 char）中間切開，留下半個字元
			var cut = char.IsHighSurrogate(value[max - 1]) ? max - 1 : max;
			property.CurrentValue = value[..cut];
			logger.LogWarning(
				"[Sync] {Entity}.{Property} 超過欄位上限 {Max} 字（實際 {Length} 字），截斷後寫入",
				entityName, meta.Name, max, value.Length);
		}
	}
}
