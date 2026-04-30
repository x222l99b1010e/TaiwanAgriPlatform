namespace TaiwanAgri.Core.Helpers
{
	public class DateHelper
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
	}
}
