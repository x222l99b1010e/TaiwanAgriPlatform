using Microsoft.EntityFrameworkCore;
using TaiwanAgri.Core.Dtos;
using TaiwanAgri.Core.Helpers;
using TaiwanAgri.Modules.Weather.Data;
using TaiwanAgri.Modules.Weather.Dtos.ApiResponses;

namespace TaiwanAgri.Modules.Weather.Services
{
	public class PestService : IPestService
	{
		private readonly WeatherDbContext _context;
		public PestService(WeatherDbContext context)
		{
			_context = context;
		}

		public async Task<PagedResult<PestAlertResponseDto>> GetPestAlertsByCityAsync(string? cityName = null, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
		{
			var query = _context.PestAlerts
				.Include(pa => pa.Cities)
				.Include(pa => pa.Crops)
				.AsQueryable();

			// 「臺」「台」兩種寫法都比：2026-11 之前同步進來的警報存的是「台」（見 TaiwanCityName）。
			// 原本精確比對，選臺中／臺南／臺東永遠 0 筆，前端只好在送出前把「臺」換回「台」
			if (!string.IsNullOrWhiteSpace(cityName))
			{
				var (primary, alternate) = TaiwanCityName.Variants(cityName);
				query = query.Where(a => a.Cities.Any(c => c.CityName == primary || c.CityName == alternate));
			}

			// 總筆數要在套 Skip/Take 之前算，前端才有總頁數可用（比照 FoodSafetyService）
			var totalCount = await query.CountAsync(cancellationToken);

			// Id 當第二排序鍵：同一個發布日最多有 5 筆警報，只比發布日的話資料庫不保證先後，
			// 翻頁時同一筆可能出現兩次或漏掉（其他分頁查詢都有加，這支原本漏了）
			var items = await query
				.OrderByDescending(pa => pa.PubDate)
				.ThenByDescending(pa => pa.Id)
				.Skip((page - 1) * pageSize)
				.Take(pageSize)
				.Select(pa => new PestAlertResponseDto
				{
					Id = pa.Id,
					Subject = pa.Subject,
					Body = pa.Body,
					Prescription = pa.Prescription,
					PubDate = pa.PubDate,
					Issue = pa.Issue,
					Cities = pa.Cities.Select(c => c.CityName).ToList(),
					Crops = pa.Crops.Select(c => c.CropName).ToList()
				})
				.ToListAsync(cancellationToken);

			// 回給前端的縣市一律是正式寫法，舊資料的「台」在這裡換掉，前端不必自己處理
			foreach (var item in items)
				item.Cities = item.Cities.Select(TaiwanCityName.Normalize).Distinct().ToList();

			return PagedResult<PestAlertResponseDto>.Create(items, totalCount, page, pageSize);
		}

		/// <summary>
		/// 各縣市被幾則警報影響。原本前端為了畫縣市燈號，用 pageSize=500 把全部警報（含長篇內文）抓回來自己數，
		/// 靠的正是分頁沒有上限；分頁補上上限之後改由這支直接回計數（2026-11 總檢查 3-A7）。
		/// 一則警報可能同時有「台南市」「臺南市」兩列（舊資料），所以先換成正式寫法、再數不重複的警報
		/// </summary>
		public async Task<List<PestAlertCityCountDto>> GetPestAlertCountsByCityAsync(CancellationToken cancellationToken = default)
		{
			// 只取（警報編號、縣市）兩欄：一則警報影響幾個縣市，全表不過數百列
			var pairs = await _context.PestAlertCities
				.AsNoTracking()
				.Select(c => new { c.AlertId, c.CityName })
				.ToListAsync(cancellationToken);

			return pairs
				.GroupBy(p => TaiwanCityName.Normalize(p.CityName))
				.Select(g => new PestAlertCityCountDto
				{
					CityName = g.Key,
					Count = g.Select(p => p.AlertId).Distinct().Count()
				})
				.OrderBy(c => c.CityName, StringComparer.Ordinal)
				.ToList();
		}

		public async Task<List<PestDecadeSummaryResponseDto>> GetPestDecadeDensityByPestNameAsync(string pestName, CancellationToken cancellationToken = default)
		{
			var pestDecadeSummaries = await _context.PestDecadeSummaries
				.Where(pds => pds.PestName == pestName)
				.OrderByDescending(pds => pds.Year)
				.ThenByDescending(pds => pds.Month)
				.ThenByDescending(pds => pds.TenDays)
				.Select(pds => new PestDecadeSummaryResponseDto
				{
					PestName = pds.PestName,
					Year = pds.Year,
					Month = pds.Month,
					TenDays = pds.TenDays,
					City = pds.City,
					Town = pds.Town,
					Average = pds.Average,
					ProportionIsland = pds.ProportionIsland
				})
				.ToListAsync(cancellationToken); 
			return pestDecadeSummaries;
		}

		public async Task<List<string>> GetAllPestNamesAsync(CancellationToken cancellationToken = default)
		{
			// 排序：害蟲名稱的下拉選單直接照這個順序列，沒有排序的話每次順序由資料庫決定
			var pestNames = await _context.PestDecadeSummaries
				.Select(pds => pds.PestName)
				.Distinct()
				.OrderBy(name => name)
				.ToListAsync(cancellationToken);
			return pestNames;
		}
	}
}
