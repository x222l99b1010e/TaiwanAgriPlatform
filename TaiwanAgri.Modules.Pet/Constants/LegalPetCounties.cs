namespace TaiwanAgri.Modules.Pet.Constants
{
	/// <summary>
	/// LegalSpecificPet 的 legaltype（所在縣市代碼）對照表。
	/// 官方 API 介接說明書（MOAOPD-API-EIR643）裡這張表的 PDF 轉文字順序整個錯位，
	/// 直接照抄會抄錯（例如 A200 文件排版看起來像嘉義市，實際上是嘉義縣）。
	/// 這裡改用真實資料反查：抓舊制端點 5845 筆全量資料，比對每個代碼底下 legaladdress
	/// 開頭最常出現的縣市名稱，逐一驗證後才定案（2026-07-30）。
	/// A330（連江縣）文件有列但目前 0 筆資料未實測到，保留在清單裡是因為迴圈同步時
	/// 這個代碼本來就該查一次（哪天馬祖真的有業者登記，不會漏接）。
	/// </summary>
	public static class LegalPetCounties
	{
		public static readonly IReadOnlyDictionary<string, string> CodeToName = new Dictionary<string, string>
		{
			["A010"] = "臺北市",
			["A020"] = "新北市",
			["A030"] = "基隆市",
			["A040"] = "高雄市",
			["A060"] = "臺中市",
			["A090"] = "桃園市",
			["A100"] = "新竹市",
			["A110"] = "新竹縣",
			["A130"] = "苗栗縣",
			["A150"] = "南投縣",
			["A170"] = "彰化縣",
			["A180"] = "雲林縣",
			["A190"] = "嘉義市",
			["A200"] = "嘉義縣",
			["A210"] = "臺南市",
			["A240"] = "屏東縣",
			["A260"] = "宜蘭縣",
			["A280"] = "花蓮縣",
			["A300"] = "臺東縣",
			["A310"] = "澎湖縣",
			["A320"] = "金門縣",
			["A330"] = "連江縣",
		};
	}
}
