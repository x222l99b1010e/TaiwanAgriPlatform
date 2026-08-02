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

		/// <summary>
		/// 以業務鍵 upsert：鍵存在就逐欄位覆寫既有實體（EF 追蹤下只有真的有變動的欄位才會產生
		/// UPDATE，沒變動的整批安靜跳過），鍵不存在才新增。用在「舊資料的狀態欄位會隨時間變動」
		/// 的情境（如 LegalSpecificPet 的評鑑等級/營業狀態），跟 InsertNewByKeyAsync
		/// 「只新增、鍵存在就整筆丟棄」的語意相反，兩者不可混用。
		/// </summary>
		/// <param name="existingEntities">DB 既有實體查詢（會被追蹤，於此方法內才執行）</param>
		/// <param name="incoming">本批待 upsert 資料（呼叫端已完成批次內去重）</param>
		/// <param name="keySelector">Entity 的業務鍵選擇器，須與 existingEntities 同一欄位</param>
		/// <param name="applyUpdate">欄位覆寫邏輯，參數順序固定為 (existing, incoming)——
		/// existing 是被追蹤的既有實體（要修改的對象），incoming 是這批新抓到的資料（值的來源）</param>
		public static async Task<int> UpsertByKeyAsync<TEntity, TKey>(
			DbContext db,
			IQueryable<TEntity> existingEntities,
			IReadOnlyCollection<TEntity> incoming,
			Func<TEntity, TKey> keySelector,
			Action<TEntity, TEntity> applyUpdate,
			ILogger logger,
			string logPrefix,
			CancellationToken stoppingToken) where TEntity : class where TKey : notnull
		{
			var existingByKey = await existingEntities.ToDictionaryAsync(keySelector, stoppingToken);

			var toInsert = new List<TEntity>();
			var updatedCount = 0;

			foreach (var item in incoming)
			{
				if (existingByKey.TryGetValue(keySelector(item), out var existing))
				{
					applyUpdate(existing, item);
					updatedCount++;
				}
				else
				{
					toInsert.Add(item);
				}
			}

			if (toInsert.Count > 0)
				await db.Set<TEntity>().AddRangeAsync(toInsert, stoppingToken);

			await db.SaveChangesAsync(stoppingToken);
			logger.LogInformation(
				"{LogPrefix} upsert 完成：新增 {Inserted} 筆，比對既有 {Matched} 筆（EF 只會對真正有欄位變動的部分產生 UPDATE）",
				logPrefix, toInsert.Count, updatedCount);
			return toInsert.Count;
		}
	}
}
