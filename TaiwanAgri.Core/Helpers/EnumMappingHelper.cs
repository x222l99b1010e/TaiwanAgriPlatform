using Microsoft.Extensions.Logging;

namespace TaiwanAgri.Core.Helpers
{
	/// <summary>
	/// 外部 API 資料轉列舉時的共用容錯：記錄未預期值並回傳 fallback，
	/// 避免各模組各自重複寫「switch + LogWarning」樣板
	/// </summary>
	public static class EnumMappingHelper
	{
		public static T LogUnexpectedValue<T>(string recordKey, string fieldName, string rawValue, T fallback, ILogger logger)
		{
			logger.LogWarning("記錄 {RecordKey} 的 {Field} 出現未預期值 {RawValue}，fallback 為 {Fallback}",
				recordKey, fieldName, rawValue, fallback);
			return fallback;
		}
	}
}
