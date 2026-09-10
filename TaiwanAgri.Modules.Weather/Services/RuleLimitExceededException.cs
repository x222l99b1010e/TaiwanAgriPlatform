namespace TaiwanAgri.Modules.Weather.Services
{
	/// <summary>
	/// 建立規則時已達該使用者的規則數上限。
	///
	/// <para>
	/// 為什麼是專屬型別而不是沿用 <see cref="InvalidOperationException"/>：
	/// Controller 要把這一種拒絕翻成 400，而 EF 在儲存階段也可能丟出
	/// <see cref="InvalidOperationException"/>——共用型別的話，那些內部錯誤會被一併翻成
	/// 「已達規則數上限」回給使用者，訊息與真正的原因完全無關，而且沒有任何訊號說它翻錯了。
	/// 專屬型別讓 catch 只接得到這一種。
	/// </para>
	///
	/// <para>
	/// 為什麼不是在請求驗證那一層擋：上限要看該使用者已經有幾條規則，
	/// 那是資料庫狀態，請求本身看不出來。
	/// </para>
	/// </summary>
	public class RuleLimitExceededException : Exception
	{
		public RuleLimitExceededException(string message) : base(message)
		{
		}
	}
}
