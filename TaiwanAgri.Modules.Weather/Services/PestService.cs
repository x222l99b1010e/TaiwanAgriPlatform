using Microsoft.EntityFrameworkCore;
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

		public async Task<List<PestAlertResponseDto>> GetPestAlertsByCityAsync(string? cityName = null, int page = 1)
		{
			var pestAlerts = await _context.PestAlerts
				.Include(pa => pa.Cities)
				.Include(pa => pa.Crops)
				.Where(a => cityName == null || a.Cities.Any(c => c.CityName == cityName))
				.OrderByDescending(pa => pa.PubDate)
				.Skip((page - 1) * 20) //分頁需要 Skip + Take 搭配——page=1 跳過 0 筆，page=2 跳過 20 筆，以此類推。
				.Take(20)
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

			return pestAlerts;
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

		public async Task<List<string>> GetAllPestNameAsync()
		{
			var pestNames = await _context.PestDecadeSummaries
				.Select(pds => pds.PestName)
				.Distinct()
				.ToListAsync(); 
			return pestNames;
		}
	}
}
