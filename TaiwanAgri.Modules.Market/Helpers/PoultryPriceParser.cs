using System.Globalization;
using System.Text.RegularExpressions;
using TaiwanAgri.Modules.Market.Entities.Enums;

namespace TaiwanAgri.Modules.Market.Helpers
{
	/// <summary>
	/// 家禽行情原始價格字串 → (Price, PriceStatus, RawValue) 的解析規則，
	/// 是 PriceStatus 語意的單一實作處。抽成獨立類別而非寫在 Worker 裡，
	/// 是因為這段是整支同步流程裡分支最多、最需要單元測試涵蓋的邏輯。
	/// </summary>
	public static class PoultryPriceParser
	{
		/// <summary>
		/// 區間報價樣式（如「41-42」「40.5-41」）。刻意寫得嚴格：只認「數字-數字」，
		/// 不用寬鬆的「含有減號就算區間」，否則單獨的「-」與未來可能出現的其他寫法
		/// 會被誤判成區間，反而讓 Unrecognized 失去示警作用
		/// </summary>
		private static readonly Regex RangeQuotePattern =
			new(@"^\d+(\.\d+)?\s*-\s*\d+(\.\d+)?$", RegexOptions.Compiled);

		/// <summary>
		/// 解析單一價格儲存格。回傳的 RawValue 遵守 PoultryTrans 的不變式：
		/// 成功解析為數字時是 null，其餘狀態一律是去頭尾空白後的原始字串
		/// </summary>
		public static (decimal? Price, PriceStatus Status, string? RawValue) Parse(string? raw)
		{
			var value = raw?.Trim() ?? string.Empty;

			// 先試數字：這一步是「用解析成功與否判斷」而不是「用字串長相判斷」，
			// 所以任何沒預期到的非數值寫法都會安全落到下面的分類，不會拋例外中斷整批同步
			if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var price))
				return (price, PriceStatus.Normal, null);

			var status = value switch
			{
				"" => PriceStatus.Empty,
				"休市" => PriceStatus.Closed,
				"-" => PriceStatus.NotQuoted,
				"議價" => PriceStatus.Negotiated,
				_ when RangeQuotePattern.IsMatch(value) => PriceStatus.RangeQuote,
				_ => PriceStatus.Unrecognized
			};

			return (null, status, value);
		}
	}
}
