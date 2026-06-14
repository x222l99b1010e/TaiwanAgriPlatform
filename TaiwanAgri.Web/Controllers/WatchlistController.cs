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

			var watchlistItems = await userWatchlistService.GetUserWatchlistItemsAsync(userId);
			if (!watchlistItems.Any())
				return Ok(Enumerable.Empty<WatchlistEnrichedItemDto>());

			var result = new List<WatchlistEnrichedItemDto>();

			foreach (var item in watchlistItems) 
			{
				var prices = await marketService.GetPricesAsync(
					marketType: item.MarketType,
					cropCodes: new[] { item.CropCode },
					marketCode: item.MarketCode
				);
				var latestPrice = prices
						.OrderByDescending(p => p.TransDate)
						.FirstOrDefault();

				result.Add(new WatchlistEnrichedItemDto
				{
					Id = item.Id,
					CropCode = item.CropCode,
					CropName = item.CropName,
					MarketCode = item.MarketCode,
					MarketName = item.MarketName,
					MarketType = item.MarketType,
					AvgPrice = latestPrice?.AvgPrice,
					TransDate = latestPrice?.TransDate
				});
			}
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
