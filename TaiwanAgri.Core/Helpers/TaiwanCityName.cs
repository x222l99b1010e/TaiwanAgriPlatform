namespace TaiwanAgri.Core.Helpers
{
	/// <summary>
	/// 縣市名稱的「台／臺」。
	/// <para>
	/// 病蟲害警報的來源（農業部）用簡寫「台」（台南市、台中市、台東縣），其餘資料與前端的縣市選單都用正式寫法「臺」。
	/// 全專案以「臺」為準：同步寫入時用 <see cref="Normalize"/> 換掉；比對資料時用 <see cref="Variants"/> 兩種都試——
	/// 2026-11 之前同步進來的警報仍是「台」（沒有回頭改正式資料庫的舊資料，舊列靠兩種都試對上），
	/// 使用者存的規則條件也可能是任一種。
	/// </para>
	/// <para>
	/// 原本同一件事有三種處理：前端送查詢前把「臺」換回「台」、規則引擎兩種都試、警報查詢完全不處理（靠前端繞過）。
	/// 收成這一個類別之後，前端一律送正式寫法（2026-11 總檢查 1-B2）
	/// </para>
	/// </summary>
	public static class TaiwanCityName
	{
		/// <summary>換成正式寫法「臺」</summary>
		public static string Normalize(string cityName) => cityName.Replace('台', '臺');

		/// <summary>
		/// 兩種寫法。候選值先算好再進 Where，翻成 SQL 是 IN (@p0, @p1)；
		/// 若寫成 CityName.Replace(...) 會翻成 SQL 的 REPLACE()，欄位被包在函式裡就用不到索引
		/// </summary>
		public static (string Primary, string Alternate) Variants(string cityName)
			=> (cityName, cityName.Contains('臺') ? cityName.Replace('臺', '台') : cityName.Replace('台', '臺'));
	}
}
