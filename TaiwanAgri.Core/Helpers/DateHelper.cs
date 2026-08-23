using System.Globalization;

namespace TaiwanAgri.Core.Helpers
{
	public static class DateHelper
	{
		/// <summary>
		/// 解析「點分隔民國日期字串」，回傳西元 DateOnly。
		/// 輸入："107.07.15"　→　輸出：DateOnly(2018, 7, 15)
		/// 輸入："107.7.5"　　→　輸出：DateOnly(2018, 7, 5)
		/// 輸入格式錯誤（非三段、非數字）→　拋出 FormatException
		/// </summary>
		/// <param name="input"></param>
		/// <returns></returns>
		/// <exception cref="FormatException"></exception>
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
		/// <summary>
		/// 將西元 DateOnly 格式化為「點分隔民國日期字串」。
		/// 輸入：DateOnly(2018, 7, 15)　→　輸出："107.07.15"
		/// 輸入：DateOnly(2009, 1, 5)　 →　輸出："098.01.05"
		/// </summary>
		/// <param name="date"></param>
		/// <returns></returns>
		public static string FormatRocDate(DateOnly date)
		{
			int rocYear = date.Year - 1911;
			return $"{rocYear}.{date.Month:D2}.{date.Day:D2}";
		}
		/// <summary>
		/// 解析「七位數字民國日期字串（YYYMMDD）」，回傳西元 DateOnly。
		/// 輸入："1070715"　→　輸出：DateOnly(2018, 7, 15)
		/// 輸入："0980105"　→　輸出：DateOnly(2009, 1, 5)
		/// 輸入格式錯誤（非七位數字、月份超範圍、日期超出當月天數）→　拋出 ArgumentException
		/// </summary>
		/// <param name="inputDate"></param>
		/// <returns></returns>
		/// <exception cref="ArgumentException"></exception>
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
		/// <summary>
		/// 將西元 DateOnly 格式化為「七位數字民國日期字串（YYYMMDD）」。
		/// 輸入：DateOnly(2018, 7, 15)　→　輸出："1070715"
		/// 輸入：DateOnly(2009, 1, 5)　 →　輸出："0980105"
		/// </summary>
		/// <param name="inputDate"></param>
		/// <returns></returns>
		public static string ToRocNumericDate(this DateOnly inputDate)
		{
			// 民國年 = 西元年 - 1911
        int rocYear = inputDate.Year - 1911;
        
        // 使用字串插值與格式化：
        // rocYear.ToString("D3") 確保年份不足三位時補 0 (例如民國 98 年變成 098)
        // MM 與 dd 確保月日固定兩位
        return $"{rocYear:D3}{inputDate.Month:D2}{inputDate.Day:D2}";
		}
		/// <summary>
		/// 解析「ISO 8601 日期字串（yyyy-MM-dd）」，回傳西元 DateOnly；格式不符回傳 null。
		/// 輸入："2018-07-15"　→　輸出：DateOnly(2018, 7, 15)
		/// 輸入：null / ""　  →　輸出：null
		/// 輸入："107.07.15"　→　輸出：null（格式不符，不拋例外）
		/// </summary>
		/// <param name="input"></param>
		/// <returns></returns>
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
		/// <summary>
		/// 解析「分隔符不固定的民國日期字串」，回傳西元 DateOnly；任何無法解析的輸入一律回傳 null，不拋例外。
		/// 接受 '.'、'-'、'/' 三種分隔符。
		/// 輸入："120-02-19"　→　輸出：DateOnly(2031, 2, 19)
		/// 輸入："079/05/03"　→　輸出：DateOnly(1990, 5, 3)
		/// 輸入："   /  /  " / null / ""　→　輸出：null
		///
		/// 為什麼需要這支而不沿用 ParseRocDate：農藥許可證資料（PesticideDataQueryType）的兩個日期欄位
		/// 分隔符不一致——ExpireDate 用短橫線（120-02-19）、RevocationDate 用斜線（079/05/03）——
		/// 且 RevocationDate 的「無值」不是空字串或 null，而是含空白的 "   /  /  "。
		/// ParseRocDate 只吃 '.' 且格式不符就拋 FormatException，外部資料每筆都套用會直接中斷整批。
		/// 這裡採「單筆無法解析就當作沒有這個日期」的欄位級容忍策略（比照 §12.35.4）。
		/// </summary>
		public static DateOnly? ParseRocSeparatedDate(string? input)
		{
			if (string.IsNullOrWhiteSpace(input)) return null;

			string[] parts = input.Split('.', '-', '/');
			if (parts.Length != 3) return null;

			if (!int.TryParse(parts[0].Trim(), out int rocYear) ||
				!int.TryParse(parts[1].Trim(), out int month) ||
				!int.TryParse(parts[2].Trim(), out int day))
			{
				return null;
			}

			// 民國年必須為正數：西元年 = 民國年 + 1911，rocYear <= 0 代表資料本身有問題
			if (rocYear <= 0) return null;

			try { return new DateOnly(rocYear + 1911, month, day); }
			catch { return null; }
		}

		/// <summary>
		/// 將民國年、月、日三個整數轉換為西元 DateOnly；日期無效時回傳 null，不拋例外。
		/// 輸入：(107, 7, 15)　→　輸出：DateOnly(2018, 7, 15)
		/// 輸入：(107, 2, 30)　→　輸出：null（2 月沒有 30 日）
		/// 輸入：(107, 13, 1)　→　輸出：null（月份超出範圍）
		/// </summary>
		/// <param name="rocYear"></param>
		/// <param name="month"></param>
		/// <param name="day"></param>
		/// <returns></returns>
		public static DateOnly? ConvertRocRestDay(int rocYear, int month, int day)
		{
			try { return new DateOnly(rocYear + 1911, month, day); }
			catch { return null; }
		}
	}
}
