using System.Globalization;

namespace TaiwanAgri.Core.Helpers
{
	public static class DateHelper
	{
		public static DateOnly ParseRocDate(string input)
		{
			// 1. 使用 Split 拆分
			string[] dateParts = input.Split('.');
			// 2. 基本長度檢查，避免輸入只有 "107.07" 導致索引報錯
			if (dateParts.Length != 3)
			{
				throw new FormatException("輸入的日期格式不正確，'YYY.MM.DD'");
			}
			// 3. 解析年、月、日
			if (int.TryParse(dateParts[0], out int year) &&
				int.TryParse(dateParts[1], out int month) &&
				int.TryParse(dateParts[2], out int day))
			{
				// 4. 將民國年轉換為西元年
				year += 1911;
				return new DateOnly(year, month, day);
			}
			else
			{
				throw new FormatException("日期數字解析失敗'");
			}
		}

		public static string FormatRocDate(DateOnly date)
		{
			int rocYear = date.Year - 1911;
			return $"{rocYear}.{date.Month:D2}.{date.Day:D2}";
		}

		public static DateOnly ParseRocNumericDate(string inputDate) 
		{
			// 1. 基本格式檢查：不可為空、長度必須為 7、必須全為數字
			if (string.IsNullOrWhiteSpace(inputDate) || inputDate.Length != 7 || !inputDate.All(char.IsDigit))
			{
				throw new ArgumentException($"日期格式錯誤: '{inputDate}'，須為 7 位數字。");
			}
			// 2. 切片並轉換西元年
			// 使用 int.Parse 是安全的，因為前面已經用 .All(char.IsDigit) 驗證過
			int adYear = int.Parse(inputDate[0..3]) + 1911;
			int month = int.Parse(inputDate[3..5]);
			int day = int.Parse(inputDate[5..7]);
			// 3. 利用內建方法進行最終驗證與轉換
			// 使用格式化字串與 InvariantCulture 確保解析邏輯在任何系統語系下都一致
			string isoDate = $"{adYear:D4}-{month:D2}-{day:D2}";

			if (DateOnly.TryParseExact(isoDate, "yyyy-MM-dd",
							   CultureInfo.InvariantCulture,
							   DateTimeStyles.None,
							   out DateOnly result))
			{
				return result;
			}
			// 4. 解析失敗：代表月份 1-12 以外，或日期超出該月天數（含閏年判斷）
			throw new ArgumentException($"無效的日期內容: '{inputDate}' (轉換後為 {isoDate})。");
		}

		public static string ToRocNumericDate(this DateOnly inputDate)
		{
			// 民國年 = 西元年 - 1911
        int rocYear = inputDate.Year - 1911;
        
        // 使用字串插值與格式化：
        // rocYear.ToString("D3") 確保年份不足三位時補 0 (例如民國 98 年變成 098)
        // MM 與 dd 確保月日固定兩位
        return $"{rocYear:D3}{inputDate.Month:D2}{inputDate.Day:D2}";
		}

		public static DateOnly? ParseIsoDate(string? input)
		{
			if (string.IsNullOrWhiteSpace(input)) return null;

			if (DateOnly.TryParseExact(input, "yyyy-MM-dd",
				CultureInfo.InvariantCulture,
				DateTimeStyles.None,
				out var result))
			{
				return result;
			}

			return null;
		}
	}
}
