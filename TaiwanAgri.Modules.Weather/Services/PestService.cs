using Microsoft.EntityFrameworkCore;
using TaiwanAgri.Core.Dtos;
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

		public async Task<PagedResult<PestAlertResponseDto>> GetPestAlertsByCityAsync(string? cityName = null, int page = 1, int pageSize = 20)
		{
			var query = _context.PestAlerts
				.Include(pa => pa.Cities)
				.Include(pa => pa.Crops)
				.Where(a => cityName == null || a.Cities.Any(c => c.CityName == cityName));

			// 總筆數要在套 Skip/Take 之前算，前端才有總頁數可用（比照 FoodSafetyService）
			var totalCount = await query.CountAsync();

			var items = await query
				.OrderByDescending(pa => pa.PubDate)
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
				.ToListAsync();

			return new PagedResult<PestAlertResponseDto>
			{
				Items = items,
				TotalCount = totalCount,
				Page = page,
				PageSize = pageSize,
				TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
			};
		}

		public async Task<List<PestDecadeSummaryResponseDto>> GetPestDecadeDensityByPestNameAsync(string pestName)
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
				.ToListAsync(); 
			return pestDecadeSummaries;
		}

		public async Task<List<string>> GetAllPestNamesAsync()
		{
			var pestNames = await _context.PestDecadeSummaries
				.Select(pds => pds.PestName)
				.Distinct()
				.ToListAsync(); 
			return pestNames;
		}
	}
}
