namespace TaiwanAgri.Core.Dtos
{
	public class PagedResult<T>
	{
		public List<T> Items { get; set; } = new();
		public int TotalCount { get; set; }
		public int Page { get; set; }
		public int PageSize { get; set; }
		public int TotalPages { get; set; }

		/// <summary>
		/// 由查詢結果組出分頁回應。
		/// 有這支工廠方法之前，七個查詢方法各自手寫一次相同的物件初始化與
		/// TotalPages 的 Math.Ceiling 公式——公式改一次要找七個地方，
		/// 而漏改的那幾處不會有任何錯誤訊號，只會讓最後一頁算錯。
		/// </summary>
		public static PagedResult<T> Create(List<T> items, int totalCount, int page, int pageSize)
			=> new()
			{
				Items = items,
				TotalCount = totalCount,
				Page = page,
				PageSize = pageSize,
				// pageSize 由 PagedQueryDto 的 [Range] 保證 >= 1，這裡不會除以零
				TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
			};
	}
}
