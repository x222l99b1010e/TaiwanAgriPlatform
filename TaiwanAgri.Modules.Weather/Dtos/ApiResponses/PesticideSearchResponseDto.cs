namespace TaiwanAgri.Modules.Weather.Dtos.ApiResponses
{
	/// <summary>
	/// Service 層回給 Controller 的信封。
	///
	/// 為什麼「關鍵字過廣」不用丟例外表達：那是**預期內**的使用者輸入問題（查「滅」這種單字），
	/// 不是例外狀況。用例外表達會讓正常流程走 catch，也會逼 GlobalExceptionMiddleware
	/// 為了一個模組的輸入驗證去長出領域知識。由 Controller 讀旗標後決定 HTTP 語意（400），
	/// 職責邊界比較乾淨。
	/// </summary>
	public class PesticideSearchOutcome
	{
		/// <summary>
		/// 上游第一層查詢已被截斷（Next=true）。為 true 時 <see cref="Response"/> 為 null——
		/// 因為此時拿到的 500 筆是「照許可證號排序的前 500 筆」，不是相關性最高的 500 筆，
		/// 回傳它等於給使用者一份殘缺而且順序無意義的結果。
		/// </summary>
		public bool KeywordTooBroad { get; init; }

		public PesticideSearchResponseDto? Response { get; init; }
	}

	/// <summary>
	/// 農藥查詢結果。三層結構：成分 → 劑型 → 許可證。
	///
	/// 為什麼分三層而不是攤平成許可證清單，兩個獨立理由：
	/// 1. **使用範圍掛在劑型層級**（探勘實測：亞滅培 53 張許可證背後只有 2 份相異的使用範圍，
	///    分組鍵是 成分＋含量＋劑型，與許可證號無關）。攤平的話同一份 184 列的使用範圍要在
	///    52 張許可證上各複製一份，9742 列 vs 358 列，差 27 倍。
	/// 2. **上游 ChineseName 是 contains 模糊比對，會撈到其他成分**（查「加保扶」會一併回
	///    「丁基加保扶」＝另一種農藥 CARBOSULFAN）。刻意不做精確過濾——使用者記不全藥名時
	///    那些鄰近結果正是他要的——改用分組讓使用者一眼看出「查到幾種成分」，
	///    選擇權留在使用端，但不同成分的資料不會混在同一份清單裡誤導。
	/// </summary>
	public class PesticideSearchResponseDto
	{
		/// <summary>使用者輸入的中文成分名（已 Trim；未填時為空字串）。</summary>
		public string Keyword { get; set; } = string.Empty;

		/// <summary>使用者輸入的英文成分名（已 Trim；未填時為空字串）。</summary>
		public string EnglishName { get; set; } = string.Empty;

		public List<PesticideIngredientDto> Ingredients { get; set; } = new();
	}

	/// <summary>第一層：有效成分。</summary>
	public class PesticideIngredientDto
	{
		public string PesticideCode { get; set; } = string.Empty;
		public string ChineseName { get; set; } = string.Empty;
		public string EnglishName { get; set; } = string.Empty;

		/// <summary>藥劑分類，如 殺蟲劑。</summary>
		public string Category { get; set; } = string.Empty;

		/// <summary>化學類別，如 醯胺系。</summary>
		public string ChemicalType { get; set; } = string.Empty;

		/// <summary>
		/// 成分名是否與關鍵字完全相同。供前端把「完全符合」的那組排前面或視覺突顯，
		/// 不是用來過濾——不完全符合的成分一樣要回傳。
		/// </summary>
		public bool IsExactMatch { get; set; }

		public List<PesticideFormulationDto> Formulations { get; set; } = new();
	}

	/// <summary>第二層：劑型（＝使用範圍真正的分組單位）。</summary>
	public class PesticideFormulationDto
	{
		/// <summary>劑型代碼原始值，原體為空字串。</summary>
		public string FormCode { get; set; } = string.Empty;

		/// <summary>劑型中文名。查無對應代碼時 fallback 為原始代碼字串。</summary>
		public string FormName { get; set; } = string.Empty;

		/// <summary>含量原始字串（格式不統一，僅供顯示與分組，不做運算）。</summary>
		public string Contents { get; set; } = string.Empty;

		/// <summary>這個劑型是不是原體（工業原料，農民買不到、也沒有使用範圍資料）。</summary>
		public bool IsTechnicalGrade { get; set; }

		public List<PesticideLicenseResultDto> Licenses { get; set; } = new();

		/// <summary>核准的用途清單。整組共用一份，不在每張許可證底下重複。</summary>
		public List<PesticideUsageDto> Usages { get; set; } = new();

		/// <summary>
		/// 使用範圍是否成功取得。
		/// Usages 為空有三種完全不同的成因：上游真的沒有核准用途、第二層回空 body、該次呼叫失敗被吞掉。
		/// 少了這個旗標，前端無法區分「這個劑型沒有核准用途」與「這次沒抓到，請重試」。
		/// 原體（IsTechnicalGrade）不會去抓，此旗標固定為 false。
		/// </summary>
		public bool UsagesAvailable { get; set; }
	}

	/// <summary>第三層：許可證（＝市面上實際存在的一個產品）。</summary>
	public class PesticideLicenseResultDto
	{
		public string Permit { get; set; } = string.Empty;
		public string PermitNumber { get; set; } = string.Empty;
		public string BrandName { get; set; } = string.Empty;
		public string Vendor { get; set; } = string.Empty;

		/// <summary>國外原廠（進口證才有）。</summary>
		public string ForeignMaker { get; set; } = string.Empty;

		/// <summary>到期日民國原字串，保留原樣供顯示（使用者看的是民國年）。</summary>
		public string ExpireDateRoc { get; set; } = string.Empty;

		/// <summary>到期日轉西元；無法解析時為 null。</summary>
		public DateOnly? ExpireDate { get; set; }

		/// <summary>
		/// 是否已過期。刻意與 IsRevoked 分開——探勘發現「未廢止」不等於「有效」：
		/// 亞滅培 69 張未廢止的許可證裡仍有 8 張到期日已過。兩者是獨立的失效原因，
		/// 壓縮成單一布林值會蓋掉「證還在但去年就過期了」這個真實狀態。
		/// 日界以台灣時區計算（TaiwanTime）。
		/// </summary>
		public bool IsExpired { get; set; }

		/// <summary>是否已廢止。</summary>
		public bool IsRevoked { get; set; }

		/// <summary>廢止類型：廢止／撤銷／申請廢止／逾期廢止；未廢止時為 null。</summary>
		public string? RevocationType { get; set; }

		/// <summary>廢止日期轉西元；未廢止或無法解析時為 null。</summary>
		public DateOnly? RevocationDate { get; set; }

		/// <summary>農業部的許可證掃描圖網址，供前端做外連。</summary>
		public string LicenseImageUrl { get; set; } = string.Empty;
	}

	/// <summary>核准用途的單筆紀錄。</summary>
	public class PesticideUsageDto
	{
		public string CropName { get; set; } = string.Empty;
		public string PestName { get; set; } = string.Empty;
		public string Dilution { get; set; } = string.Empty;
		public string DosagePerHectare { get; set; } = string.Empty;
		public string ApplicationTiming { get; set; } = string.Empty;
		public string ApplicationInterval { get; set; } = string.Empty;
		public string ApplicationMethod { get; set; } = string.Empty;

		/// <summary>安全採收期——這個功能對使用者最關鍵的欄位。</summary>
		public string SafeHarvestInterval { get; set; } = string.Empty;

		public string Notes { get; set; } = string.Empty;
		public string Precautions { get; set; } = string.Empty;
	}
}
