namespace TaiwanAgri.Modules.Market.Constants
{
	/// <summary>
	/// 集中管理所有 Redis Cache Key 前綴。
	/// Cache Set 與 Cache Invalidation 必須使用相同前綴，
	/// 禁止在 Service 內散落字串字面值。
	/// </summary>
	public static class CacheKeys
	{
		/// <summary>
		/// 農產品交易價格查詢結果。
		/// 完整格式：market:prices:{marketType}:{sortedCrops}:{marketCode}:{startDate}:{endDate}
		/// </summary>
		public const string MarketPricesPrefix = "market:prices:";
	}
}
