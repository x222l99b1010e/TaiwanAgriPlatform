using System.Text.Json.Serialization;

namespace TaiwanAgri.Modules.Weather.Dtos.ExternalResponses
{
	/// <summary>
	/// PesticideDataQueryType 的回應信封（RS／Data／Next，與其他農業部 api/v1 端點相同）。
	/// </summary>
	public class PesticideDataQueryApiResponse
	{
		[JsonPropertyName("RS")] public string RS { get; set; } = string.Empty;
		[JsonPropertyName("Data")] public List<PesticideLicenseDto> Data { get; set; } = new();

		/// <summary>
		/// 「還有下一頁」＝結果已被截斷在第一頁 500 筆。
		/// 未帶 api_key 時拿不到第二頁（決策 1：api_key 的作用僅為分頁權限），
		/// 而這支 API 沒有總筆數欄位，所以這個布林值是「結果不完整」的唯一訊號。
		/// </summary>
		[JsonPropertyName("Next")] public bool Next { get; set; }
	}

	/// <summary>
	/// 一筆＝一張農藥許可證。刻意忠實承接 API 原始形狀（民國日期、右側補空白、代碼未翻譯），
	/// 型別轉換與語意化留給 Service 層做，不綁在反序列化上（§12.34.2 SRP 原則）。
	/// </summary>
	public class PesticideLicenseDto
	{
		/// <summary>許可證類別：農藥製／農藥進／農藥原製／農藥原進。帶「原」字的是原體（工業原料），無使用範圍資料。</summary>
		[JsonPropertyName("Permit")] public string Permit { get; set; } = string.Empty;

		/// <summary>許可證號，五位數字字串（含前導零，如 "04763"）。不可當數字處理。</summary>
		[JsonPropertyName("PermitNumber")] public string PermitNumber { get; set; } = string.Empty;

		/// <summary>有效成分俗名，使用者查的就是這個欄位。實測有極少數為空字串（第一頁 500 筆中 3 筆）。</summary>
		[JsonPropertyName("ChineseName")] public string ChineseName { get; set; } = string.Empty;

		/// <summary>有效成分代碼，如 I225。首字母為藥劑類別：I 殺蟲／F 殺菌／H 除草／A 殺蟎／X 混合。</summary>
		[JsonPropertyName("PesticideCode")] public string PesticideCode { get; set; } = string.Empty;

		[JsonPropertyName("EnName")] public string EnName { get; set; } = string.Empty;

		/// <summary>商品名（廠商自取的品牌名）。原體證多半為空。右側可能補空白。</summary>
		[JsonPropertyName("BrandName")] public string BrandName { get; set; } = string.Empty;

		[JsonPropertyName("ChemicalComposition")] public string ChemicalComposition { get; set; } = string.Empty;

		[JsonPropertyName("ForeignMaker")] public string ForeignMaker { get; set; } = string.Empty;

		/// <summary>許可證到期日，民國年「短橫線」分隔（如 "120-02-19"）。與 RevocationDate 的分隔符不同。</summary>
		[JsonPropertyName("ExpireDate")] public string ExpireDate { get; set; } = string.Empty;

		/// <summary>劑型代碼（SP／SC／WG…）。原體為空字串。</summary>
		[JsonPropertyName("formCode")] public string FormCode { get; set; } = string.Empty;

		/// <summary>
		/// 含量。格式不統一，實測出現過 "20.000 (%)"、"5.000  (%)"（兩個空格）、
		/// "300 g/L (30% w/v)"、"1,100,000 AmBu/g"。一律當字串處理，不要嘗試解析成數值。
		/// </summary>
		[JsonPropertyName("contents")] public string Contents { get; set; } = string.Empty;

		/// <summary>國內販售廠商。右側可能補空白。</summary>
		[JsonPropertyName("Vendor")] public string Vendor { get; set; } = string.Empty;

		/// <summary>
		/// 「使用範圍」的完整網址（含 ltyp／lno 參數），是適用作物與病蟲害資料的唯一來源。
		/// 直接使用這個值、不要自己組網址；但使用前必須驗證 host（見 PesticideService）。
		/// </summary>
		[JsonPropertyName("ScopeOfUse")] public string ScopeOfUse { get; set; } = string.Empty;

		/// <summary>許可證掃描圖的網址，供前端做外連（不由後端下載）。</summary>
		[JsonPropertyName("GetFile")] public string GetFile { get; set; } = string.Empty;

		/// <summary>廢止類型：空字串＝未廢止；有值時為 廢止／撤銷／申請廢止／逾期廢止 四者之一。</summary>
		[JsonPropertyName("RevocationType")] public string RevocationType { get; set; } = string.Empty;

		/// <summary>廢止日期，民國年「斜線」分隔（如 "079/05/03"）。無值時是含空白的 "   /  /  "，不是空字串。</summary>
		[JsonPropertyName("RevocationDate")] public string RevocationDate { get; set; } = string.Empty;

		/// <summary>藥劑分類，如 殺蟲劑／殺菌劑／除草劑。</summary>
		[JsonPropertyName("PesticideCategoryCh")] public string PesticideCategoryCh { get; set; } = string.Empty;

		/// <summary>化學類別，如 醯胺系／有機磷／氨基甲酸鹽。</summary>
		[JsonPropertyName("PesticideTypeCh")] public string PesticideTypeCh { get; set; } = string.Empty;

		/// <summary>國際抗藥性作用機制分類碼（殺菌／除草／殺蟲各一套），多為空字串。</summary>
		[JsonPropertyName("FRAC")] public string Frac { get; set; } = string.Empty;
		[JsonPropertyName("HRAC")] public string Hrac { get; set; } = string.Empty;
		[JsonPropertyName("IRAC")] public string Irac { get; set; } = string.Empty;
	}
}
