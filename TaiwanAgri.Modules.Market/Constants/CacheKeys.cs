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
		/// 完整格式：market:prices:v2:{marketType}:{sortedCrops}:{marketCode}:{startDate}:{endDate}
		/// v2：2026-11 查詢改用交易資料的 TcType 篩類別（總檢查 3-A2），舊版鍵底下存的是只算到部分市場的結果，
		/// 換一個前綴讓部署之後不會再讀到它們（快取 TTL 25 小時，不換的話要等它自己過期）。
		/// 之後查詢邏輯再改到「同一組條件會回不同結果」時，同樣要升版
		/// </summary>
		public const string MarketPricesPrefix = "market:prices:v2:";

		/// <summary>
		/// 某個類別有交易過的作物清單（2026-11 總檢查 G-C2 加的快取）。
		/// 完整格式：market:crops:v1:{TcType}（N04 蔬菜、N05 水果、N06 花卉）。
		/// 跟價格一樣，查詢邏輯改到「同一個類別會回不同結果」時要升版
		/// </summary>
		public const string MarketCropsPrefix = "market:crops:v1:";
	}
}
