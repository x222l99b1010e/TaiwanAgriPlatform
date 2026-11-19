namespace TaiwanAgri.Core.Helpers
{
	/// <summary>
	/// 存成 UTC 的時間戳（通知的觸發時間、啟事與規則的建立時間這類「系統當下」的時間），
	/// 從 SQL Server 讀回來時 Kind 是 Unspecified：datetime2 不存時區，EF 也不知道它是 UTC。
	/// 這種值序列化成 JSON 不帶結尾的 Z，瀏覽器會當成本地時間解讀，台灣的使用者看到的時間就差 8 小時。
	/// 回應 DTO 的這類欄位一律經過 <see cref="MarkUtc"/> 補上標記。
	///
	/// 不做成全域的 JSON 轉換器：氣象與雨量的 ObservedAt 存的是台灣時間，全域轉換會把那些欄位改錯。
	/// 測試要注意：EF InMemory 會保留寫入時的 Kind，種子資料用 UtcNow 建的話，測試看不出這個差別，
	/// 要用 Kind=Unspecified 的值模擬 SQL Server 讀回來的樣子。
	/// </summary>
	public static class UtcTimestamp
	{
		public static DateTime MarkUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
	}
}
