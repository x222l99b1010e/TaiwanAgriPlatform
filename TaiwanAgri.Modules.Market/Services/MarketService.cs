using Microsoft.EntityFrameworkCore;
using TaiwanAgri.Core.Helpers;
using TaiwanAgri.Modules.Market.Data;
using TaiwanAgri.Modules.Market.Dtos.ApiResponses;

namespace TaiwanAgri.Modules.Market.Services
{
	public class MarketService : IMarketService
	{
		private readonly MarketDbContext _context;
		public MarketService(MarketDbContext context)
		{
			_context = context;
		}
		public async Task<List<PriceResponseDto>> GetPricesAsync(
			string marketType,
			string[] cropCodes,
			string? marketCode = null,
			DateOnly? startDate = null,
			DateOnly? endDate = null)
		{
			// 1. 日期解析與預設值
			DateOnly finalEnd = endDate ?? DateOnly.FromDateTime(DateTime.Today);
			DateOnly finalStart = startDate ?? finalEnd.AddDays(-365);

			// 2. 三表 JOIN 基礎查詢
			var baseQuery = from t in _context.AgriProductsTrans
							join c in _context.CropInfos
								on t.CropCode equals c.CropCode
							join m in _context.MarketInfos
								on t.MarketCode equals m.MarketCode
							where t.TransDate >= finalStart
							   && t.TransDate <= finalEnd
							   && m.MarketType == marketType
							   && cropCodes.Contains(t.CropCode)
							select new { t, c.CropName };
			// 3. 動態追加市場過濾
			if (!string.IsNullOrEmpty(marketCode))
			{
				baseQuery = baseQuery.Where(x => x.t.MarketCode == marketCode);
			}

			// 4. GroupBy + 聚合
			var result = await baseQuery
				.GroupBy(x => new { x.t.TransDate, x.t.CropCode, x.CropName })
				.Select(g => new PriceResponseDto
				{
					TransDate = g.Key.TransDate,
					CropCode = g.Key.CropCode,
					CropName = g.Key.CropName,
					UpperPrice = g.Average(x => x.t.UpperPrice),
					MiddlePrice = g.Average(x => x.t.MiddlePrice),
					LowerPrice = g.Average(x => x.t.LowerPrice),
					AvgPrice = g.Average(x => x.t.AvgPrice),
					TransQuantity = g.Sum(x => x.t.TransQuantity)
				})
				.OrderByDescending(x => x.TransDate)
				.ToListAsync();

			return result;
		}
		public async Task<List<DisasterResponseDto>> GetDisastersAsync(string[] counties, DateOnly startDate, DateOnly endDate)
		{
			// 1. 建立基礎查詢
			var query = _context.DebrisAlertRecords.AsQueryable();

			// 2. 必填條件：日期區間
			// 修正：將 DateOnly 轉為 DateTime 才能與資料庫欄位比較
			// startDate.ToDateTime(TimeOnly.MinValue) 會變成該日的 00:00:00
			// endDate.ToDateTime(TimeOnly.MaxValue) 會變成該日的 23:59:59.999...
			DateTime startDateTime = startDate.ToDateTime(TimeOnly.MinValue);
			DateTime endDateTime = endDate.ToDateTime(TimeOnly.MaxValue);

			query = query.Where(d => d.LastUpdateDate >= startDateTime && d.LastUpdateDate <= endDateTime);

			// 3. 選填條件：縣市過濾
			if (counties != null && counties.Any())
			{
				query = query.Where(d => counties.Contains(d.County));
			}

			// 4. 投影到 Dto 並執行非同步回傳
			return await query
				.Select(d => new DisasterResponseDto
				{
					DisasterName = d.DisasterName,
					County = d.County,
					Town = d.Town,
					AlertLevel = d.AlertLevel,
					AlertType = d.AlertType,
					// 修正：從 DateTime 轉回 DateOnly 給 DTO
					LastUpdateDate = DateOnly.FromDateTime(d.LastUpdateDate)
				})
				.ToListAsync();
		}
		public async Task<List<CropResponseDto>> GetCropsAsync(string marketType)
		{
			// 1. 定義查詢邏輯
			var crops = await (from c in _context.CropInfos
						join t in _context.AgriProductsTrans
							on c.CropCode equals t.CropCode
						join m in _context.MarketInfos
							on t.MarketCode equals m.MarketCode
						where m.MarketType == marketType
						   && c.CropName != ""
						// 2. 執行 Distinct 去重，並異步轉換成 List
						// 注意：Distinct 必須放在 Select 之後，確保是針對 CropCode + CropName 組合進行去重
						select new CropResponseDto
						{
							CropCode = c.CropCode,
							CropName = c.CropName
						})
						.Distinct()
						.ToListAsync();
			return crops;
		}

		public async Task<List<MarketResponseDto>> GetMarketsAsync(string marketType)
		{
			return await _context.MarketInfos
				.Where(m => m.MarketType == marketType)
				//MarketResponseDto 的 Select 只做一件事——把欄位值搬過去。
				//SELECT MarketCode, MarketName FROM MarketInfos WHERE MarketType = 'Veg'
				//SQL 完全理解「取這兩個欄位的值」。
				.Select(m => new MarketResponseDto
				{
					MarketCode = m.MarketCode,
					MarketName = m.MarketName
				})
				.ToListAsync();
		}

		public async Task<List<RestDayResponseDto>> GetRestDaysAsync(string marketCode, DateOnly startDate, DateOnly endDate)
		{
			// 先用 MarketCode 在 DB 篩選
			//第一段：.ToListAsync()   ← 資料從資料庫載入記憶體
			// ↑ 這條線以上是 SQL 世界
			var records = await _context.MarketRestDays
				.Where(r => r.MarketCode == marketCode)
				.ToListAsync();

			//第二段：.Select(r => new DateOnly(...))  ← C# 建構子，合法
			// 在記憶體組成 DateOnly，再篩日期範圍
			return records
				 //new DateOnly(r.Year, r.Month, r.RestDay) 是在用三個值計算出一個新的物件，這個邏輯 SQL 沒有對應的語法。
				.Select(r => new DateOnly(r.Year, r.Month, r.RestDay))
				.Where(d => d >= startDate && d <= endDate)
				.Select(d => new RestDayResponseDto { RestDate = d })
				.ToList();
		}
	}
}
