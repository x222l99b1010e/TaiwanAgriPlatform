using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TaiwanAgri.Modules.FoodSafety.Data;
using TaiwanAgri.Modules.FoodSafety.Dtos.Queries;
using TaiwanAgri.Modules.FoodSafety.Entities;
using TaiwanAgri.Modules.FoodSafety.Services;

namespace TaiwanAgri.Tests.FoodSafety
{
	public class FoodSafetyServiceTests
	{
		[Fact]
		public async Task GetOrganicCertificationsAsync_NoFilters_ReturnsAllPaged()
		{
			// ── Arrange ──────────────────────────────────────────

			// 1. 建立 InMemory DB
			var options = new DbContextOptionsBuilder<FoodSafetyDbContext>()
				.UseInMemoryDatabase("TestDb_NoFilter_GetOrganicCert")
				.Options;
			var dbContext = new FoodSafetyDbContext(options);

			// 2. 準備 3 筆假資料，Id 故意給 1/2/3，方便預測排序後的順序
			var entities = new List<OrganicCertification>
			{
				new OrganicCertification
				{
					Id = 1,
					CertOrganicSn = "SN-001",
					Name = "業者A",
					Address = "台北市信義區",
					Tel = "02-1111-1111",
					Products = "水稻",
					BehaviorType = "生產",
					CompanyName = "驗證機構A",
					EffectiveDate = new DateOnly(2026, 1, 1),
					Status = "有效",
					ContainCrops = "水稻、玉米",
					MailingAddress = "台北市信義區",
					OldCertOrganicSn = "OLD-001",
					IsMultiCertSource = false
				},
				new OrganicCertification
				{
					Id = 2,
					CertOrganicSn = "SN-002",
					Name = "業者B",
					Address = "台中市西區",
					Tel = "04-2222-2222",
					Products = "茶葉",
					BehaviorType = "生產",
					CompanyName = "驗證機構B",
					EffectiveDate = new DateOnly(2026, 2, 1),
					Status = "有效",
					ContainCrops = "茶葉",
					MailingAddress = "台中市西區",
					OldCertOrganicSn = "OLD-002",
					IsMultiCertSource = false
				},
				new OrganicCertification
				{
					Id = 3,
					CertOrganicSn = "SN-003",
					Name = "業者C",
					Address = "高雄市三民區",
					Tel = "07-3333-3333",
					Products = "蔬菜",
					BehaviorType = "生產",
					CompanyName = "驗證機構C",
					EffectiveDate = new DateOnly(2026, 3, 1),
					Status = "有效",
					ContainCrops = "蔬菜、水果",
					MailingAddress = "高雄市三民區",
					OldCertOrganicSn = "OLD-003",
					IsMultiCertSource = false
				}
			};
			await dbContext.OrganicCertifications.AddRangeAsync(entities);
			await dbContext.SaveChangesAsync();

			// 3. Mock IHttpClientFactory（建構子需要，但這個方法不會用到，給空殼即可）
			var mockHttpClientFactory = new Mock<IHttpClientFactory>();
			mockHttpClientFactory
				.Setup(f => f.CreateClient("MoaApi"))
				.Returns(new HttpClient());

			// 4. 建立被測對象
			var service = new FoodSafetyService(mockHttpClientFactory.Object, dbContext, NullLogger<FoodSafetyService>.Instance, TimeProvider.System);

			// ── Act ──────────────────────────────────────────────

			// 5. 呼叫 Page=1
			var page1Result = await service.GetOrganicCertificationsAsync(
				new OrganicCertificationQueryDto { Page = 1, PageSize = 1 });

			// 6. 呼叫 Page=2
			var page2Result = await service.GetOrganicCertificationsAsync(
				new OrganicCertificationQueryDto { Page = 2, PageSize = 1 });

			// 7. 呼叫 Page=3
			var page3Result = await service.GetOrganicCertificationsAsync(
				new OrganicCertificationQueryDto { Page = 3, PageSize = 1 });

			// ── Assert ───────────────────────────────────────────

			// 8. 驗證 TotalCount：三次呼叫，資料庫總筆數應該都是 3
			//    （不受 Page/PageSize 影響，這是「全部資料」這件事的證明）
			Assert.Equal(3, page1Result.TotalCount);
			Assert.Equal(3, page2Result.TotalCount);
			Assert.Equal(3, page3Result.TotalCount);

			// 9. 驗證 TotalPages：3 筆資料、PageSize=1 → 應該是 3 頁
			Assert.Equal(3, page1Result.TotalPages);

			// 10. 驗證每頁筆數：PageSize=1，每次應該只拿到 1 筆
			Assert.Single(page1Result.Items);
			Assert.Single(page2Result.Items);
			Assert.Single(page3Result.Items);

			// 11. 驗證排序＋分頁正確：OrderByDescending(Id) → Id=3,2,1
			//     Page=1 應該拿到 Id=3（業者C）
			//     Page=2 應該拿到 Id=2（業者B）
			//     Page=3 應該拿到 Id=1（業者A）
			Assert.Equal("業者C", page1Result.Items[0].OperatorName);
			Assert.Equal("業者B", page2Result.Items[0].OperatorName);
			Assert.Equal("業者A", page3Result.Items[0].OperatorName);
		}

		[Fact]
		public async Task GetOrganicCertificationsAsync_FilterByOperatorName_ReturnsMatchedOnly()
		{
			// ── Arrange ──────────────────────────────────────────
			var options = new DbContextOptionsBuilder<FoodSafetyDbContext>()
				.UseInMemoryDatabase("TestDb_FilterOperatorName_GetOrganicCert")
				.Options;
			var dbContext = new FoodSafetyDbContext(options);

			var entities = new List<OrganicCertification>
				{
					new OrganicCertification { Id = 1, CertOrganicSn = "SN-001", Name = "業者A", Address = "台北市信義區", Tel = "02-1111-1111", Products = "水稻", BehaviorType = "生產", CompanyName = "驗證機構A", EffectiveDate = new DateOnly(2026,1,1), Status = "有效", ContainCrops = "水稻、玉米", MailingAddress = "台北市信義區", OldCertOrganicSn = "OLD-001", IsMultiCertSource = false },
					new OrganicCertification { Id = 2, CertOrganicSn = "SN-002", Name = "業者B", Address = "台中市西區", Tel = "04-2222-2222", Products = "茶葉", BehaviorType = "生產", CompanyName = "驗證機構B", EffectiveDate = new DateOnly(2026,2,1), Status = "有效", ContainCrops = "茶葉", MailingAddress = "台中市西區", OldCertOrganicSn = "OLD-002", IsMultiCertSource = false },
					new OrganicCertification { Id = 3, CertOrganicSn = "SN-003", Name = "業者C", Address = "高雄市三民區", Tel = "07-3333-3333", Products = "蔬菜", BehaviorType = "生產", CompanyName = "驗證機構C", EffectiveDate = new DateOnly(2026,3,1), Status = "有效", ContainCrops = "蔬菜、水果", MailingAddress = "高雄市三民區", OldCertOrganicSn = "OLD-003", IsMultiCertSource = false }
				};
			await dbContext.OrganicCertifications.AddRangeAsync(entities);
			await dbContext.SaveChangesAsync();

			var mockHttpClientFactory = new Mock<IHttpClientFactory>();
			mockHttpClientFactory.Setup(f => f.CreateClient("MoaApi")).Returns(new HttpClient());
			var service = new FoodSafetyService(mockHttpClientFactory.Object, dbContext, NullLogger<FoodSafetyService>.Instance, TimeProvider.System);

			// ── Act ──────────────────────────────────────────────
			// 傳完整字串「業者A」，只會篩出這一筆；若傳部分字串「業者」會篩出全部3筆
			var result = await service.GetOrganicCertificationsAsync(
				new OrganicCertificationQueryDto { Page = 1, PageSize = 10, OperatorName = "業者A" });

			// ── Assert ───────────────────────────────────────────
			Assert.Equal(1, result.TotalCount);
			Assert.Single(result.Items);
			Assert.Equal("業者A", result.Items[0].OperatorName);
		}

		[Fact]
		public async Task GetOrganicCertificationsAsync_MultipleFilters_AppliesAndLogic()
		{
			// ── Arrange ──────────────────────────────────────────
			var options = new DbContextOptionsBuilder<FoodSafetyDbContext>()
				.UseInMemoryDatabase("TestDb_MultipleFilters_GetOrganicCert")
				.Options;
			var dbContext = new FoodSafetyDbContext(options);

			var entities = new List<OrganicCertification>
				{
					new OrganicCertification { Id = 1, CertOrganicSn = "SN-001", Name = "業者A", Address = "台北市信義區", Tel = "02-1111-1111", Products = "水稻", BehaviorType = "生產", CompanyName = "驗證機構A", EffectiveDate = new DateOnly(2026,1,1), Status = "有效", ContainCrops = "水稻、玉米", MailingAddress = "台北市信義區", OldCertOrganicSn = "OLD-001", IsMultiCertSource = false },
					new OrganicCertification { Id = 2, CertOrganicSn = "SN-002", Name = "業者B", Address = "台中市西區", Tel = "04-2222-2222", Products = "茶葉", BehaviorType = "生產", CompanyName = "驗證機構B", EffectiveDate = new DateOnly(2026,2,1), Status = "有效", ContainCrops = "茶葉", MailingAddress = "台中市西區", OldCertOrganicSn = "OLD-002", IsMultiCertSource = false },
					new OrganicCertification { Id = 3, CertOrganicSn = "SN-003", Name = "業者C", Address = "高雄市三民區", Tel = "07-3333-3333", Products = "蔬菜", BehaviorType = "生產", CompanyName = "驗證機構C", EffectiveDate = new DateOnly(2026,3,1), Status = "有效", ContainCrops = "蔬菜、水果", MailingAddress = "高雄市三民區", OldCertOrganicSn = "OLD-003", IsMultiCertSource = false }
				};
			await dbContext.OrganicCertifications.AddRangeAsync(entities);
			await dbContext.SaveChangesAsync();

			var mockHttpClientFactory = new Mock<IHttpClientFactory>();
			mockHttpClientFactory.Setup(f => f.CreateClient("MoaApi")).Returns(new HttpClient());
			var service = new FoodSafetyService(mockHttpClientFactory.Object, dbContext, NullLogger<FoodSafetyService>.Instance, TimeProvider.System);

			// ── Act ──────────────────────────────────────────────
			// OperatorName="業者"（部分比對，符合全部3筆）
			// VerificationBodyName="驗證機構B"（只符合業者B）
			// AND 邏輯下，交集應該只剩業者B這一筆——這才證明是 AND 而非 OR
			var result = await service.GetOrganicCertificationsAsync(
				new OrganicCertificationQueryDto
				{
					Page = 1,
					PageSize = 10,
					OperatorName = "業者",
					VerificationBodyName = "驗證機構B"
				});

			// ── Assert ───────────────────────────────────────────
			Assert.Equal(1, result.TotalCount);
			Assert.Single(result.Items);
			Assert.Equal("業者B", result.Items[0].OperatorName);
			Assert.Equal("驗證機構B", result.Items[0].VerificationBodyName);
		}

		[Fact]
		public async Task GetOrganicCertificationsAsync_ProductKeyword_MatchesProductsOrProductScope()
		{
			// ── Arrange ──────────────────────────────────────────
			var options = new DbContextOptionsBuilder<FoodSafetyDbContext>()
				.UseInMemoryDatabase("TestDb_ProductKeyword_GetOrganicCert")
				.Options;
			var dbContext = new FoodSafetyDbContext(options);

			var entities = new List<OrganicCertification>
				{
					new OrganicCertification { Id = 1, CertOrganicSn = "SN-001", Name = "業者A", Address = "台北市信義區", Tel = "02-1111-1111", Products = "水稻", BehaviorType = "生產", CompanyName = "驗證機構A", EffectiveDate = new DateOnly(2026,1,1), Status = "有效", ContainCrops = "水稻、玉米", MailingAddress = "台北市信義區", OldCertOrganicSn = "OLD-001", IsMultiCertSource = false },
					new OrganicCertification { Id = 2, CertOrganicSn = "SN-002", Name = "業者B", Address = "台中市西區", Tel = "04-2222-2222", Products = "茶葉", BehaviorType = "生產", CompanyName = "驗證機構B", EffectiveDate = new DateOnly(2026,2,1), Status = "有效", ContainCrops = "茶葉", MailingAddress = "台中市西區", OldCertOrganicSn = "OLD-002", IsMultiCertSource = false },
					new OrganicCertification { Id = 3, CertOrganicSn = "SN-003", Name = "業者C", Address = "高雄市三民區", Tel = "07-3333-3333", Products = "蔬菜", BehaviorType = "生產", CompanyName = "驗證機構C", EffectiveDate = new DateOnly(2026,3,1), Status = "有效", ContainCrops = "蔬菜、水果", MailingAddress = "高雄市三民區", OldCertOrganicSn = "OLD-003", IsMultiCertSource = false }
				};
			await dbContext.OrganicCertifications.AddRangeAsync(entities);
			await dbContext.SaveChangesAsync();

			var mockHttpClientFactory = new Mock<IHttpClientFactory>();
			mockHttpClientFactory.Setup(f => f.CreateClient("MoaApi")).Returns(new HttpClient());
			var service = new FoodSafetyService(mockHttpClientFactory.Object, dbContext, NullLogger<FoodSafetyService>.Instance, TimeProvider.System);

			// ── Act ──────────────────────────────────────────────
			// 「玉米」只存在於業者A的 ContainCrops（水稻、玉米），Products 沒有 → 驗證 ContainCrops 這條 OR 分支
			var resultFromContainCrops = await service.GetOrganicCertificationsAsync(
				new OrganicCertificationQueryDto { Page = 1, PageSize = 10, ProductKeyword = "玉米" });

			// 「蔬菜」存在於業者C的 Products → 驗證 Products 這條 OR 分支
			var resultFromProducts = await service.GetOrganicCertificationsAsync(
				new OrganicCertificationQueryDto { Page = 1, PageSize = 10, ProductKeyword = "蔬菜" });

			// ── Assert ───────────────────────────────────────────
			Assert.Equal(1, resultFromContainCrops.TotalCount);
			Assert.Equal("業者A", resultFromContainCrops.Items[0].OperatorName);

			Assert.Equal(1, resultFromProducts.TotalCount);
			Assert.Equal("業者C", resultFromProducts.Items[0].OperatorName);
		}

		// ── GetViolationsAsync ───────────────────────────────────────

		private static FoodSafetyDbContext CreateViolationDb(string dbName)
		{
			var options = new DbContextOptionsBuilder<FoodSafetyDbContext>()
				.UseInMemoryDatabase(dbName)
				.Options;
			var dbContext = new FoodSafetyDbContext(options);

			// 取樣日期用相對今天的近期值，確保落在預設 days=90 視窗內
			var recentDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-3);
			dbContext.PesticideViolations.AddRange(
				new PesticideViolation { Id = 1, Number = "V-001", SamplingDate = recentDate, ProductName = "青江菜", ProducerName = "產戶A", SamplingLocation = "台北市", InspectResult = "不合格", Note = "" },
				new PesticideViolation { Id = 2, Number = "V-002", SamplingDate = recentDate, ProductName = "小白菜", ProducerName = "產戶B", SamplingLocation = "新北市", InspectResult = "與規定不符", Note = "" });
			dbContext.SaveChanges();
			return dbContext;
		}

		private static FoodSafetyService CreateService(FoodSafetyDbContext dbContext)
		{
			var mockHttpClientFactory = new Mock<IHttpClientFactory>();
			mockHttpClientFactory.Setup(f => f.CreateClient("MoaApi")).Returns(new HttpClient());
			return new FoodSafetyService(mockHttpClientFactory.Object, dbContext, NullLogger<FoodSafetyService>.Instance, TimeProvider.System);
		}

		[Fact]
		public async Task GetViolationsAsync_EmptyOrWhitespaceInspectResult_IgnoresFilter()
		{
			// ── Arrange ──────────────────────────────────────────
			var dbContext = CreateViolationDb("TestDb_EmptyInspectResult_GetViolations");
			var service = CreateService(dbContext);

			// ── Act ──────────────────────────────────────────────
			// 客戶端送 ?inspectResult=（空字串）或全空白時，應視同「未指定」而非過濾 InspectResult == ""
			var emptyResult = await service.GetViolationsAsync(
				new ViolationQueryDto { Days = 90, InspectResult = "" });
			var whitespaceResult = await service.GetViolationsAsync(
				new ViolationQueryDto { Days = 90, InspectResult = "  " });

			// ── Assert ───────────────────────────────────────────
			Assert.Equal(2, emptyResult.TotalCount);
			Assert.Equal(2, whitespaceResult.TotalCount);
		}

		[Fact]
		public async Task GetViolationsAsync_InspectResultFilter_ReturnsMatchedOnly()
		{
			// ── Arrange ──────────────────────────────────────────
			var dbContext = CreateViolationDb("TestDb_FilterInspectResult_GetViolations");
			var service = CreateService(dbContext);

			// ── Act ──────────────────────────────────────────────
			var result = await service.GetViolationsAsync(
				new ViolationQueryDto { Days = 90, InspectResult = "不合格" });

			// ── Assert ───────────────────────────────────────────
			// 對照組：有值時過濾仍要生效，證明空字串修正沒有把過濾整個關掉
			Assert.Equal(1, result.TotalCount);
			Assert.Equal("V-001", result.Items[0].Number);
		}

		/// <summary>固定時刻的 TimeProvider，讓「近 N 天」邊界測試可重現</summary>
		private sealed class FixedTimeProvider : TimeProvider
		{
			private readonly DateTimeOffset _utcNow;
			public FixedTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;
			public override DateTimeOffset GetUtcNow() => _utcNow;
		}

		[Fact]
		public async Task GetViolationsAsync_近N天邊界_以台灣時區日界計算()
		{
			// ── Arrange ──────────────────────────────────────────
			// UTC 2026-07-10 18:00 = 台灣 2026-07-11 02:00（已跨日）：
			// 台灣日界的「今天」是 7/11；若誤用 UTC 日界會算成 7/10
			var clock = new FixedTimeProvider(new DateTimeOffset(2026, 7, 10, 18, 0, 0, TimeSpan.Zero));

			var options = new DbContextOptionsBuilder<FoodSafetyDbContext>()
				.UseInMemoryDatabase("TestDb_TaiwanDayBoundary_GetViolations")
				.Options;
			var dbContext = new FoodSafetyDbContext(options);
			dbContext.PesticideViolations.AddRange(
				new PesticideViolation { Id = 1, Number = "V-IN", SamplingDate = new DateOnly(2026, 7, 10), ProductName = "青江菜", ProducerName = "產戶A", SamplingLocation = "台北市", InspectResult = "不合格", Note = "" },
				new PesticideViolation { Id = 2, Number = "V-OUT", SamplingDate = new DateOnly(2026, 7, 9), ProductName = "小白菜", ProducerName = "產戶B", SamplingLocation = "新北市", InspectResult = "不合格", Note = "" });
			dbContext.SaveChanges();

			var mockHttpClientFactory = new Mock<IHttpClientFactory>();
			mockHttpClientFactory.Setup(f => f.CreateClient("MoaApi")).Returns(new HttpClient());
			var service = new FoodSafetyService(mockHttpClientFactory.Object, dbContext, NullLogger<FoodSafetyService>.Instance, clock);

			// ── Act ──────────────────────────────────────────────
			// Days=1 → fromDate = 台灣今天(7/11) - 1 = 7/10
			var result = await service.GetViolationsAsync(new ViolationQueryDto { Days = 1 });

			// ── Assert ───────────────────────────────────────────
			// 只有 7/10 這筆入選；若仍以 UTC 日界計算（fromDate = 7/09），7/09 那筆也會被誤納
			Assert.Equal(1, result.TotalCount);
			Assert.Equal("V-IN", result.Items[0].Number);
		}
	}
}