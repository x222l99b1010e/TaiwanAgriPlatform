using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TaiwanAgri.Modules.Market.Services;
using TaiwanAgri.Modules.User.Services;
using TaiwanAgri.Modules.User.Dtos.ApiRequests;
using TaiwanAgri.Modules.User.Dtos.ApiResponses;
using TaiwanAgri.Modules.Market.Dtos.ApiResponses;

namespace TaiwanAgri.Web.Controllers
{
	[Route("api/[controller]")]
	[Authorize]
	[ApiController]
	public class WatchlistController(IUserWatchlistService userWatchlistService, IMarketService marketService) : ControllerBase
	{
		[HttpGet]
		public async Task<IActionResult> GetWatchlistItems()
		{
			var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
			if (userId is null) return Unauthorized();

			var watchlistItems = (await userWatchlistService.GetUserWatchlistItemsAsync(userId)).ToList();
			if (watchlistItems.Count == 0)
				return Ok(Enumerable.Empty<WatchlistEnrichedItemDto>());

			// 一次批次查詢所有（作物, 市場）組合的最新均價，
			// 取代原本 foreach 逐筆呼叫 GetPricesAsync 的 N+1 查詢
			var latestPrices = await marketService.GetLatestPricesAsync(
				watchlistItems.Select(item => (item.CropCode, item.MarketCode)));

			var priceLookup = latestPrices.ToDictionary(p => (p.CropCode, p.MarketCode));

			var result = watchlistItems.Select(item =>
			{
				priceLookup.TryGetValue((item.CropCode, item.MarketCode), out var latestPrice);
				return new WatchlistEnrichedItemDto
				{
					Id = item.Id,
					CropCode = item.CropCode,
					CropName = item.CropName,
					MarketCode = item.MarketCode,
					MarketName = item.MarketName,
					MarketType = item.MarketType,
					AvgPrice = latestPrice?.AvgPrice,
					TransDate = latestPrice?.TransDate
				};
			}).ToList();

			return Ok(result);
		}
		[HttpPost]
		public async Task<IActionResult> AddWatchlistItem([FromBody] AddWatchlistRequestDto request)
		{
			var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
			if (userId is null) return Unauthorized();

			var success = await userWatchlistService.AddWatchlistItemAsync(userId, request);
			if (!success) return Conflict("此作物與市場組合已在監看清單中");

			return NoContent();
		}
		[HttpDelete]
		public async Task<IActionResult> RemoveWatchlistItems([FromQuery] IEnumerable<int> ids)
		{
			var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
			if (userId is null) return Unauthorized();

			await userWatchlistService.RemoveWatchlistItemsAsync(userId, ids);
			return NoContent();
		}
	}
}
