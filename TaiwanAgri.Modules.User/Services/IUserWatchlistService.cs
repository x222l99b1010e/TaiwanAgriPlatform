using TaiwanAgri.Modules.User.Dtos.ApiRequests;
using TaiwanAgri.Modules.User.Dtos.ApiResponses;

namespace TaiwanAgri.Modules.User.Services
{
	public interface IUserWatchlistService
	{
		Task<IEnumerable<WatchlistItemDto>> GetUserWatchlistItemsAsync(string userId, CancellationToken cancellationToken = default);//可以建立多筆監測清單
		Task<bool> AddWatchlistItemAsync(string userId, AddWatchlistRequestDto request, CancellationToken cancellationToken = default);//一次新增一樣監測清單項目
		Task RemoveWatchlistItemsAsync(string userId, IEnumerable<int> ids, CancellationToken cancellationToken = default);//一次刪除多筆監測清單項目
	}
}
