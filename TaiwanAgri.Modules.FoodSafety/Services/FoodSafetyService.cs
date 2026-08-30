using TaiwanAgri.Core.Helpers;
using TaiwanAgri.Modules.FoodSafety.Data;
using TaiwanAgri.Modules.FoodSafety.Dtos.ApiResponses;
using Microsoft.EntityFrameworkCore;
using TaiwanAgri.Core.Dtos;
using TaiwanAgri.Modules.FoodSafety.Dtos.Queries;

namespace TaiwanAgri.Modules.FoodSafety.Services
{
	/// <summary>
	/// 食安模組的落地資料查詢（有機驗證、農藥違規），只碰 DB、不打外部 API。
	/// 追溯碼查詢是即時打農業部 API、不落地的另一種形態，已分出 TraceabilityService。
	/// </summary>
	public class FoodSafetyService : IFoodSafetyService
	{
		private readonly FoodSafetyDbContext _context;
		private readonly TimeProvider _timeProvider;

		public FoodSafetyService(FoodSafetyDbContext context, TimeProvider timeProvider)
		{
			_context = context;
			_timeProvider = timeProvider;
		}

		public async Task<PagedResult<OrganicCertificationResponseDto>> GetOrganicCertificationsAsync(OrganicCertificationQueryDto queryDto)
		{
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
					LegacyCertNumber = x.OldCertOrganicSn,
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

		public async Task<PagedResult<ViolationResponseDto>> GetViolationsAsync(ViolationQueryDto queryDto)
		{
			// 「近 N 天」以台灣時區日界計算（UtcNow 慢 8 小時，日界前後會差一天）
			var fromDate = TaiwanTime.Today(_timeProvider).AddDays(-queryDto.Days);

			var violationsQuery = _context.PesticideViolations
				.Where(v => v.SamplingDate >= fromDate);

			// 空字串/空白視同未過濾：客戶端送 ?inspectResult= 時不應變成 InspectResult == "" 而靜默回空頁
			if (!string.IsNullOrWhiteSpace(queryDto.InspectResult))
			{
				violationsQuery = violationsQuery.Where(v => v.InspectResult == queryDto.InspectResult);
			}

			var totalCount = await violationsQuery.CountAsync();
			var items = await violationsQuery
				// 同日多筆時以 Id 決勝，確保翻頁時同一筆不會重複出現或消失
				.OrderByDescending(v => v.SamplingDate)
				.ThenByDescending(v => v.Id)
				.Skip((queryDto.Page - 1) * queryDto.PageSize)
				.Take(queryDto.PageSize)
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
				Page = queryDto.Page,
				PageSize = queryDto.PageSize,
				TotalPages = (int)Math.Ceiling((double)totalCount / queryDto.PageSize)
			};
		}
	}
}