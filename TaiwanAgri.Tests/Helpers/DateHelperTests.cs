using TaiwanAgri.Core.Helpers;

namespace TaiwanAgri.Tests.Helpers
{
	public class DateHelperTests
	{
		// ── ParseRocSeparatedDate：分隔符不固定的民國日期（農藥許可證資料用） ──

		[Theory]
		// ExpireDate 用短橫線、RevocationDate 用斜線，同一份資料裡兩種格式並存
		[InlineData("120-02-19", 2031, 2, 19)]
		[InlineData("079/05/03", 1990, 5, 3)]
		[InlineData("105-11-09", 2016, 11, 9)]
		// 相容既有的點分隔格式
		[InlineData("107.07.15", 2018, 7, 15)]
		// 前後有空白仍應解析成功
		[InlineData(" 103/01/01 ", 2014, 1, 1)]
		public void ParseRocSeparatedDate_合法輸入_回傳正確西元日期(string input, int year, int month, int day)
		{
			var result = DateHelper.ParseRocSeparatedDate(input);

			Assert.NotNull(result);
			Assert.Equal(new DateOnly(year, month, day), result);
		}

		[Theory]
		[InlineData("   /  /  ")]   // ★ 許可證資料裡「未廢止」的實際值，不是空字串也不是 null
		[InlineData("")]
		[InlineData("   ")]
		[InlineData(null)]
		[InlineData("120-02")]      // 段數不足
		[InlineData("120-02-19-1")] // 段數過多
		[InlineData("abc-de-fg")]   // 非數字
		[InlineData("000-01-01")]   // 民國 0 年不存在
		[InlineData("107-02-30")]   // 2 月沒有 30 日
		[InlineData("107-13-01")]   // 月份超出範圍
		public void ParseRocSeparatedDate_無法解析的輸入_回傳null且不拋例外(string? input)
		{
			// 外部資料每筆都要套用，單筆解析失敗不能中斷整批（欄位級容忍）
			Assert.Null(DateHelper.ParseRocSeparatedDate(input));
		}

		// ── Happy Path ────────────────────────────────────────────────────

		[Fact]
		public void ConvertRocRestDay_NormalDate_ReturnsCorrectDateOnly()
		{
			// 民國 107 年 7 月 15 日 → 西元 2018/7/15
			var result = DateHelper.ConvertRocRestDay(107, 7, 15);

			Assert.NotNull(result);
			Assert.Equal(new DateOnly(2018, 7, 15), result!.Value);
		}

		[Fact]
		public void ConvertRocRestDay_LeapYearFeb29_ReturnsCorrectDateOnly()
		{
			// 民國 109 年 = 西元 2020 年（閏年），2/29 合法
			var result = DateHelper.ConvertRocRestDay(109, 2, 29);

			Assert.NotNull(result);
			Assert.Equal(new DateOnly(2020, 2, 29), result!.Value);
		}

		// ── Null Path（非法日期應回傳 null，不拋例外）─────────────────────

		[Fact]
		public void ConvertRocRestDay_Feb30_ReturnsNull()
		{
			// 2 月沒有 30 日，任何年份都不合法
			var result = DateHelper.ConvertRocRestDay(107, 2, 30);

			Assert.Null(result);
		}

		[Fact]
		public void ConvertRocRestDay_NonLeapYearFeb29_ReturnsNull()
		{
			// 民國 94 年 = 西元 2005 年（非閏年），2/29 不存在
			var result = DateHelper.ConvertRocRestDay(94, 2, 29);

			Assert.Null(result);
		}

		[Fact]
		public void ConvertRocRestDay_InvalidMonth13_ReturnsNull()
		{
			// 月份 13 超出範圍
			var result = DateHelper.ConvertRocRestDay(107, 13, 1);

			Assert.Null(result);
		}

		[Fact]
		public void ConvertRocRestDay_InvalidMonth0_ReturnsNull()
		{
			// 月份 0 超出範圍
			var result = DateHelper.ConvertRocRestDay(107, 0, 1);

			Assert.Null(result);
		}
	}
}