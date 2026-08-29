using System;
using System.Collections.Generic;
using System.Text;

namespace TaiwanAgri.Core.Constants
{
	public static class MoaApiEndpoints
	{
		// 模組 2：氣象
		public const string AutoWeatherStation = "api/v1/AutoWeatherStationType/";
		public const string AutoRainfall = "api/v1/AutoRainfallStationType/";
		public const string RainfallStation = "api/v1/TaiwanRainfallStationInformationType/";
		public const string PlantEpidemic = "api/v1/PlantEpidemicType/";
		public const string FruitPestControl = "api/v1/FruitVegetalePestControlType/";
		// 農藥查詢（W24）：許可證主檔。只吃 ChineseName（成分俗名）與 EnName 兩個查詢參數，
		// 且兩者都是 contains 模糊比對；送 Permit／PermitNumber／PesticideCode／BrandName 會被
		// 「靜默忽略」——回 200、回一大包未過濾的資料，不報錯（探勘實測，同 W21b Traceno_Start 那類坑）。
		public const string PesticideDataQuery = "api/v1/PesticideDataQueryType/";
		// 農藥「使用範圍」（適用作物／病蟲害／稀釋倍數／安全採收期）走農業部舊制 FromM 通道，
		// 網址由 PesticideDataQuery 回應的 ScopeOfUse 欄位直接提供（含 ltyp／lno 參數），
		// 因此這裡不放常數、也不要自己組網址——只保留這行說明避免日後有人以為漏掉了。
		// 刻意不納入的兩支候選端點（2026-08-22 探勘結論，詳見文件資料夾 W24 探勘脈絡）：
		//   PesticideManualType──停留在 2014 年前的靜態快照，無任何狀態欄位（已禁用的農藥仍列為現行），
		//                        且與許可證主檔對同一產品的劑型記載互相矛盾。
		//   PesticideType──────沒有任何可用的查詢參數（13 種參數名實測全部無效），永遠只能拿到
		//                        整表前 500 筆；且其 5 個欄位在許可證主檔全部都有對應，無獨佔資訊。

		// 模組 4：行情
		public const string AgriProductsTrans = "api/v1/AgriProductsTransType/";
		public const string PorkTrans = "api/v1/PorkTransType/";
		// 家禽行情（W25）：四支各自獨立的端點，欄位集互不相同，共同點只有
		// TransDate/LunarCalendar 與 RS/Data/Next 外殼。日期參數 Start_time/End_time
		// 與回傳的 TransDate 都是「西元 yyyy/MM/dd」，與 PorkTrans 的民國 YYYMMDD 不同，
		// 不可套用 DateHelper 的 ROC 轉換。
		// 各支的歷史起點不同（實測）：BoiledChicken_Eggs 與 Goose_Duck_Duckegg 自
		// 2010/10/07 起，RedFeather 與 BlackFeather 自 2014/04/01 起——四支共用單一
		// 同步游標會漏抓前者 2010-2014 的資料，故 Worker 內為四支各配一組 SyncState。
		public const string PoultryBoiledChickenEggs = "api/v1/PoultryTransType_BoiledChicken_Eggs/";
		public const string PoultryRedFeather = "api/v1/PoultryTransType_RedFeather/";
		public const string PoultryBlackFeather = "api/v1/PoultryTransType_BlackFeather/";
		public const string PoultryGooseDuckDuckegg = "api/v1/PoultryTransType_Goose_Duck_Duckegg/";
		// 土石流警戒走農業部舊制 TransService 通道（非 api/v1 REST 形態）：
		// 該資料集未上架新版 OpenData API，只能以 UnitId 參數呼叫舊端點
		public const string DebrisAlert = "Service/OpenData/TransService.aspx?UnitId=kRam3LShuWSv";
		public const string MarketRestDay = "api/v1/MarketRestDayFarmWCF/";
		public const string CropMarketType = "api/v1/CropMarketType/?CropMarketType=";

		// 模組 1：食安
		public const string Traceability = "api/v1/TraceabilityType/";
		public const string AgriProductInfo = "api/v1/TWAgriProductsTraceabilityType_ProductInfo/";
		public const string AgriProducerInfo = "api/v1/TWAgriProductsTraceabilityType_ProducerInfo/";
		public const string WashedEggs = "api/v1/WashedEggsTraceabilityType/";
		public const string DomesticPoultry = "api/v1/DomesticPoultryTraceabilityType/";
		public const string OrganicVerification = "api/v1/TWOrganicAgricultureVerificationInformationType/";
		public const string PesticideViolation = "api/v1/SalesResumeAgriproductsResultsType/";

		// 模組 3：寵物
		public const string AnimalRecognition = "api/v1/AnimalRecognition/";
		// AnimalRecognition 新制存在但有 $top 上限鎖死 1000、Page=2 被擋的限制，
		// 一次性回填改走舊制 TransService 通道拿全量資料（見 DECISIONS.md 關鍵決策 12）
		public const string AnimalRecognitionLegacy = "Service/OpenData/TransService.aspx?UnitId=QcbUEzN6E6DL";
		public const string PetLoseList = "api/v1/PetLoseList/";
		public const string LegalSpecificPet = "api/v1/LegalSpecificPet/";
		// LegalSpecificPet 舊制端點：文件（MOAOPD-API-EIR643）寫「單次查詢最多回傳1000筆」，
		// 但實測不帶參數直接拿到 5845 筆——文件與實際行為不符，代表容量沒有正式保證，
		// 只適合當一次性回填起點，長期排程改走上面的新制逐縣市迴圈（見 LegalPetCounties）
		public const string LegalSpecificPetLegacy = "Service/OpenData/TransService.aspx?UnitId=fNT9RMo8PQRO";
	}
}
