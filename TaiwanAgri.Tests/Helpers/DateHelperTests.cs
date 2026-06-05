using TaiwanAgri.Core.Helpers;

namespace TaiwanAgri.Tests.Helpers
{
	public class DateHelperTests
	{
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