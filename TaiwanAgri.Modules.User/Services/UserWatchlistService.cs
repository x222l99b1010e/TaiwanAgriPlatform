using Microsoft.EntityFrameworkCore;
using TaiwanAgri.Modules.User.Data;
using TaiwanAgri.Modules.User.Dtos.ApiRequests;
using TaiwanAgri.Modules.User.Dtos.ApiResponses;
using TaiwanAgri.Modules.User.Entities;

namespace TaiwanAgri.Modules.User.Services
{
	public class UserWatchlistService(UserDbContext context) : IUserWatchlistService
	{
		public async Task<IEnumerable<WatchlistItemDto>> GetUserWatchlistItemsAsync(string userId)
		{
			var watchlistItem = await context.UserWatchlists
				.Where(u => u.UserId == userId)
				.ToListAsync();
			return watchlistItem.Select(w => new WatchlistItemDto
			{
				Id = w.Id,
				CropCode = w.CropCode,
				CropName = w.CropName,
				MarketCode = w.MarketCode,
				MarketName = w.MarketName,
				MarketType = w.MarketType
			});
		}

		public async Task<bool> AddWatchlistItemAsync(string userId, AddWatchlistRequestDto request)
		{
			var exists = await context.UserWatchlists
				.AnyAsync(w => w.UserId == userId
							&& w.CropCode == request.CropCode
							&& w.MarketCode == request.MarketCode);

			if (exists) return false;  // 告訴 Controller：重複了

			var watchlistItem = new UserWatchlist
			{
				UserId = userId,
				CropCode = request.CropCode,
				CropName = request.CropName,
				MarketCode = request.MarketCode,
				MarketName = request.MarketName,
				MarketType = request.MarketType
			};
			context.UserWatchlists.Add(watchlistItem);
			await context.SaveChangesAsync();
			return true;  // 新增成功
		}

		public async Task RemoveWatchlistItemsAsync(string userId, IEnumerable<int> ids)
		{
			// 數量上限防禦在 Controller 層驗證後回 400，
			// 這裡不做靜默截斷（原 Take(50) 無排序，刪哪 50 筆不確定）
			var targetWatchListItems = context.UserWatchlists
				.Where(w => w.UserId == userId && ids.Contains(w.Id));
			context.UserWatchlists.RemoveRange(targetWatchListItems);
			await context.SaveChangesAsync();
		}
	}
}
