using System.ComponentModel.DataAnnotations;

namespace TaiwanAgri.Modules.Pet.Dtos.ApiRequests
{
	/// <summary>
	/// 「可以不填；有填的話必須是 http／https 的絕對網址」。
	/// <para>
	/// 給走失啟事的照片網址用：原本後端完全沒有驗證、資料庫欄位是 nvarchar(max)，
	/// 繞過畫面直接打 API 可以塞幾十 MB 的字串，之後每個人打開清單都會把它傳一次（2026-11 總檢查資安 A4）。
	/// 內建的 [Url] 不能用：它把空字串判成不合法，而這個欄位允許不填。
	/// 跟前端顯示前的判斷（utils/lostPetPost.ts 的 isDisplayableImageUrl）同一個標準
	/// </para>
	/// </summary>
	[AttributeUsage(AttributeTargets.Property)]
	public sealed class OptionalHttpUrlAttribute : ValidationAttribute
	{
		public OptionalHttpUrlAttribute() : base("照片網址必須是 http 或 https 開頭的網址") { }

		public override bool IsValid(object? value)
		{
			if (value is not string s || s.Length == 0) return true;
			return Uri.TryCreate(s, UriKind.Absolute, out var uri)
				&& (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
		}
	}
}
