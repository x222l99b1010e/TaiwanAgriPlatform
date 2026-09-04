using TaiwanAgri.Worker.Pet;
using Xunit;

namespace TaiwanAgri.Tests.Worker
{
	public class LegalSpecificPetSyncWorkerTests
	{
		// ===== ParseValidDate =====
		// 這支解析邏輯沒有正式規格，格式字串是照官方文件裡的範例字串反推的，
		// 一旦真實資料出現這裡沒設想到的變體會直接炸掉整輪回填（不像 enum fallback
		// 只是分類不準），值得釘住現況。

		[Fact]
		public void ParseValidDate_正常格式上午_解析出正確日期()
		{
			var result = LegalSpecificPetSyncWorker.ParseValidDate("2028/3/12 上午 12:00:00");

			Assert.Equal(new DateOnly(2028, 3, 12), result);
		}

		[Fact]
		public void ParseValidDate_正常格式下午_解析出正確日期()
		{
			var result = LegalSpecificPetSyncWorker.ParseValidDate("2025/12/1 下午 12:00:00");

			Assert.Equal(new DateOnly(2025, 12, 1), result);
		}

		[Fact]
		public void ParseValidDate_月日皆為兩位數_解析出正確日期()
		{
			var result = LegalSpecificPetSyncWorker.ParseValidDate("2030/11/25 上午 12:00:00");

			Assert.Equal(new DateOnly(2030, 11, 25), result);
		}

		[Fact]
		public void ParseValidDate_空字串_回傳null()
		{
			var result = LegalSpecificPetSyncWorker.ParseValidDate("");

			Assert.Null(result);
		}

		[Fact]
		public void ParseValidDate_空白字串_回傳null()
		{
			var result = LegalSpecificPetSyncWorker.ParseValidDate("   ");

			Assert.Null(result);
		}
	}
}
