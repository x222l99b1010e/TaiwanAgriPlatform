namespace TaiwanAgri.Worker
{
	/// <summary>
	/// 把上游的 DTO 逐筆轉成 Entity；某一筆格式不對（例如日期解析失敗）時記一則警告、略過那一筆，其餘照常落地。
	/// <para>
	/// 原本是 <c>.Select(MapToEntity)</c>：一筆日期格式不對就丟例外，整批（那一天、那個縣市）都寫不進去，
	/// 隔天從同一個位置重抓、又是同一筆，會每天卡住（2026-11 總檢查 3-B8）。
	/// 只接資料格式造成的例外（FormatException，以及欄位整個缺值的 ArgumentNullException）；
	/// 其他例外代表程式本身有問題，照樣往上拋，讓那一輪失敗、留在記錄裡
	/// </para>
	/// </summary>
	internal static class SyncMapping
	{
		public static List<TEntity> MapSkippingMalformed<TDto, TEntity>(
			IEnumerable<TDto> dtos,
			Func<TDto, TEntity> map,
			Func<TDto, string?> describe,
			ILogger logger,
			string logPrefix)
		{
			var result = new List<TEntity>();
			var skipped = 0;
			foreach (var dto in dtos)
			{
				try
				{
					result.Add(map(dto));
				}
				catch (Exception ex) when (ex is FormatException or ArgumentNullException)
				{
					skipped++;
					logger.LogWarning(ex, "{LogPrefix} 這一筆的資料格式不對，略過、其餘照常寫入：{Record}",
						logPrefix, describe(dto));
				}
			}

			if (skipped > 0)
				logger.LogWarning("{LogPrefix} 本批共略過 {Skipped} 筆格式不對的資料", logPrefix, skipped);
			return result;
		}
	}
}
