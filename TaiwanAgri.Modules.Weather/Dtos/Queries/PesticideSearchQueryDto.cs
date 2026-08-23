using System.Text.RegularExpressions;

namespace TaiwanAgri.Modules.Weather.Dtos.Queries
{
	/// <summary>
	/// 農藥查詢的輸入條件。兩個名稱欄位分開，對應前端兩個獨立的輸入框：
	/// 上游 PesticideDataQueryType 的 ChineseName 與 EnName 是兩個獨立參數、可同時生效（AND），
	/// 硬要用同一個欄位「猜使用者輸入的是中文還是英文」只會在混合輸入時猜錯。
	/// </summary>
	public class PesticideSearchQueryDto
	{
		/// <summary>有效成分俗名（中文），如「亞滅培」。上游是 contains 模糊比對。</summary>
		public string? Keyword { get; set; }

		/// <summary>有效成分英文名，如「ACETAMIPRID」。上游是 contains 模糊比對、不分大小寫。</summary>
		public string? EnglishName { get; set; }

		/// <summary>是否納入已廢止的許可證。預設 false。</summary>
		public bool IncludeRevoked { get; set; }

		/// <summary>
		/// 英文名欄位的合法字元白名單。
		///
		/// 為什麼用白名單而不是「偵測全形／中文就擋」的黑名單：黑名單只擋得住想得到的字元，
		/// 全形英數（Ａ-Ｚ）、CJK、日文假名、emoji、零寬字元、控制字元、各種 Unicode 空白，
		/// 想漏任何一類就會漏過去。白名單反過來只放行明確認可的字元，
		/// 「沒想到的」預設就被擋住，這才是這個防護真正要達到的效果。
		///
		/// 允許的字元取自真實 EnName 值的實際字元集：英數字、空白，以及
		/// 「+」（混合劑，如 THIAMETHOXAM + CHLORANTRANILIPROLE）、
		/// 「-」（如 OXINE-COPPER、metalaxyl-M）、「,」「'」「.」「(」「)」「/」（化學命名用）。
		/// 另外要求至少含一個英文字母，避免「---」「123」這種不可能命中的輸入白跑一趟外部 API。
		/// </summary>
		private static readonly Regex EnglishNamePattern =
			new(@"^(?=.*[A-Za-z])[A-Za-z0-9 +\-,.'()/]+$", RegexOptions.Compiled);

		public static bool IsValidEnglishName(string value) => EnglishNamePattern.IsMatch(value);
	}
}
