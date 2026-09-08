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
		public void 分隔符不固定的民國日期解析為西元日期(string input, int year, int month, int day)
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
		public void 分隔符不固定的無效輸入回傳null而不拋例外(string? input)
		{
			// 外部資料每筆都要套用，單筆解析失敗不能中斷整批（欄位級容忍）
			Assert.Null(DateHelper.ParseRocSeparatedDate(input));
		}

		// ── Happy Path ────────────────────────────────────────────────────

		[Fact]
		public void 民國年月日轉換為西元日期()
		{
			// 民國 107 年 7 月 15 日 → 西元 2018/7/15
			var result = DateHelper.ConvertRocRestDay(107, 7, 15);

			Assert.NotNull(result);
			Assert.Equal(new DateOnly(2018, 7, 15), result!.Value);
		}

		[Fact]
		public void 閏年二月二十九日是合法日期()
		{
			// 民國 109 年 = 西元 2020 年（閏年），2/29 合法
			var result = DateHelper.ConvertRocRestDay(109, 2, 29);

			Assert.NotNull(result);
			Assert.Equal(new DateOnly(2020, 2, 29), result!.Value);
		}

		// ── Null Path（非法日期應回傳 null，不拋例外）─────────────────────

		[Fact]
		public void 二月三十日回傳null而不拋例外()
		{
			// 2 月沒有 30 日，任何年份都不合法
			var result = DateHelper.ConvertRocRestDay(107, 2, 30);

			Assert.Null(result);
		}

		[Fact]
		public void 平年二月二十九日回傳null()
		{
			// 民國 94 年 = 西元 2005 年（非閏年），2/29 不存在
			var result = DateHelper.ConvertRocRestDay(94, 2, 29);

			Assert.Null(result);
		}

		[Fact]
		public void 月份十三超出範圍時回傳null()
		{
			// 月份 13 超出範圍
			var result = DateHelper.ConvertRocRestDay(107, 13, 1);

			Assert.Null(result);
		}

		[Fact]
		public void 月份零超出範圍時回傳null()
		{
			// 月份 0 超出範圍
			var result = DateHelper.ConvertRocRestDay(107, 0, 1);

			Assert.Null(result);
		}

		// ── ValidateRange：查詢區間的界限 ────────────────────────────────────

		/// <summary>
		/// 任一端沒有值就不構成可檢查的區間。多數端點的日期是選填的，
		/// 把「沒有指定」當成違規會讓不帶參數的查詢全部回 400
		/// </summary>
		[Theory]
		[InlineData(null, "2026-09-01")]
		[InlineData("2026-09-01", null)]
		[InlineData(null, null)]
		public void 區間任一端未指定時不做檢查(string? start, string? end)
		{
			var result = DateHelper.ValidateRange(
				start == null ? null : DateOnly.Parse(start),
				end == null ? null : DateOnly.Parse(end));

			Assert.Null(result);
		}

		/// <summary>
		/// 起日晚於迄日是使用者操作得出來的狀態（兩個日期選擇器互不知道對方）。
		/// 不擋的話查詢會回空陣列，而空陣列跟「這段期間真的沒資料」看起來一樣
		/// </summary>
		[Fact]
		public void 起日晚於迄日要回傳錯誤訊息()
		{
			var result = DateHelper.ValidateRange(new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 1));

			Assert.Equal("開始日期不得晚於結束日期", result);
		}

		/// <summary>
		/// 同一天是合法區間，長度算一天。算成零天的話「只查今天」會被自己的下限擋掉
		/// </summary>
		[Fact]
		public void 起迄同一天是合法區間()
		{
			Assert.Null(DateHelper.ValidateRange(
				new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 1), maxRangeDays: 1));
		}

		/// <summary>
		/// 上限是閉區間：剛好等於上限要通過，超過一天才擋。
		/// 這條邊界寫錯的症狀是「設定十天卻只能查九天」，而沒有人會為此回報問題
		/// </summary>
		[Fact]
		public void 區間長度剛好等於上限時通過超過一天才擋()
		{
			var start = new DateOnly(2026, 9, 1);

			Assert.Null(DateHelper.ValidateRange(start, new DateOnly(2026, 9, 10), maxRangeDays: 10));
			Assert.NotNull(DateHelper.ValidateRange(start, new DateOnly(2026, 9, 11), maxRangeDays: 10));
		}

		/// <summary>
		/// 錯誤訊息要同時說出上限與實際天數，呼叫端才知道要縮多少。
		/// 只說「超過上限」的話使用者只能亂試
		/// </summary>
		[Fact]
		public void 超過上限的訊息要同時說出上限與實際天數()
		{
			var result = DateHelper.ValidateRange(
				new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 11), maxRangeDays: 10);

			Assert.Contains("10", result);
			Assert.Contains("11", result);
		}

		/// <summary>
		/// 預設上限要容得下前端的預設查詢區間（今天往前一年），
		/// 同時擋得掉「一九〇〇到二一〇〇」這種一次掃全表的參數
		/// </summary>
		[Fact]
		public void 預設上限容得下一年區間但擋得掉兩百年()
		{
			var today = new DateOnly(2026, 9, 8);

			Assert.Null(DateHelper.ValidateRange(today.AddYears(-1), today));
			Assert.NotNull(DateHelper.ValidateRange(new DateOnly(1900, 1, 1), new DateOnly(2100, 1, 1)));
		}
	}
}