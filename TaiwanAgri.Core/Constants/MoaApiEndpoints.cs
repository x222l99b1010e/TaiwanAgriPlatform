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

		// 模組 4：行情
		public const string AgriProductsTrans = "api/v1/AgriProductsTransType/";
		public const string PorkTrans = "api/v1/PorkTransType/";
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
	}
}
