using TaiwanAgri.Worker;
using Xunit;

namespace TaiwanAgri.Tests.Worker
{
	/// <summary>
	/// 日期游標型 Worker 每一輪的抓取區間。
	/// <para>
	/// 這一組釘的是「游標推過一個當時還沒公布的日子之後，下一輪仍會回頭抓」。
	/// 少了這一層，晚公布的那天會被當成無資料永久跳過，而且結束碼、摘要、同步進度表全都看起來正常
	/// </para>
	/// </summary>
	public class SyncWindowTests
	{
		private static readonly DateOnly Yesterday = new(2026, 10, 3);

		// ===== 從哪天開始抓 =====

		[Fact]
		public void 起點_游標落後超過重掃天數_從游標隔天開始()
		{
			// 回填中：落後二十幾天，只抓最近七天會漏掉中間那段
			var start = SyncWindow.StartDate(new DateOnly(2026, 9, 10), Yesterday);

			Assert.Equal(new DateOnly(2026, 9, 11), start);
		}

		[Fact]
		public void 起點_游標已追到昨天_仍往回重掃七天()
		{
			var start = SyncWindow.StartDate(Yesterday, Yesterday);

			// 含昨天共七天：9/27～10/3
			Assert.Equal(new DateOnly(2026, 9, 27), start);
		}

		[Theory]
		[InlineData("2026-09-25", "2026-09-26")] // 游標隔天比重掃起點早：取游標隔天
		[InlineData("2026-09-26", "2026-09-27")] // 游標隔天剛好是重掃起點
		[InlineData("2026-09-27", "2026-09-27")] // 游標隔天比重掃起點晚：取重掃起點
		public void 起點_取游標隔天與重掃起點較早的那天(string lastSynced, string expected)
		{
			var start = SyncWindow.StartDate(DateOnly.Parse(lastSynced), Yesterday);

			Assert.Equal(DateOnly.Parse(expected), start);
		}

		[Fact]
		public void 起點_游標已推過還沒公布的日子_下一輪仍涵蓋那天()
		{
			// 10/2 那天抓的時候還沒公布、被當成無資料，游標照樣推到 10/3；
			// 隔天那輪（昨天＝10/4）一定要回頭抓到 10/2
			var start = SyncWindow.StartDate(new DateOnly(2026, 10, 3), new DateOnly(2026, 10, 4));

			Assert.True(start <= new DateOnly(2026, 10, 2));
		}

		// ===== 游標怎麼推進 =====

		[Fact]
		public void 推進_處理的日子比游標舊_游標不往回()
		{
			// 重掃到 9/28 時游標已在 10/3，照寫會把游標拉回 9/28
			var cursor = SyncWindow.Advance(Yesterday, new DateOnly(2026, 9, 28));

			Assert.Equal(Yesterday, cursor);
		}

		[Fact]
		public void 推進_處理的日子比游標新_游標前進到那天()
		{
			var cursor = SyncWindow.Advance(new DateOnly(2026, 10, 2), Yesterday);

			Assert.Equal(Yesterday, cursor);
		}
	}
}
