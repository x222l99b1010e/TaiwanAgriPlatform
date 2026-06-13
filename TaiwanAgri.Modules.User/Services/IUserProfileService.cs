using TaiwanAgri.Modules.User.Entities;

namespace TaiwanAgri.Modules.User.Services
{
	public interface IUserProfileService
	{
		Task <UserFarmProfile?> GetUserFarmProfileAsync(string userId);
		Task UpsertUserFarmProfileAsync(
				string userId,
				string? farmCity,
				string? farmType,
				List<(string CropCode, string CropName)> crops);
	}
}
