using System.Net.Http.Json;
using TaiwanAgri.Core.Constants;
using TaiwanAgri.Modules.FoodSafety.Data;
using TaiwanAgri.Modules.FoodSafety.Dtos.ApiResponses;
using TaiwanAgri.Modules.FoodSafety.Dtos.ExternalResponses;
using Microsoft.EntityFrameworkCore;
using TaiwanAgri.Core.Dtos;
using TaiwanAgri.Modules.FoodSafety.Dtos.Queries;

namespace TaiwanAgri.Modules.FoodSafety.Services
{
	public class FoodSafetyService : IFoodSafetyService
	{
		private readonly HttpClient _httpClient;
		private readonly FoodSafetyDbContext _context;

		public FoodSafetyService(IHttpClientFactory httpClientFactory, FoodSafetyDbContext context)
		{
			_httpClient = httpClientFactory.CreateClient("MoaApi");
			_context = context;
		}

		public async Task<PagedResult<OrganicCertificationResponseDto>> GetOrganicCertificationsAsync(OrganicCertificationQueryDto queryDto)
		{
			//Console.WriteLine($"[偵測] Page={queryDto.Page}, PageSize={queryDto.PageSize}, OperatorName={queryDto.OperatorName}");

			var query = _context.OrganicCertifications.AsQueryable();

			if (!string.IsNullOrWhiteSpace(queryDto.OperatorName))
				query = query.Where(x => x.Name.Contains(queryDto.OperatorName));

			if (!string.IsNullOrWhiteSpace(queryDto.VerificationBodyName))
				query = query.Where(x => x.CompanyName.Contains(queryDto.VerificationBodyName));

			if (!string.IsNullOrWhiteSpace(queryDto.ProductKeyword))
				query = query.Where(x => x.Products.Contains(queryDto.ProductKeyword) || x.ContainCrops.Contains(queryDto.ProductKeyword));
			
			var totalCount = await query.CountAsync();

			var items = await query
				.OrderByDescending(x => x.Id) //依照 Id 排序，確保分頁結果一致
				.Skip((queryDto.Page - 1) * queryDto.PageSize) //跳過幾頁
				.Take(queryDto.PageSize)
				.Select(x => new OrganicCertificationResponseDto
				{
					Id = x.Id,
					CertOrganicSn = x.CertOrganicSn,
					OperatorName = x.Name,
					Address = x.Address,
					Tel = x.Tel,
					Products = x.Products,
					BehaviorType = x.BehaviorType,
					VerificationBodyName = x.CompanyName,
					EffectiveDate = x.EffectiveDate,
					Status = x.Status,
					ProductScope = x.ContainCrops,
					MailingAddress = x.MailingAddress,
					LegacyCertNumber = x.OldCertOrganicSN,
					HasAmbiguousProductMapping = x.IsMultiCertSource
				})
				.ToListAsync();

			return new PagedResult<OrganicCertificationResponseDto>
			{
				Items = items,
				TotalCount = totalCount,
				Page = queryDto.Page,
				PageSize = queryDto.PageSize,
				TotalPages = (int)Math.Ceiling((double)totalCount / queryDto.PageSize)
			};
		}

		public async Task<PagedResult<ViolationResponseDto>> GetViolationsAsync(int days, string? inspectResult = null, int page = 1, int pageSize = 20)
		{
			var fromDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-days);

			var targetDate = _context.PesticideViolations
				.Where(v => v.SamplingDate >= fromDate);

			if (inspectResult != null)
			{
				targetDate = targetDate.Where(v => v.InspectResult == inspectResult);
			}

			var totalCount = await targetDate.CountAsync();
			var items = await targetDate
				.Skip((page - 1) * pageSize)
				.Take(pageSize)
				.Select(v => new ViolationResponseDto
				{
					Number = v.Number,
					SamplingDate = v.SamplingDate,
					ProductName = v.ProductName,
					ProducerName = v.ProducerName,
					SamplingLocation = v.SamplingLocation,
					InspectResult = v.InspectResult,
					Note = v.Note
				})
				.ToListAsync();

			return new PagedResult<ViolationResponseDto>
			{
				Items = items,
				TotalCount = totalCount,
				Page = page,
				PageSize = pageSize,
				TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
			};
		}

		public async Task<TraceabilityResponseDto> SearchTraceabilityAsync(string traceCode)
		{
			// ── 1. 同時發出四支 API 請求 ─────────────────────────────────
			// 每個 Task 各自打一支 API，四個同時跑
			// 用 SafeFetch 包住，確保單支失敗不影響其他支

			var productTask = SafeFetch<AgriProductApiResponse>(
				$"{MoaApiEndpoints.AgriProductInfo}?TraceCode={traceCode}");

			var producerTask = SafeFetch<AgriProducerApiResponse>(
				$"{MoaApiEndpoints.AgriProducerInfo}?TraceCode={traceCode}");

			// 洗選蛋：用比 traceCode 小的起始值讓目標批次出現在結果裡
			// API 的 Traceno_Start 參數是「>= 過濾」，後端再做區間包含比對
			var eggStartParam = traceCode.Length >= 4
				? traceCode[..^4] + "0000"
				: traceCode;

			var eggTask = SafeFetch<WashedEggApiResponse>(
				$"{MoaApiEndpoints.WashedEggs}?Traceno_Start={eggStartParam}");

			// 禽肉：同洗選蛋策略
			var poultryStartParam = traceCode.Length >= 4
				? traceCode[..^4] + "0000"
				: traceCode;

			var poultryTask = SafeFetch<PoultryApiResponse>(
				$"{MoaApiEndpoints.DomesticPoultry}?Traceno_Start={poultryStartParam}");

			// ── 2. 等四支全部回來 ────────────────────────────────────────
			// Task.WhenAll：同時等待，總時間 = 最慢那支，不是四支加總
			await Task.WhenAll(productTask, producerTask, eggTask, poultryTask);

			// ── 3. 取出各自結果（此時已完成，直接 .Result 不會 block）────
			var productResp = productTask.Result;
			var producerResp = producerTask.Result;
			var eggResp = eggTask.Result;
			var poultryResp = poultryTask.Result;

			// ── 4. 組裝回傳給前端的 DTO ─────────────────────────────────
			return new TraceabilityResponseDto
			{
				TraceCode = traceCode,

				// 農產品：一個追溯碼可能對應多種作物，取第一頁所有筆
				AgriProducts = productResp?.Data
					.Where(d => d.TraceCode == traceCode)
					.Select(d => new AgriProductResultDto
					{
						Product = d.Product,
						Place = d.Place,
						Mark = d.Mark
					})
					.ToList(),

				// 生產者：取第一筆（同一追溯碼通常只有一個生產者）
				Producer = producerResp?.Data
					.FirstOrDefault(d => d.TraceCode == traceCode) is { } p
					? new AgriProducerResultDto
					{
						Producer = p.Producer,
						Address = p.Address,
						Mark = p.Mark,
						Status = p.Status,
						Description = p.Description
					}
					: null,

				// 洗選蛋：API 回傳「起始碼 >= eggStartParam」的批次
				// 用字串比較找出包含 traceCode 的那一筆
				WashedEgg = eggResp?.Data.FirstOrDefault(e =>
					string.Compare(e.TracenoStart, traceCode, StringComparison.Ordinal) <= 0 &&
					string.Compare(e.TracenoEnd, traceCode, StringComparison.Ordinal) >= 0
				) is { } e
					? new WashedEggResultDto
					{
						TracenoStart = e.TracenoStart,
						TracenoEnd = e.TracenoEnd,
						SelName = e.SelName,
						SelAddr = e.SelAddr,
						SelBoss = e.SelBoss,
						EggName1 = e.EggName1,
						FarTownName1 = e.FarTownName1,
						EggName2 = e.EggName2,
						FarTownName2 = e.FarTownName2,
						EggName3 = e.EggName3,
						FarTownName3 = e.FarTownName3
					}
					: null,

				// 禽肉：同洗選蛋策略，字串比較找包含 traceCode 的批次
				Poultry = poultryResp?.Data.FirstOrDefault(pt =>
					string.Compare(pt.TracenoStart, traceCode, StringComparison.Ordinal) <= 0 &&
					string.Compare(pt.TracenoEnd, traceCode, StringComparison.Ordinal) >= 0
				) is { } pt
					? new PoultryResultDto
					{
						TracenoStart = pt.TracenoStart,
						TracenoEnd = pt.TracenoEnd,
						KilName = pt.KilName,
						KilAddr = pt.KilAddr,
						KilBoss = pt.KilBoss,
						FarmersName1 = pt.FarmersName1,
						FarmersType1 = pt.FarmersType1,
						Farmersplace1 = pt.Farmersplace1,
						FarmersName2 = pt.FarmersName2,
						FarmersType2 = pt.FarmersType2,
						Farmersplace2 = pt.Farmersplace2,
						Cdate = pt.Cdate
					}
					: null
			};
		}

		// ── 私有輔助方法：安全打 API，失敗回傳 null 而非拋例外 ──────────
		private async Task<T?> SafeFetch<T>(string url) where T : class
		{
			try
			{
				var response = await _httpClient.GetAsync(url);
				if (!response.IsSuccessStatusCode)
					return null;

				return await response.Content.ReadFromJsonAsync<T>();
			}
			catch
			{
				// 網路錯誤、timeout、反序列化失敗都回傳 null
				// 讓其他三支 API 的結果仍然可以正常回傳
				return null;
			}
		}
	}
}