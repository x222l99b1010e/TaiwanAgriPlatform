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
		public async Task<List<DisasterResponseDto>> GetDisastersAsync(
			string[] counties,
			DateOnly startDate,
			DateOnly endDate)
		{
			DateTime startDateTime = startDate.ToDateTime(TimeOnly.MinValue);
			DateTime endDateTime = endDate.ToDateTime(TimeOnly.MaxValue);

			var query = _context.DebrisAlertRecords
				.Where(d => d.LastUpdateDate >= startDateTime && d.LastUpdateDate <= endDateTime);

			if (counties != null && counties.Any())
				query = query.Where(d => counties.Contains(d.County));

			// 先撈出去，再在記憶體 GroupBy 去重
			// 同一天同一個災害可能有幾百筆（每個村落一筆），前端只需要唯一事件
			var raw = await query
				.Select(d => new {
					d.DisasterName,
					d.AlertType,
					d.County,                                          // ← 補回
					AlertDate = DateOnly.FromDateTime(d.LastUpdateDate)
				})
				.ToListAsync();

			return raw
				.GroupBy(d => new { d.DisasterName, d.AlertDate })
				.Select(g => new DisasterResponseDto
				{
					DisasterName = g.Key.DisasterName,
					AlertType = g.First().AlertType,
					AlertDate = g.Key.AlertDate.ToString("yyyy-MM-dd"),
					AffectedCounties = g.Select(x => x.County)
										.Distinct()
										.OrderBy(c => c)
										.ToList()
				})
				.OrderBy(d => d.AlertDate)
				.ToList();
		}
		public async Task<List<CropResponseDto>> GetCropsAsync(string marketType)
		{
			// 1. 定義查詢邏輯
			//var crops = await (from c in _context.CropInfos
			//				   join t in _context.AgriProductsTrans
			//					   on c.CropCode equals t.CropCode
			//				   join m in _context.MarketInfos
			//					   on t.MarketCode equals m.MarketCode
			//				   where m.MarketType == marketType
			//					  && c.CropName != ""
			//				   // 2. 執行 Distinct 去重，並異步轉換成 List
			//				   // 注意：Distinct 必須放在 Select 之後，確保是針對 CropCode + CropName 組合進行去重
			//				   select new CropResponseDto
			//				   {
			//					   CropCode = c.CropCode,
			//					   CropName = c.CropName
			//				   })
			//			.Distinct()
			//			.ToListAsync();

			// Step 1: 先拿 MarketCodes（小表，幾筆）
			var marketCodes = await _context.MarketInfos
				.Where(m => m.MarketType == marketType)
				.Select(m => m.MarketCode)
				.ToListAsync();

			// Step 2: 用具體的 MarketCode 值查 AgriProductsTrans
			// EF Core 會產生 IN ('101','102',...) 而不是 JOIN + 參數
			var crops = await _context.CropInfos
				.Where(c => c.CropName != "" &&
							_context.AgriProductsTrans
								.Where(a => marketCodes.Contains(a.MarketCode))
								.Select(a => a.CropCode)
								.Contains(c.CropCode))
				.Select(c => new CropResponseDto
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
				 .Select(r =>
				 {
					 try { return (DateOnly?)new DateOnly(r.Year + 1911, r.Month, r.RestDay); }
					 catch { return null; }
				 })
				.Where(d => d.HasValue)
				.Select(d => d!.Value)
				.Where(d => d >= startDate && d <= endDate)
				.Select(d => new RestDayResponseDto { RestDate = d })
				.ToList();
		}
	}
}
