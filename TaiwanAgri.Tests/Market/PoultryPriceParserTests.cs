using TaiwanAgri.Modules.Market.Entities.Enums;
using TaiwanAgri.Modules.Market.Helpers;

namespace TaiwanAgri.Tests.Market
{
	/// <summary>
	/// PoultryPriceParser 的分類規則測試。
	/// 每個非數值案例的輸入都取自 2026-08-24 對四支家禽 API 全歷史
	/// （2010/10/07 起、88236 個價格儲存格）的實測窮舉結果，不是臆造的邊界值。
	/// </summary>
	public class PoultryPriceParserTests
	{
		[Theory]
		[InlineData("34.0", 34.0)]
		[InlineData("67.0", 67.0)]
		[InlineData("52.8", 52.8)]
		[InlineData("39.8", 39.8)]
		[InlineData("100", 100)]
		public void Parse_數字字串_回傳Normal且RawValue為null(string raw, decimal expected)
		{
			var (price, status, rawValue) = PoultryPriceParser.Parse(raw);

			Assert.Equal(expected, price);
			Assert.Equal(PriceStatus.Normal, status);
			Assert.Null(rawValue);
		}

		[Theory]
		[InlineData("", PriceStatus.Empty)]              // 實測 5426 次，幾乎全在雞蛋產地價
		[InlineData("休市", PriceStatus.Closed)]          // 實測 3585 次，四支來源都有
		[InlineData("-", PriceStatus.NotQuoted)]          // 實測 3353 次，幾乎全在紅羽土雞南區
		[InlineData("議價", PriceStatus.Negotiated)]      // 實測 7 次，只出現在雞蛋大運輸價
		[InlineData("41-42", PriceStatus.RangeQuote)]     // 實測 36 次
		[InlineData("40-41", PriceStatus.RangeQuote)]     // 實測 22 次
		[InlineData("42-43", PriceStatus.RangeQuote)]     // 實測 12 次
		[InlineData("31..8", PriceStatus.Unrecognized)]   // 實測 1 次，31.8 的鍵入錯誤
		public void Parse_非數值字串_分類正確且Price為null(string raw, PriceStatus expected)
		{
			var (price, status, _) = PoultryPriceParser.Parse(raw);

			Assert.Null(price);
			Assert.Equal(expected, status);
		}

		[Theory]
		[InlineData("休市")]
		[InlineData("-")]
		[InlineData("議價")]
		[InlineData("41-42")]
		[InlineData("31..8")]
		public void Parse_非數值字串_原始值一律留在RawValue(string raw)
		{
			// PoultryTrans 的不變式：Normal 以外的狀態都保留原文，
			// 讓 RangeQuote 的區間資訊與 Unrecognized 的未知寫法都不會遺失
			var (_, _, rawValue) = PoultryPriceParser.Parse(raw);

			Assert.Equal(raw, rawValue);
		}

		[Fact]
		public void Parse_null輸入_視為Empty不拋例外()
		{
			// DTO 的價格屬性是 string?，來源若整個欄位缺漏會是 null 而非空字串
			var (price, status, rawValue) = PoultryPriceParser.Parse(null);

			Assert.Null(price);
			Assert.Equal(PriceStatus.Empty, status);
			Assert.Equal(string.Empty, rawValue);
		}

		[Theory]
		[InlineData("  34.0  ", 34.0)]
		[InlineData(" 休市 ", null)]
		public void Parse_前後空白_先去除再判斷(string raw, double? expectedPrice)
		{
			var (price, status, _) = PoultryPriceParser.Parse(raw);

			if (expectedPrice is null)
			{
				Assert.Null(price);
				Assert.Equal(PriceStatus.Closed, status);
			}
			else
			{
				Assert.Equal((decimal)expectedPrice.Value, price);
				Assert.Equal(PriceStatus.Normal, status);
			}
		}

		[Theory]
		[InlineData("40.5-41.5")]  // 帶小數的區間
		[InlineData("40 - 41")]    // 減號兩側有空白
		public void Parse_區間報價的其他合理寫法_一併認得(string raw)
		{
			var (_, status, _) = PoultryPriceParser.Parse(raw);

			Assert.Equal(PriceStatus.RangeQuote, status);
		}

		[Theory]
		[InlineData("暫不報價")]
		[InlineData("N/A")]
		[InlineData("40~41")]      // 波浪號不是已知的區間寫法
		[InlineData("--")]
		public void Parse_未知寫法_落入Unrecognized而非誤判成其他狀態(string raw)
		{
			// Unrecognized 的作用是示警：規則寫得嚴格，寧可讓沒見過的寫法浮上來，
			// 也不要用寬鬆比對把它悄悄歸進某個已知狀態
			var (price, status, rawValue) = PoultryPriceParser.Parse(raw);

			Assert.Null(price);
			Assert.Equal(PriceStatus.Unrecognized, status);
			Assert.Equal(raw, rawValue);
		}
	}
}
