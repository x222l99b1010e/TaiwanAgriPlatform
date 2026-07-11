using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TaiwanAgri.Modules.FoodSafety.Data;
using TaiwanAgri.Modules.FoodSafety.Dtos.ExternalResponses;
using TaiwanAgri.Modules.FoodSafety.Services;

namespace TaiwanAgri.Tests.FoodSafety
{
	/// <summary>
	/// SearchTraceabilityAsync 的單元測試。
	/// 用假的 HttpMessageHandler 依 URL 回應不同 JSON，
	/// 不需要真的打農業部 API，即可驗證：
	/// (1) 後四位歸零的查詢參數、(2) 區間包含比對、(3) SafeFetch 單支失敗隔離
	/// </summary>
	public class TraceabilitySearchTests
	{
		// ── 測試替身：依 URL 前綴決定回應的 HttpMessageHandler ─────────────

		private sealed class RouteHandler : HttpMessageHandler
		{
			private readonly List<(string PathContains, Func<HttpResponseMessage> Respond)> _routes = new();
			public List<string> RequestedUrls { get; } = new();

			public void When(string pathContains, Func<HttpResponseMessage> respond)
				=> _routes.Add((pathContains, respond));

			protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
			{
				var url = request.RequestUri!.ToString();
				RequestedUrls.Add(url);

				foreach (var (pathContains, respond) in _routes)
				{
					if (url.Contains(pathContains))
						return Task.FromResult(respond());
				}
				// 沒設定的路由一律回空的 OK 回應
				return Task.FromResult(JsonResponse(new { RS = "OK", Data = Array.Empty<object>(), Next = false }));
			}
		}

		private static HttpResponseMessage JsonResponse(object payload)
			=> new(HttpStatusCode.OK)
			{
				Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
			};

		private static FoodSafetyService CreateService(RouteHandler handler)
		{
			var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://data.moa.gov.tw/") };
			var mockFactory = new Mock<IHttpClientFactory>();
			mockFactory.Setup(f => f.CreateClient("MoaApi")).Returns(httpClient);

			var options = new DbContextOptionsBuilder<FoodSafetyDbContext>()
				.UseInMemoryDatabase($"TraceabilityTest_{Guid.NewGuid()}")
				.Options;

			return new FoodSafetyService(mockFactory.Object, new FoodSafetyDbContext(options),
				NullLogger<FoodSafetyService>.Instance, TimeProvider.System);
		}

		// ── NormalizeTracenoStart：純函式，直接驗證 ─────────────────────────

		[Theory]
		[InlineData("2411010012345", "2411010010000")] // 一般長度：後四位歸零
		[InlineData("12345", "10000")]
		[InlineData("1234", "0000")]                   // 恰好 4 位：全歸零
		[InlineData("123", "123")]                     // 不足 4 位：原樣回傳
		public void NormalizeTracenoStart_ZeroesLastFourDigits(string traceCode, string expected)
		{
			Assert.Equal(expected, FoodSafetyService.NormalizeTracenoStart(traceCode));
		}

		// ── 區間包含比對 ────────────────────────────────────────────────────

		[Fact]
		public async Task SearchTraceability_EggBatchContainsCode_ReturnsThatBatch()
		{
			// Arrange：兩個批次，traceCode 只落在第二個批次的區間內
			var handler = new RouteHandler();
			handler.When("WashedEggsTraceabilityType", () => JsonResponse(new
			{
				RS = "OK",
				Next = false,
				Data = new[]
				{
					new { Traceno_Start = "2411010010000", Traceno_End = "2411010011999", Sel_Name = "甲洗選場" },
					new { Traceno_Start = "2411010012000", Traceno_End = "2411010013999", Sel_Name = "乙洗選場" }
				}
			}));
			var service = CreateService(handler);

			// Act
			var result = await service.SearchTraceabilityAsync("2411010012345");

			// Assert：命中乙洗選場的批次
			Assert.NotNull(result.WashedEgg);
			Assert.Equal("乙洗選場", result.WashedEgg!.SelName);
			Assert.Equal("2411010012000", result.WashedEgg.TracenoStart);

			// 驗證查詢參數確實是後四位歸零的起始值
			var eggUrl = handler.RequestedUrls.Single(u => u.Contains("WashedEggsTraceabilityType"));
			Assert.Contains("Traceno_Start=2411010010000", eggUrl);
		}

		[Fact]
		public async Task SearchTraceability_CodeOutsideAllBatches_ReturnsNullEgg()
		{
			// Arrange：批次區間都在 traceCode 之前
			var handler = new RouteHandler();
			handler.When("WashedEggsTraceabilityType", () => JsonResponse(new
			{
				RS = "OK",
				Next = false,
				Data = new[]
				{
					new { Traceno_Start = "2411010010000", Traceno_End = "2411010011999", Sel_Name = "甲洗選場" }
				}
			}));
			var service = CreateService(handler);

			// Act
			var result = await service.SearchTraceabilityAsync("2411010015000");

			// Assert：沒有任何批次包含此碼
			Assert.Null(result.WashedEgg);
		}

		// ── SafeFetch 容錯隔離 ──────────────────────────────────────────────

		[Fact]
		public async Task SearchTraceability_OneSourceFails_OthersStillReturned()
		{
			// Arrange：農產品 API 回 500，其餘正常
			var handler = new RouteHandler();
			handler.When("TWAgriProductsTraceabilityType_ProductInfo",
				() => new HttpResponseMessage(HttpStatusCode.InternalServerError));
			handler.When("WashedEggsTraceabilityType", () => JsonResponse(new
			{
				RS = "OK",
				Next = false,
				Data = new[]
				{
					new { Traceno_Start = "2411010010000", Traceno_End = "2411010019999", Sel_Name = "甲洗選場" }
				}
			}));
			var service = CreateService(handler);

			// Act：不應拋例外
			var result = await service.SearchTraceabilityAsync("2411010012345");

			// Assert：失敗的來源為 null，成功的來源正常回傳
			Assert.Null(result.AgriProducts);
			Assert.NotNull(result.WashedEgg);
			Assert.Equal("甲洗選場", result.WashedEgg!.SelName);
		}

		[Fact]
		public async Task SearchTraceability_AgriProduct_FiltersByExactTraceCode()
		{
			// Arrange：API 回傳兩筆，只有一筆的 TraceCode 精確等於查詢碼
			var handler = new RouteHandler();
			handler.When("TWAgriProductsTraceabilityType_ProductInfo", () => JsonResponse(new
			{
				RS = "OK",
				Next = false,
				Data = new[]
				{
					new { TraceCode = "1060107000001", Product = "高麗菜", Place = "彰化", Mark = "" },
					new { TraceCode = "1060107999999", Product = "青江菜", Place = "雲林", Mark = "" }
				}
			}));
			var service = CreateService(handler);

			// Act
			var result = await service.SearchTraceabilityAsync("1060107000001");

			// Assert：只留下精確比對的那筆
			Assert.NotNull(result.AgriProducts);
			var product = Assert.Single(result.AgriProducts!);
			Assert.Equal("高麗菜", product.Product);
		}
	}
}
