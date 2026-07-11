using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace TaiwanAgri.Core.Helpers
{
	/// <summary>
	/// 同步 Worker 的共用落地流水線：「撈既有業務鍵 → 過濾已存在 → AddRange → SaveChanges」。
	/// 既有鍵查詢由呼叫端提供，可先以日期視窗縮小掃描範圍（見 PesticideViolationSyncWorker）
	/// </summary>
	public static class DbSyncHelper
	{
		/// <summary>
		/// 以業務鍵過濾 DB 既有資料後批次寫入，回傳實際寫入筆數
		/// </summary>
		/// <param name="existingKeys">DB 既有業務鍵查詢（於此方法內才執行）</param>
		/// <param name="incoming">本批待寫入資料（呼叫端已完成批次內去重）</param>
		/// <param name="keySelector">Entity 的業務鍵選擇器，須與 existingKeys 同一欄位</param>
		public static async Task<int> InsertNewByKeyAsync<TEntity, TKey>(
			DbContext db,
			IQueryable<TKey> existingKeys,
			IReadOnlyCollection<TEntity> incoming,
			Func<TEntity, TKey> keySelector,
			ILogger logger,
			string logPrefix,
			CancellationToken stoppingToken) where TEntity : class
		{
			var existing = await existingKeys.ToHashSetAsync(stoppingToken);
			var toInsert = incoming.Where(x => !existing.Contains(keySelector(x))).ToList();

			if (toInsert.Count == 0)
			{
				logger.LogInformation("{LogPrefix} 無新資料需要同步", logPrefix);
				return 0;
			}

			await db.Set<TEntity>().AddRangeAsync(toInsert, stoppingToken);
			await db.SaveChangesAsync(stoppingToken);
			logger.LogInformation("{LogPrefix} 成功同步 {Count} 筆新資料，略過 {Skipped} 筆重複",
				logPrefix, toInsert.Count, incoming.Count - toInsert.Count);
			return toInsert.Count;
		}
	}
}
