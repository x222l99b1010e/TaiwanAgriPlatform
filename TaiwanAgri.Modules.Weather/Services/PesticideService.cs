using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TaiwanAgri.Core.Constants;
using TaiwanAgri.Core.Helpers;
using TaiwanAgri.Modules.Weather.Constants;
using TaiwanAgri.Modules.Weather.Dtos.ApiResponses;
using TaiwanAgri.Modules.Weather.Dtos.ExternalResponses;
using TaiwanAgri.Modules.Weather.Dtos.Queries;

namespace TaiwanAgri.Modules.Weather.Services
{
	/// <summary>
	/// 農藥查詢（模組 2 F05）。形態與 W21b 追溯查詢相同：即時打農業部 API、不落地 DB、
	/// 不注入任何 DbContext。
	///
	/// 兩層抓取：
	///   第一層 PesticideDataQueryType──依成分俗名查許可證清單（單一來源，失敗即無結果可回）
	///   第二層 ScopeOfUse────────────依（成分,含量,劑型）去重後並行查核准用途（多來源，可部分失敗）
	/// 兩層的容錯策略刻意不同，理由見各自的方法註解。
	/// </summary>
	public class PesticideService : IPesticideService
	{
		/// <summary>
		/// 第二層網址只允許這個 host。ScopeOfUse 是「跟著外部回應內容去發請求」的模式，
		/// 不驗證等於把發送目標的控制權交給外部資料源。
		/// </summary>
		private const string AllowedDetailHost = "data.moa.gov.tw";

		private readonly HttpClient _httpClient;
		private readonly ILogger<PesticideService> _logger;
		private readonly TimeProvider _timeProvider;

		public PesticideService(
			IHttpClientFactory httpClientFactory,
			ILogger<PesticideService> logger,
			TimeProvider timeProvider)
		{
			_httpClient = httpClientFactory.CreateClient("MoaApi");
			_logger = logger;
			_timeProvider = timeProvider;
		}

		public async Task<PesticideSearchOutcome> SearchAsync(
			PesticideSearchQueryDto query,
			CancellationToken cancellationToken = default)
		{
			var keyword = query.Keyword?.Trim() ?? string.Empty;
			var englishName = query.EnglishName?.Trim() ?? string.Empty;

			// ── 第一層：查許可證清單 ─────────────────────────────────────
			// 兩個名稱參數上游都支援、且同時給時是真 AND（實測：ChineseName=亞滅培&EnName=XXXX 回 0 筆），
			// 所以有填才帶、兩個都填就讓上游自己交集
			var conditions = new List<string>();
			if (keyword.Length > 0)
				conditions.Add($"ChineseName={Uri.EscapeDataString(keyword)}");
			if (englishName.Length > 0)
				conditions.Add($"EnName={Uri.EscapeDataString(englishName)}");

			var url = $"{MoaApiEndpoints.PesticideDataQuery}?{string.Join("&", conditions)}";
			var apiResponse = await FetchLicensesAsync(url, cancellationToken);

			// 上游一頁 500 筆、未帶 api_key 拿不到第二頁，且沒有總筆數欄位——
			// Next=true 是「結果被截斷」的唯一訊號。此時拿到的是「照許可證號排序的前 500 筆」，
			// 不是相關性最高的 500 筆，回傳它等於給一份殘缺又順序無意義的結果，
			// 所以在進第二層之前就中止（也順帶避免寬鬆關鍵字引爆數十次第二層呼叫）。
			if (apiResponse.Next)
			{
				_logger.LogInformation(
					"[Pesticide] 查詢條件（中文「{Keyword}」／英文「{EnglishName}」）的結果被上游截斷（Next=true），中止查詢並要求收斂關鍵字",
					keyword, englishName);
				return new PesticideSearchOutcome { KeywordTooBroad = true };
			}

			var licenses = query.IncludeRevoked
				? apiResponse.Data
				: apiResponse.Data.Where(l => string.IsNullOrWhiteSpace(l.RevocationType)).ToList();

			// ── 分組：成分 → 劑型 ────────────────────────────────────────
			var ingredients = BuildIngredients(licenses, keyword, englishName);

			// ── 第二層：每個非原體劑型組各抓一次核准用途，全部並行 ──────
			// 分組鍵（成分,含量,劑型）以外的差異不影響使用範圍內容（探勘實測：亞滅培 53 張
			// 許可證背後只有 2 份相異內容），所以每組只需要一張代表許可證的 ScopeOfUse。
			var pending = ingredients
				.SelectMany(i => i.Formulations)
				.Where(f => !f.Dto.IsTechnicalGrade && !string.IsNullOrWhiteSpace(f.ScopeOfUseUrl))
				.ToList();

			if (pending.Count > 0)
			{
				await Task.WhenAll(pending.Select(f => FillUsagesAsync(f, cancellationToken)));
			}

			return new PesticideSearchOutcome
			{
				Response = new PesticideSearchResponseDto
				{
					Keyword = keyword,
					EnglishName = englishName,
					Ingredients = ingredients.Select(i => i.Dto).ToList()
				}
			};
		}

		/// <summary>
		/// 第一層抓取。與第二層不同，這裡**不吞例外**：
		/// 它是唯一的資料來源，失敗時沒有任何東西可以回，若比照 SafeFetch 回 null 再當成空結果，
		/// 使用者會看到「查無資料」——那是一句謊話（實際上是我們沒查成功）。
		/// 讓例外往上拋由 GlobalExceptionMiddleware 統一回 500，語意才誠實。
		/// </summary>
		private async Task<PesticideDataQueryApiResponse> FetchLicensesAsync(string url, CancellationToken cancellationToken)
		{
			var response = await _httpClient.GetAsync(url, cancellationToken);

			if (!response.IsSuccessStatusCode)
			{
				_logger.LogError("[Pesticide] 第一層 API 回應非 2xx（{StatusCode}）：{Url}",
					(int)response.StatusCode, url);
				throw new HttpRequestException($"農藥許可證查詢失敗，上游回應 {(int)response.StatusCode}");
			}

			return await response.Content.ReadFromJsonAsync<PesticideDataQueryApiResponse>(cancellationToken)
				?? new PesticideDataQueryApiResponse();
		}

		/// <summary>
		/// 第二層抓取。與第一層相反，這裡**吞掉所有失敗**：一個劑型的用途沒抓到，
		/// 不該讓其他劑型與整份許可證清單跟著消失（比照 FoodSafetyService 的 SafeFetch 精神）。
		/// 成敗記錄在 UsagesAvailable，讓前端能區分「沒有核准用途」與「這次沒抓到」。
		/// </summary>
		private async Task FillUsagesAsync(FormulationBuildItem item, CancellationToken cancellationToken)
		{
			var url = item.ScopeOfUseUrl;

			if (!IsAllowedDetailUrl(url))
			{
				_logger.LogWarning("[Pesticide] 使用範圍網址的 host 不在允許清單內，已略過：{Url}", url);
				return;
			}

			try
			{
				var response = await _httpClient.GetAsync(url, cancellationToken);
				if (!response.IsSuccessStatusCode)
				{
					_logger.LogWarning("[Pesticide] 使用範圍 API 回應非 2xx（{StatusCode}）：{Url}",
						(int)response.StatusCode, url);
					return;
				}

				// 實測地雷：部分許可證會回「HTTP 200 ＋ Content-Type: application/json ＋ 完全空的 body」
				// （案例：達馬松「上界靈」農藥製00044）。直接 ReadFromJsonAsync 會拋 JsonException，
				// 雖然下面的 catch 接得住，但會被誤記成「呼叫失敗」汙染日誌——
				// 空 body 的真實語意是「這張證沒有使用範圍資料」，屬正常情形，先攔下來當成空清單。
				var raw = await response.Content.ReadAsStringAsync(cancellationToken);
				if (string.IsNullOrWhiteSpace(raw))
				{
					item.Dto.UsagesAvailable = true;
					return;
				}

				var usages = JsonSerializer.Deserialize<List<PesticideUsageExternalDto>>(raw);
				if (usages == null)
				{
					_logger.LogWarning("[Pesticide] 使用範圍反序列化結果為 null：{Url}", url);
					return;
				}

				item.Dto.Usages = usages
					.Select(u => new PesticideUsageDto
					{
						CropName = u.CropName.Trim(),
						PestName = u.PestName.Trim(),
						Dilution = u.Dilution.Trim(),
						DosagePerHectare = u.DosagePerHectare.Trim(),
						ApplicationTiming = u.ApplicationTiming.Trim(),
						ApplicationInterval = u.ApplicationInterval.Trim(),
						ApplicationMethod = u.ApplicationMethod.Trim(),
						SafeHarvestInterval = u.SafeHarvestInterval.Trim(),
						Notes = u.Notes.Trim(),
						Precautions = u.Precautions.Trim()
					})
					.ToList();
				item.Dto.UsagesAvailable = true;
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "[Pesticide] 使用範圍抓取失敗，該劑型以無資料處理：{Url}", url);
			}
		}

		/// <summary>ScopeOfUse 網址必須是絕對網址、走 http(s)、且 host 為農業部開放資料平台。</summary>
		internal static bool IsAllowedDetailUrl(string? url)
		{
			if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
			if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return false;

			return string.Equals(uri.Host, AllowedDetailHost, StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>
		/// 把扁平的許可證清單組成「成分 → 劑型 → 許可證」三層結構。
		/// 第一層依 PesticideCode 分組（不是依 ChineseName）——同一個俗名可能對到多個成分代碼，
		/// 例如賽速安同時有 I235（單劑）與 I254（混合劑）。
		/// </summary>
		private List<IngredientBuildItem> BuildIngredients(
			List<PesticideLicenseDto> licenses, string keyword, string englishName)
		{
			var today = TaiwanTime.Today(_timeProvider);

			var result = new List<IngredientBuildItem>();

			// PesticideCode 理論上可能為空，退回用中文名當群組鍵，避免所有無代碼的資料被併成一堆
			var ingredientGroups = licenses
				.GroupBy(l => !string.IsNullOrWhiteSpace(l.PesticideCode)
					? l.PesticideCode.Trim()
					: $"NAME:{l.ChineseName.Trim()}");

			foreach (var ingredientGroup in ingredientGroups)
			{
				var sample = ingredientGroup.First();
				var chineseName = sample.ChineseName.Trim();
				var enName = sample.EnName.Trim();

				var ingredientDto = new PesticideIngredientDto
				{
					PesticideCode = sample.PesticideCode.Trim(),
					// 極少數許可證的成分名為空（實測第一頁 500 筆中 3 筆），
					// 空著會讓前端顯示一張沒有標題的卡片，退回用成分代碼當標題
					ChineseName = !string.IsNullOrWhiteSpace(chineseName) ? chineseName : sample.PesticideCode.Trim(),
					EnglishName = enName,
					Category = sample.PesticideCategoryCh.Trim(),
					ChemicalType = sample.PesticideTypeCh.Trim(),
					// 「完全符合」＝使用者填了的每個條件都是全等（沒填的條件不參與判斷）。
					// 英文名比對不分大小寫，因為上游的 EnName 大小寫並不一致
					// （實測同一份資料裡有 ACETAMIPRID 與 metalaxyl-M 兩種寫法並存）
					IsExactMatch =
						(keyword.Length == 0 || string.Equals(chineseName, keyword, StringComparison.Ordinal)) &&
						(englishName.Length == 0 || string.Equals(enName, englishName, StringComparison.OrdinalIgnoreCase))
				};

				var formulationItems = new List<FormulationBuildItem>();

				// 第二層分組鍵＝（含量, 劑型）。成分已經由外層分組固定，
				// 所以這裡不必再放 PesticideCode——三者合起來就是探勘驗證出的
				// (PesticideCode, contents, formCode) 分組鍵。
				var formulationGroups = ingredientGroup
					.GroupBy(l => (Contents: l.Contents.Trim(), FormCode: l.FormCode.Trim()));

				foreach (var formulationGroup in formulationGroups)
				{
					var isTechnicalGrade = formulationGroup.All(IsTechnicalGrade);

					var formulationDto = new PesticideFormulationDto
					{
						FormCode = formulationGroup.Key.FormCode,
						FormName = PesticideForms.ToChineseName(formulationGroup.Key.FormCode),
						Contents = formulationGroup.Key.Contents,
						IsTechnicalGrade = isTechnicalGrade,
						Licenses = formulationGroup
							.Select(l => MapLicense(l, today))
							.OrderBy(l => l.Permit, StringComparer.Ordinal)
							.ThenBy(l => l.PermitNumber, StringComparer.Ordinal)
							.ToList()
					};

					// 原體沒有使用範圍資料（實測一律回空陣列），不必浪費一次呼叫；
					// 代表許可證挑「非原體且有 ScopeOfUse」的第一張。
					var representative = formulationGroup
						.FirstOrDefault(l => !IsTechnicalGrade(l) && !string.IsNullOrWhiteSpace(l.ScopeOfUse));

					formulationItems.Add(new FormulationBuildItem
					{
						Dto = formulationDto,
						ScopeOfUseUrl = representative?.ScopeOfUse.Trim() ?? string.Empty
					});
				}

				ingredientDto.Formulations = formulationItems
					.Select(f => f.Dto)
					.OrderBy(f => f.IsTechnicalGrade)                       // 農民買得到的製劑排前面
					.ThenBy(f => f.FormCode, StringComparer.Ordinal)
					.ThenBy(f => f.Contents, StringComparer.Ordinal)
					.ToList();

				result.Add(new IngredientBuildItem
				{
					Dto = ingredientDto,
					Formulations = formulationItems
				});
			}

			// 完全符合關鍵字的成分排最前面，其餘依中文名排序，讓輸出順序穩定可預期
			return result
				.OrderByDescending(i => i.Dto.IsExactMatch)
				.ThenBy(i => i.Dto.ChineseName, StringComparer.Ordinal)
				.ToList();
		}

		/// <summary>原體＝工業原料，許可證類別帶「原」字（農藥原製／農藥原進）。</summary>
		private static bool IsTechnicalGrade(PesticideLicenseDto license)
			=> license.Permit.Contains('原');

		private static PesticideLicenseResultDto MapLicense(PesticideLicenseDto license, DateOnly today)
		{
			// 兩個日期欄位的民國格式分隔符不同（ExpireDate 用 '-'、RevocationDate 用 '/'），
			// 且 RevocationDate 的無值是含空白的 "   /  /  "，統一交給寬鬆解析器處理
			var expireDate = DateHelper.ParseRocSeparatedDate(license.ExpireDate);
			var revocationType = license.RevocationType.Trim();

			return new PesticideLicenseResultDto
			{
				Permit = license.Permit.Trim(),
				PermitNumber = license.PermitNumber.Trim(),
				BrandName = license.BrandName.Trim(),
				Vendor = license.Vendor.Trim(),
				ForeignMaker = license.ForeignMaker.Trim(),
				ExpireDateRoc = license.ExpireDate.Trim(),
				ExpireDate = expireDate,
				// 日期解析不出來時不臆測，一律當作「未過期」，寧可少報也不要憑空標記為失效
				IsExpired = expireDate.HasValue && expireDate.Value < today,
				IsRevoked = !string.IsNullOrWhiteSpace(revocationType),
				RevocationType = string.IsNullOrWhiteSpace(revocationType) ? null : revocationType,
				RevocationDate = DateHelper.ParseRocSeparatedDate(license.RevocationDate),
				LicenseImageUrl = license.GetFile.Trim()
			};
		}

		// ── 組裝過程用的中繼型別 ────────────────────────────────────────
		// 需要它們的原因：第二層抓取要知道「這個劑型組要打哪個網址」，
		// 但那個網址不屬於對外 DTO 的一部分（前端用不到，也不該把內部抓取細節外洩）。
		private sealed class IngredientBuildItem
		{
			public required PesticideIngredientDto Dto { get; init; }
			public required List<FormulationBuildItem> Formulations { get; init; }
		}

		private sealed class FormulationBuildItem
		{
			public required PesticideFormulationDto Dto { get; init; }
			public required string ScopeOfUseUrl { get; init; }
		}
	}
}
