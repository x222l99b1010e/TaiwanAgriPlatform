using System.Text.Json;
using System.Text.Json.Serialization;

namespace TaiwanAgri.Modules.Weather.Dtos.ExternalResponses
{
	/// <summary>
	/// 上游把字串欄位給成 JSON null 時，讀成空字串。
	/// <para>
	/// 農藥兩支上游 API 的字串欄位，沒有值時目前都給空字串，DTO 也用 string.Empty 當預設值；
	/// 但 JSON 的 null 會蓋掉預設值，PesticideService 裡的 .Trim() 就丟 NullReferenceException——
	/// 第一層（許可證清單）變成整支查詢 500，第二層（使用範圍）被 catch 接住、誤記成「使用範圍抓取失敗」
	/// （2026-11 總檢查 3 號 C 類）。在反序列化這一層統一換掉，Service 不必每個欄位都寫 ?.Trim() ?? ""
	/// </para>
	/// </summary>
	public sealed class NullAsEmptyStringConverter : JsonConverter<string>
	{
		/// <summary>預設情況下 null 不會交給轉換器、直接寫成 null；要設成 true，Read 才收得到 null</summary>
		public override bool HandleNull => true;

		public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
			reader.TokenType == JsonTokenType.Null ? string.Empty : reader.GetString()!;

		public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) =>
			writer.WriteStringValue(value);
	}
}
