using TaiwanAgri.Core.Dtos;

namespace TaiwanAgri.Tests.Web
{
	/// <summary>
	/// PagedResult.Create 的分頁計算。
	/// TotalPages 的公式原本在七個查詢方法裡各抄一次，改一次要找七個地方，
	/// 而漏改的地方不會有任何錯誤訊號、只會讓最後一頁算錯
	/// </summary>
	public class PagedResultTests
	{
		[Theory]
		[InlineData(0, 20, 0)]     // 沒有資料就是 0 頁，不是 1 頁
		[InlineData(1, 20, 1)]
		[InlineData(20, 20, 1)]    // 剛好一頁
		[InlineData(21, 20, 2)]    // 多一筆就要多一頁
		[InlineData(100, 20, 5)]
		[InlineData(101, 20, 6)]
		public void 總頁數計算(int totalCount, int pageSize, int expected)
		{
			var result = PagedResult<string>.Create(new List<string>(), totalCount, 1, pageSize);
			Assert.Equal(expected, result.TotalPages);
		}

		[Fact]
		public void 原樣帶回其餘欄位()
		{
			var items = new List<string> { "a", "b" };
			var result = PagedResult<string>.Create(items, 42, 3, 10);

			Assert.Same(items, result.Items);
			Assert.Equal(42, result.TotalCount);
			Assert.Equal(3, result.Page);
			Assert.Equal(10, result.PageSize);
		}
	}
}
