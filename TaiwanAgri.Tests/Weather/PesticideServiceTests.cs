using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TaiwanAgri.Modules.Weather.Constants;
using TaiwanAgri.Modules.Weather.Dtos.Queries;
using TaiwanAgri.Modules.Weather.Services;

namespace TaiwanAgri.Tests.Weather
{
	/// <summary>
	/// PesticideService 的單元測試。用假的 HttpMessageHandler 依 URL 回應不同 JSON，
	/// 不需要真的打農業部 API，即可驗證探勘階段確認的幾個關鍵行為：
	/// (1) 上游截斷（Next=true）要中止且不進第二層
	/// (2) 已廢止許可證的預設過濾與 includeRevoked 放行
	/// (3) 依（成分,含量,劑型）去重——同組多張許可證只打一次第二層
	/// (4) contains 撈到多個成分時分組不混在一起
	/// (5) 原體不打第二層
	/// (6) 第二層回空 body／失敗時的差異（UsagesAvailable）
	/// (7) 「未廢止但已到期」是獨立於廢止的第三種狀態
	/// </summary>
	public class PesticideServiceTests
	{
		// ── 測試替身 ────────────────────────────────────────────────────────

		private sealed class RouteHandler : HttpMessageHandler
		{
			private readonly List<(string PathContains, Func<HttpResponseMessage> Respond)> _routes = new();
			public List<string> RequestedUrls { get; } = new();

			public void When(string pathContains, Func<HttpResponseMessage> respond)
				=> _routes.Add((pathContains, respond));

			protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
			{
				var url = request.RequestUri!.ToString();
				lock (RequestedUrls) { RequestedUrls.Add(url); }

				foreach (var (pathContains, respond) in _routes)
				{
					if (url.Contains(pathContains))
						return Task.FromResult(respond());
				}

				return Task.FromResult(JsonResponse(new { RS = "OK", Data = Array.Empty<object>(), Next = false }));
			}
		}

		private static HttpResponseMessage JsonResponse(object payload)
			=> new(HttpStatusCode.OK)
			{
				Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
			};

		private static HttpResponseMessage RawResponse(string body, HttpStatusCode status = HttpStatusCode.OK)
			=> new(status)
			{
				Content = new StringContent(body, Encoding.UTF8, "application/json")
			};

		private static PesticideService CreateService(RouteHandler handler)
		{
			var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://data.moa.gov.tw/") };
			var mockFactory = new Mock<IHttpClientFactory>();
			mockFactory.Setup(f => f.CreateClient("MoaApi")).Returns(httpClient);

			return new PesticideService(mockFactory.Object,
				NullLogger<PesticideService>.Instance, TimeProvider.System);
		}

		private static PesticideSearchQueryDto Query(
			string? keyword = null, string? englishName = null, bool includeRevoked = false)
			=> new() { Keyword = keyword, EnglishName = englishName, IncludeRevoked = includeRevoked };

		/// <summary>
		/// 組一筆許可證資料。欄位名刻意用農業部原始拼法（含小寫開頭的 formCode／contents），
		/// 因為外部 DTO 是靠 JsonPropertyName 對應的，測試要走過真正的反序列化路徑才有意義。
		/// </summary>
		private static object License(
			string permit = "農藥製",
			string permitNumber = "04763",
			string chineseName = "亞滅培",
			string pesticideCode = "I225",
			string brandName = "冠天下",
			string formCode = "SP",
			string contents = "20.000 (%)",
			string expireDate = "150-02-19",
			string revocationType = "",
			string revocationDate = "   /  /  ",
			string? scopeOfUse = null)
			=> new
			{
				Permit = permit,
				PermitNumber = permitNumber,
				ChineseName = chineseName,
				PesticideCode = pesticideCode,
				EnName = "ACETAMIPRID",
				BrandName = brandName,
				ChemicalComposition = "",
				ForeignMaker = "",
				ExpireDate = expireDate,
				formCode = formCode,
				contents = contents,
				Vendor = "台灣庵原農藥股份有限公司",
				ScopeOfUse = scopeOfUse
					?? $"https://data.moa.gov.tw/Service/OpenData/FromM/PesticideDetail.aspx?ltyp=10&lno={permitNumber}",
				GetFile = $"https://data.moa.gov.tw/Service/OpenData/FromM/PesticideLicImage.aspx?licType=10&licNo={permitNumber}",
				RevocationType = revocationType,
				RevocationDate = revocationDate,
				PesticideCategoryCh = "殺蟲劑",
				PesticideTypeCh = "醯胺系",
				FRAC = "",
				HRAC = "",
				IRAC = "4A"
			};

		private static object Usage(string cropName = "番茄", string pestName = "粉蝨類")
			=> new Dictionary<string, string>
			{
				["作物名稱"] = cropName,
				["病蟲害名稱"] = pestName,
				["施用次數"] = "",
				["每公頃使用用藥量"] = "0.5-0.6公斤",
				["稀釋倍數"] = "2,000",
				["使用時期"] = "害蟲發生時開始施藥。",
				["施藥間隔"] = "7日",
				["安全採收期"] = "6日",
				["備註"] = "",
				["注意事項"] = "",
				["施藥方法"] = "",
				["說明"] = "",
				["核准日期"] = "1110524",
				["原始登記廠商名稱"] = ""
			};

		// ── (1) 上游截斷 ────────────────────────────────────────────────────

		[Fact]
		public async Task Search_上游回Next為true_回報關鍵字過廣且不打第二層()
		{
			var handler = new RouteHandler();
			handler.When("PesticideDataQueryType", () => JsonResponse(new
			{
				RS = "OK",
				Next = true,                       // ← 結果被截斷
				Data = new[] { License() }
			}));

			var result = await CreateService(handler).SearchAsync(Query("滅"));

			Assert.True(result.KeywordTooBroad);
			Assert.Null(result.Response);
			// 關鍵：截斷時要在進第二層之前就中止，否則寬鬆關鍵字會引爆數十次呼叫
			Assert.Single(handler.RequestedUrls);
			Assert.DoesNotContain(handler.RequestedUrls, u => u.Contains("PesticideDetail"));
		}

		// ── (2) 廢止過濾 ────────────────────────────────────────────────────

		[Fact]
		public async Task Search_預設不含已廢止_只回未廢止的許可證()
		{
			var handler = new RouteHandler();
			handler.When("PesticideDataQueryType", () => JsonResponse(new
			{
				RS = "OK",
				Next = false,
				Data = new[]
				{
					License(permitNumber: "04763", brandName: "冠天下"),
					License(permitNumber: "03076", brandName: "已廢止品", revocationType: "廢止", revocationDate: "103/01/01")
				}
			}));
			handler.When("PesticideDetail", () => JsonResponse(new[] { Usage() }));

			var result = await CreateService(handler).SearchAsync(Query("亞滅培"));

			var licenses = result.Response!.Ingredients.Single().Formulations.Single().Licenses;
			Assert.Single(licenses);
			Assert.Equal("冠天下", licenses[0].BrandName);
		}

		[Fact]
		public async Task Search_includeRevoked為true_已廢止的也回傳且帶廢止資訊()
		{
			var handler = new RouteHandler();
			handler.When("PesticideDataQueryType", () => JsonResponse(new
			{
				RS = "OK",
				Next = false,
				Data = new[]
				{
					License(permitNumber: "03076", brandName: "已廢止品", revocationType: "逾期廢止", revocationDate: "103/01/01")
				}
			}));
			handler.When("PesticideDetail", () => JsonResponse(new[] { Usage() }));

			var result = await CreateService(handler).SearchAsync(Query("亞滅培", includeRevoked: true));

			var license = result.Response!.Ingredients.Single().Formulations.Single().Licenses.Single();
			Assert.True(license.IsRevoked);
			Assert.Equal("逾期廢止", license.RevocationType);
			Assert.Equal(new DateOnly(2014, 1, 1), license.RevocationDate);
		}

		// ── (3) 依（成分,含量,劑型）去重 ──────────────────────────────────

		[Fact]
		public async Task Search_同劑型多張許可證_只打一次第二層且共用同一份使用範圍()
		{
			var handler = new RouteHandler();
			handler.When("PesticideDataQueryType", () => JsonResponse(new
			{
				RS = "OK",
				Next = false,
				Data = new[]
				{
					License(permitNumber: "04763", brandName: "冠天下"),
					License(permitNumber: "04983", brandName: "勇伯仔"),
					License(permitNumber: "05435", brandName: "強必勇")
				}
			}));
			handler.When("PesticideDetail", () => JsonResponse(new[] { Usage("番茄"), Usage("甘藍", "黃條葉蚤") }));

			var result = await CreateService(handler).SearchAsync(Query("亞滅培"));

			var formulation = result.Response!.Ingredients.Single().Formulations.Single();
			Assert.Equal(3, formulation.Licenses.Count);
			Assert.Equal(2, formulation.Usages.Count);
			// 三張許可證同屬 (I225, 20.000 (%), SP)，第二層只需要抓一次
			Assert.Single(handler.RequestedUrls, u => u.Contains("PesticideDetail"));
		}

		[Fact]
		public async Task Search_同成分不同劑型_分成兩組且各自抓一次()
		{
			var handler = new RouteHandler();
			handler.When("PesticideDataQueryType", () => JsonResponse(new
			{
				RS = "OK",
				Next = false,
				Data = new[]
				{
					License(permitNumber: "04763", formCode: "SP"),
					License(permitNumber: "01977", formCode: "SG", brandName: "日曹強必勇")
				}
			}));
			handler.When("PesticideDetail", () => JsonResponse(new[] { Usage() }));

			var result = await CreateService(handler).SearchAsync(Query("亞滅培"));

			var formulations = result.Response!.Ingredients.Single().Formulations;
			Assert.Equal(2, formulations.Count);
			Assert.Equal(2, handler.RequestedUrls.Count(u => u.Contains("PesticideDetail")));
			// 劑型代碼要翻成中文，且 SG 是水溶性粒劑（不是水溶性粉劑——農藥名稱手冊那份資料在這裡是錯的）
			Assert.Contains(formulations, f => f.FormCode == "SG" && f.FormName == "水溶性粒劑");
			Assert.Contains(formulations, f => f.FormCode == "SP" && f.FormName == "水溶性粉劑");
		}

		// ── (4) contains 撈到多個成分 ──────────────────────────────────────

		[Fact]
		public async Task Search_關鍵字命中多個成分_依成分分組且標記完全符合者()
		{
			// 實測情境：查「加保扶」會一併撈到「丁基加保扶」（CARBOSULFAN，另一種農藥）
			var handler = new RouteHandler();
			handler.When("PesticideDataQueryType", () => JsonResponse(new
			{
				RS = "OK",
				Next = false,
				Data = new[]
				{
					License(permitNumber: "00201", chineseName: "丁基加保扶", pesticideCode: "I002", brandName: "好年冬"),
					License(permitNumber: "00004", chineseName: "加保扶", pesticideCode: "I024", brandName: "好速丹")
				}
			}));
			handler.When("PesticideDetail", () => JsonResponse(new[] { Usage() }));

			var result = await CreateService(handler).SearchAsync(Query("加保扶"));

			var ingredients = result.Response!.Ingredients;
			Assert.Equal(2, ingredients.Count);
			// 完全符合關鍵字的排最前面，但不完全符合的一樣要回傳（不做精確過濾）
			Assert.Equal("加保扶", ingredients[0].ChineseName);
			Assert.True(ingredients[0].IsExactMatch);
			Assert.Equal("丁基加保扶", ingredients[1].ChineseName);
			Assert.False(ingredients[1].IsExactMatch);
		}

		// ── (5) 原體不打第二層 ─────────────────────────────────────────────

		[Fact]
		public async Task Search_原體許可證_標記為原體且不打第二層()
		{
			var handler = new RouteHandler();
			handler.When("PesticideDataQueryType", () => JsonResponse(new
			{
				RS = "OK",
				Next = false,
				Data = new[]
				{
					License(permit: "農藥原進", permitNumber: "00631", brandName: "莫氏比",
						formCode: "", contents: "98.000 (%)")
				}
			}));

			var result = await CreateService(handler).SearchAsync(Query("亞滅培"));

			var formulation = result.Response!.Ingredients.Single().Formulations.Single();
			Assert.True(formulation.IsTechnicalGrade);
			Assert.Equal("原體", formulation.FormName);
			Assert.False(formulation.UsagesAvailable);
			Assert.Empty(formulation.Usages);
			// 原體的使用範圍實測一律回空陣列，不必浪費一次呼叫
			Assert.DoesNotContain(handler.RequestedUrls, u => u.Contains("PesticideDetail"));
		}

		// ── (6) 第二層的空 body 與失敗 ─────────────────────────────────────

		[Fact]
		public async Task Search_第二層回空Body_視為沒有核准用途而非失敗()
		{
			// 實測地雷：部分許可證回 HTTP 200 + Content-Type json + 完全空的 body（如 農藥製00044）
			var handler = new RouteHandler();
			handler.When("PesticideDataQueryType", () => JsonResponse(new
			{
				RS = "OK",
				Next = false,
				Data = new[] { License() }
			}));
			handler.When("PesticideDetail", () => RawResponse(string.Empty));

			var result = await CreateService(handler).SearchAsync(Query("亞滅培"));

			var formulation = result.Response!.Ingredients.Single().Formulations.Single();
			Assert.True(formulation.UsagesAvailable);   // 有查到，只是真的沒有用途
			Assert.Empty(formulation.Usages);
		}

		[Fact]
		public async Task Search_第二層失敗_許可證清單仍回傳但標記使用範圍不可用()
		{
			var handler = new RouteHandler();
			handler.When("PesticideDataQueryType", () => JsonResponse(new
			{
				RS = "OK",
				Next = false,
				Data = new[] { License() }
			}));
			handler.When("PesticideDetail", () => RawResponse("", HttpStatusCode.InternalServerError));

			var result = await CreateService(handler).SearchAsync(Query("亞滅培"));

			var formulation = result.Response!.Ingredients.Single().Formulations.Single();
			Assert.False(formulation.UsagesAvailable);  // 沒查到，跟「沒有用途」要能分辨
			Assert.Empty(formulation.Usages);
			Assert.Single(formulation.Licenses);        // 單支失敗不影響主結果
		}

		// ── (7) 未廢止但已到期 ─────────────────────────────────────────────

		[Fact]
		public async Task Search_未廢止但已到期_IsExpired為true且IsRevoked為false()
		{
			var handler = new RouteHandler();
			handler.When("PesticideDataQueryType", () => JsonResponse(new
			{
				RS = "OK",
				Next = false,
				Data = new[]
				{
					// 民國 105 年（2016）已過期，但 RevocationType 為空＝未廢止
					License(permitNumber: "02817", brandName: "金好鑽", expireDate: "105-11-09")
				}
			}));
			handler.When("PesticideDetail", () => JsonResponse(new[] { Usage() }));

			var result = await CreateService(handler).SearchAsync(Query("亞滅培"));

			var license = result.Response!.Ingredients.Single().Formulations.Single().Licenses.Single();
			Assert.True(license.IsExpired);
			Assert.False(license.IsRevoked);
			Assert.Equal("105-11-09", license.ExpireDateRoc);
			Assert.Equal(new DateOnly(2016, 11, 9), license.ExpireDate);
		}

		// ── 查無資料 ────────────────────────────────────────────────────────

		[Fact]
		public async Task Search_查無資料_回空集合而非null()
		{
			var handler = new RouteHandler();
			handler.When("PesticideDataQueryType", () => JsonResponse(new
			{
				RS = "OK",
				Next = false,
				Data = Array.Empty<object>()
			}));

			var result = await CreateService(handler).SearchAsync(Query("這不是農藥"));

			Assert.False(result.KeywordTooBroad);
			Assert.NotNull(result.Response);
			Assert.Empty(result.Response!.Ingredients);
		}

		// ── 第二層網址的 host 白名單 ───────────────────────────────────────

		[Theory]
		[InlineData("https://data.moa.gov.tw/Service/OpenData/FromM/PesticideDetail.aspx?ltyp=10&lno=04763", true)]
		[InlineData("http://data.moa.gov.tw/Service/OpenData/FromM/PesticideDetail.aspx", true)]
		[InlineData("https://DATA.MOA.GOV.TW/Service/OpenData/FromM/PesticideDetail.aspx", true)]
		[InlineData("https://evil.example.com/PesticideDetail.aspx", false)]
		[InlineData("https://data.moa.gov.tw.evil.example.com/x", false)]
		[InlineData("file:///C:/windows/system32/config", false)]
		[InlineData("/Service/OpenData/FromM/PesticideDetail.aspx", false)]  // 相對路徑不接受
		[InlineData("", false)]
		[InlineData(null, false)]
		public void IsAllowedDetailUrl_只接受農業部開放資料平台的絕對http網址(string? url, bool expected)
		{
			Assert.Equal(expected, PesticideService.IsAllowedDetailUrl(url));
		}

		// ── 英文成分名查詢 ─────────────────────────────────────────────────

		[Fact]
		public async Task Search_只填英文名_帶EnName參數且不帶ChineseName()
		{
			var handler = new RouteHandler();
			handler.When("PesticideDataQueryType", () => JsonResponse(new
			{
				RS = "OK",
				Next = false,
				Data = new[] { License() }
			}));
			handler.When("PesticideDetail", () => JsonResponse(new[] { Usage() }));

			var result = await CreateService(handler).SearchAsync(Query(englishName: "ACETAMIPRID"));

			var firstUrl = handler.RequestedUrls[0];
			Assert.Contains("EnName=ACETAMIPRID", firstUrl);
			Assert.DoesNotContain("ChineseName=", firstUrl);
			// 英文名全等（不分大小寫）也算完全符合
			Assert.True(result.Response!.Ingredients.Single().IsExactMatch);
		}

		[Fact]
		public async Task Search_中英文都填_兩個參數都帶給上游做交集()
		{
			var handler = new RouteHandler();
			handler.When("PesticideDataQueryType", () => JsonResponse(new
			{
				RS = "OK",
				Next = false,
				Data = new[] { License() }
			}));
			handler.When("PesticideDetail", () => JsonResponse(new[] { Usage() }));

			await CreateService(handler).SearchAsync(Query("亞滅培", "acetamiprid"));

			var firstUrl = handler.RequestedUrls[0];
			Assert.Contains("ChineseName=", firstUrl);
			Assert.Contains("EnName=acetamiprid", firstUrl);
		}

		[Fact]
		public async Task Search_英文名大小寫不同_仍判定為完全符合()
		{
			var handler = new RouteHandler();
			handler.When("PesticideDataQueryType", () => JsonResponse(new
			{
				RS = "OK",
				Next = false,
				Data = new[] { License() }        // EnName = "ACETAMIPRID"
			}));
			handler.When("PesticideDetail", () => JsonResponse(new[] { Usage() }));

			// 上游資料裡大小寫並不一致（ACETAMIPRID 與 metalaxyl-M 並存），比對必須不分大小寫
			var result = await CreateService(handler).SearchAsync(Query(englishName: "Acetamiprid"));

			Assert.True(result.Response!.Ingredients.Single().IsExactMatch);
		}

		// ── 英文名輸入白名單 ───────────────────────────────────────────────

		[Theory]
		[InlineData("ACETAMIPRID", true)]
		[InlineData("metalaxyl-M", true)]
		[InlineData("THIAMETHOXAM + CHLORANTRANILIPROLE", true)]   // 混合劑
		[InlineData("THIOPHANATE-METHYL + OXINE-COPPER", true)]
		[InlineData("Bacillus subtilis Y1336", true)]              // 含數字
		[InlineData("2,4-D", true)]                                // 逗號與連字號
		[InlineData("亞滅培", false)]                               // 中文
		[InlineData("ＡＣＥＴＡＭＩＰＲＩＤ", false)]                // 全形英文
		[InlineData("ACETAMIPRID亞滅培", false)]                    // 中英混合
		[InlineData("アセタミプリド", false)]                        // 日文假名
		[InlineData("ACETA​MIPRID", false)]                   // 零寬空格
		[InlineData("ACETAMIPRID　", false)]                   // 全形空白
		[InlineData("ACETAMIPRID😀", false)]                       // emoji
		[InlineData("ACETA\tMIPRID", false)]                       // 控制字元
		[InlineData("---", false)]                                 // 沒有任何英文字母
		[InlineData("123", false)]
		[InlineData("<script>", false)]
		public void IsValidEnglishName_只放行英數與化學命名符號(string value, bool expected)
		{
			// 白名單而非黑名單：沒有明確允許的字元一律擋下，
			// 不必逐一列舉全形／CJK／假名／emoji／零寬字元等「想得到的」危險輸入
			Assert.Equal(expected, PesticideSearchQueryDto.IsValidEnglishName(value));
		}

		// ── 劑型代碼對照 ───────────────────────────────────────────────────

		[Theory]
		[InlineData("SP", "水溶性粉劑")]
		[InlineData("SG", "水溶性粒劑")]   // 與農藥名稱手冊記載衝突時以許可證主檔為準
		[InlineData("EC", "乳劑")]
		[InlineData("UL", "超低容量液劑")] // 實測 41 張，初版對照表漏收
		[InlineData("TB", "片劑")]         // 官方用「片劑」不是國際標準直譯的「錠劑」
		[InlineData("CS", "膠囊懸著劑")]   // 官方用語，非「微囊懸浮劑」
		[InlineData("", "原體")]           // formCode 為空一律是原體許可證
		[InlineData("   ", "原體")]
		[InlineData(null, "原體")]
		[InlineData("ZZZ", "ZZZ")]         // 未收錄的代碼 fallback 顯示原碼，不顯示空白
		[InlineData("XX", "XX")]           // 實際存在但查不到中文名，同樣走 fallback
		public void PesticideForms_ToChineseName_未收錄代碼fallback顯示原碼(string? code, string expected)
		{
			Assert.Equal(expected, PesticideForms.ToChineseName(code));
		}

		[Fact]
		public async Task Search_第二層網址host不在白名單_略過抓取且不發出請求()
		{
			var handler = new RouteHandler();
			handler.When("PesticideDataQueryType", () => JsonResponse(new
			{
				RS = "OK",
				Next = false,
				Data = new[] { License(scopeOfUse: "https://evil.example.com/PesticideDetail.aspx") }
			}));

			var result = await CreateService(handler).SearchAsync(Query("亞滅培"));

			var formulation = result.Response!.Ingredients.Single().Formulations.Single();
			Assert.False(formulation.UsagesAvailable);
			Assert.DoesNotContain(handler.RequestedUrls, u => u.Contains("evil.example.com"));
		}
	}
}
