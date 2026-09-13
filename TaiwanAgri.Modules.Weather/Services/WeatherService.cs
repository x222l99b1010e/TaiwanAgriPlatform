using TaiwanAgri.Core.Helpers;
using TaiwanAgri.Modules.Weather.Data;
using TaiwanAgri.Modules.Weather.Dtos.ApiResponses;
using Microsoft.EntityFrameworkCore;


namespace TaiwanAgri.Modules.Weather.Services
{
	public class WeatherService : IWeatherService
	{
		private readonly WeatherDbContext _context;
		private readonly TimeProvider _timeProvider;
		public WeatherService(WeatherDbContext context, TimeProvider timeProvider)
		{
			_context = context;
			_timeProvider = timeProvider;
		}
		public async Task<List<RainfallResponseDto>> GetRainfallByCityAsync(string cityName, DateOnly? startDate = null, DateOnly? endDate = null, CancellationToken cancellationToken = default)
		{
			// 預設區間的「今天」＝台灣時區日界。DateTime.Now 是主機本地時區，
			// 部署在 UTC 主機（Azure App Service 預設）時，台灣時間每天 08:00 之前
			// 會停在前一天，整組區間跟著差一天
			DateOnly finalEnd = endDate ?? TaiwanTime.Today(_timeProvider);
			DateOnly finalStart = startDate ?? finalEnd.AddDays(-14);

			var result = await _context.RainfallObservations
				.Join(_context.RainfallStations,
					obs => obs.StationId,
					sta => sta.StationId,
					(obs, sta) => new { obs, sta })
				.Where(x => x.sta.CityName == cityName
						 && DateOnly.FromDateTime(x.obs.ObservedAt) >= finalStart
						 && DateOnly.FromDateTime(x.obs.ObservedAt) <= finalEnd)
				.Select(x => new RainfallResponseDto
				{
					StationName = x.sta.StationName,
					CityName = x.sta.CityName,
					ObservedAt = x.obs.ObservedAt,
					Hour3 = x.obs.Hour3,
					Hour6 = x.obs.Hour6,
					Hour12 = x.obs.Hour12,
					Hour24 = x.obs.Hour24
				})
				.ToListAsync(cancellationToken);

			return result;
		}
		/// <summary>
		/// 查詢指定縣市下所有氣象站的最新觀測資料。
		/// 採兩段式查詢策略：
		/// Step 1：SQL 端 GroupBy 取各站最新 ObservedAt（回傳筆數 = 站台數，通常幾十筆）
		/// Step 2：用 (StationId, ObservedAt) 撈完整欄位資料
		/// Step 3：記憶體端 GroupBy 做最後防護，排除極端情況下同一站有多筆相同時間的重複資料
		/// 末段記憶體 GroupBy 開銷可忽略，因為資料量僅為站台數。
		/// </summary>
		/// <param name="cityName"></param>
		/// <returns></returns>

		public async Task<List<WeatherStationResponseDto>> GetStationsByCityAsync(string cityName, CancellationToken cancellationToken = default)
		{
			// Step 1：在 DB 端計算每個站的最新觀測時間
			// 這一段完全在 SQL Server 執行，只回傳 N 筆（N = 站台數量，通常幾十筆）
			var latestTimes = await _context.WeatherObservations
				.Where(s => s.CityName == cityName)
				.GroupBy(s => s.StationId)
				.Select(g => new
				{
					StationId = g.Key,
					LatestAt = g.Max(w => w.ObservedAt)
				})
				.ToListAsync(cancellationToken);

			// Step 2：用 (StationId, ObservedAt) 這對組合撈完整資料
			// 這樣只撈每個站最新的那一筆，而不是全部幾千筆
			var stationIds = latestTimes.Select(x => x.StationId).ToList();
			var latestObservedAts = latestTimes.Select(x => x.LatestAt).ToList();

			var result = await _context.WeatherObservations
				.Where(s => stationIds.Contains(s.StationId)
						 && latestObservedAts.Contains(s.ObservedAt)
						 && s.CityName == cityName)
				.Select(s => new WeatherStationResponseDto
				{
					StationName = s.StationName,
					CityName = s.CityName,
					TownName = s.TownName,
					ObservedAt = s.ObservedAt,
					Temperature = s.Temperature,
					DailyMaxTemp = s.DailyMaxTemp,
					DailyMinTemp = s.DailyMinTemp,
					Humidity = s.Humidity,
					WindSpeed = s.WindSpeed,
					WindDirection = s.WindDirection,
					MaxGust = s.MaxGust,
					Rainfall24h = s.Rainfall24h,
					SunshineHours = s.SunshineHours,
					Pressure = s.Pressure
				})
				.ToListAsync(cancellationToken);

			// 安全防護：確保每個站只回傳最新的一筆
			// 理論上 Step 2 的 IN 條件已能正確篩選，
			// 但若資料庫中某站有多筆相同 ObservedAt 的重複資料（違反 Unique Index 的邊界情況），
			// 這裡做最後一道防護，只取每站最新的一筆。
			// 此 GroupBy 在記憶體執行，資料量僅為站台數（通常幾十筆），開銷可忽略。
			return result
				.GroupBy(r => r.StationName)
				.Select(g => g.OrderByDescending(r => r.ObservedAt).First())
				.ToList();
		}
	}
}
