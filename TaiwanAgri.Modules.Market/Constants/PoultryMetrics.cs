using System.Collections.Generic;

namespace TaiwanAgri.Modules.Market.Constants
{
	/// <summary>
	/// PoultryTrans.MetricCode 的單一真相來源：常數本身＋中文顯示名對照。
	/// Worker 寫入、查詢層白名單驗證、前端顯示名，三處都應該引用這裡，
	/// 不要各自重複寫死字串（原始 API 的 JSON key 風格四支各異且含不合法字元，
	/// 如 "TaijinPrice_2.0kgup" 帶小數點，不能直接當 C# 識別字或長期對外的代碼）。
	/// </summary>
	public static class PoultryMetrics
	{
		// --- 白肉雞／雞蛋 (PoultryTransType_BoiledChicken_Eggs) ---
		public const string BoiledChicken_2_0KgUp = "BoiledChicken_2_0KgUp";
		public const string BoiledChicken_1_75To1_95Kg = "BoiledChicken_1_75To1_95Kg";
		public const string BoiledChicken_Store_KaoPing = "BoiledChicken_Store_KaoPing";
		public const string Egg_Transport = "Egg_Transport";
		public const string Egg_Producer = "Egg_Producer";

		// --- 紅羽土雞 (PoultryTransType_RedFeather)：北/中/南 × 公/母 ---
		public const string RedFeather_North_Male = "RedFeather_North_Male";
		public const string RedFeather_North_Female = "RedFeather_North_Female";
		public const string RedFeather_Central_Male = "RedFeather_Central_Male";
		public const string RedFeather_Central_Female = "RedFeather_Central_Female";
		public const string RedFeather_South_Male = "RedFeather_South_Male";
		public const string RedFeather_South_Female = "RedFeather_South_Female";

		// --- 黑羽土雞 (PoultryTransType_BlackFeather)：舍飼南區 公/母 ---
		public const string BlackFeather_South_Male = "BlackFeather_South_Male";
		public const string BlackFeather_South_Female = "BlackFeather_South_Female";

		// --- 肉鵝／番鴨／鴨蛋 (PoultryTransType_Goose_Duck_Duckegg) ---
		public const string Goose_WhiteRoman = "Goose_WhiteRoman";
		public const string Duck_Male = "Duck_Male";
		public const string Duck_75Days = "Duck_75Days";
		public const string Duckegg_Tainan = "Duckegg_Tainan";

		/// <summary>MetricCode → 中文顯示名，前端與查詢層共用</summary>
		public static readonly IReadOnlyDictionary<string, string> DisplayNames = new Dictionary<string, string>
		{
			{ BoiledChicken_2_0KgUp, "白肉雞2.0Kg以上" },
			{ BoiledChicken_1_75To1_95Kg, "白肉雞1.75-1.95Kg" },
			{ BoiledChicken_Store_KaoPing, "白肉雞門市價(高屏)" },
			{ Egg_Transport, "雞蛋大運輸價" },
			{ Egg_Producer, "雞蛋產地價" },

			{ RedFeather_North_Male, "紅羽土雞北區(公)" },
			{ RedFeather_North_Female, "紅羽土雞北區(母)" },
			{ RedFeather_Central_Male, "紅羽土雞中區(公)" },
			{ RedFeather_Central_Female, "紅羽土雞中區(母)" },
			{ RedFeather_South_Male, "紅羽土雞南區(公)" },
			{ RedFeather_South_Female, "紅羽土雞南區(母)" },

			{ BlackFeather_South_Male, "黑羽土雞舍飼(南區)公" },
			{ BlackFeather_South_Female, "黑羽土雞舍飼(南區)母" },

			{ Goose_WhiteRoman, "肉鵝白羅曼" },
			{ Duck_Male, "正番鴨公" },
			{ Duck_75Days, "土番鴨75天" },
			{ Duckegg_Tainan, "鴨蛋新蛋(台南)" },
		};

		/// <summary>查詢層白名單驗證用（比照 GetPorkAsync 的做法）</summary>
		public static bool IsValid(string metricCode) => DisplayNames.ContainsKey(metricCode);

		// --- 依來源 API 分組：Worker 查「DB 已存在哪些鍵」時用來把掃描範圍限縮在該支來源 ---
		// 宣告成 string[] 而非 IReadOnlyList<string>，是為了讓 EF Core 能把 Contains 轉譯成 SQL IN

		public static readonly string[] BoiledChickenEggsMetrics =
		{
			BoiledChicken_2_0KgUp, BoiledChicken_1_75To1_95Kg, BoiledChicken_Store_KaoPing,
			Egg_Transport, Egg_Producer
		};

		public static readonly string[] RedFeatherMetrics =
		{
			RedFeather_North_Male, RedFeather_North_Female,
			RedFeather_Central_Male, RedFeather_Central_Female,
			RedFeather_South_Male, RedFeather_South_Female
		};

		public static readonly string[] BlackFeatherMetrics =
		{
			BlackFeather_South_Male, BlackFeather_South_Female
		};

		public static readonly string[] GooseDuckDuckeggMetrics =
		{
			Goose_WhiteRoman, Duck_Male, Duck_75Days, Duckegg_Tainan
		};
	}
}
