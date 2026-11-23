namespace TaiwanAgri.Web.Services
{
	/// <summary>
	/// 註冊被 Identity 擋下（密碼不符規則、信箱已經註冊過等）。訊息是寫給使用者看的，Controller 回 400。
	///
	/// <para>
	/// 為什麼是專屬型別而不是沿用 <see cref="InvalidOperationException"/>：框架與資料庫層的內部錯誤
	/// 也常用 <see cref="InvalidOperationException"/>，共用型別的話，那些錯誤的原文會被當成
	/// 「註冊失敗的原因」回給使用者（2026-11 總檢查資安 C5）。專屬型別讓 catch 只接得到這一種，
	/// 其他例外交給 GlobalExceptionMiddleware 回 500。同樣的理由見 Weather 模組的 RuleLimitExceededException
	/// </para>
	/// </summary>
	public class RegistrationRejectedException : Exception
	{
		public RegistrationRejectedException(string message) : base(message)
		{
		}
	}
}
