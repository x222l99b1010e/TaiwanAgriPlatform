using System;
using System.Collections.Generic;
using System.Text;

namespace TaiwanAgri.Core.Constants
{
	public static class MoaApiEndpoints
	{
		// 模組 2：氣象
		public const string AutoWeatherStation = "api/v1/AutoWeatherStationType/";
		public const string AutoRainfallStation = "api/v1/AutoRainfallStationType/";
		public const string PlantEpidemic = "api/v1/PlantEpidemicType/";
		public const string FruitPestControl = "api/v1/FruitVegetalePestControlType/";

		// 模組 4：行情
		public const string AgriProductsTrans = "api/v1/AgriProductsTransType/";
		public const string PorkTrans = "api/v1/PorkTransType/";
		public const string DebrisAlert = "api/v1/DebrisAlertServices/GetDebrisVillInfo/";
		public const string MarketRestDay = "api/v1/MarketRestDayFarmWCF/";

		// 模組 1：食安
		public const string Traceability = "api/v1/TraceabilityType/";
		public const string OrganicVerification = "api/v1/TWOrganicAgricultureVerificationInformationType/";
		public const string PesticideViolation = "api/v1/SalesResumeAgriproductsResultsType/";

		// 模組 3：寵物
		public const string AnimalRecognition = "api/v1/AnimalRecognition/";
		public const string PetLoseList = "api/v1/PetLoseList/";
	}
}
