using Microsoft.EntityFrameworkCore;
using TaiwanAgri.Modules.User.Data;
using TaiwanAgri.Modules.User.Entities;

namespace TaiwanAgri.Modules.User.Services
{
	public class UserProfileService(UserDbContext context) : IUserProfileService
	{
		public async Task<UserFarmProfile?> GetUsersFarmProfileAsync(string userId)
		{
			// Include(p => p.Crops)：一次查詢同時載入作物清單
			// FirstOrDefaultAsync：找不到回傳 null，不拋例外
			return await context.UserFarmProfiles
				.Include(p => p.Crops)
				.FirstOrDefaultAsync(p => p.UserId == userId);
		}

		public async Task UpsertUsersFarmProfileAsync(string userId, string? farmCity, string? farmType, List<(string CropCode, string CropName)> crops)
		{
			var existing = await context.UserFarmProfiles
				.Include(p => p.Crops)
				.FirstOrDefaultAsync(p => p.UserId == userId);

			if (existing == null)
			{
				// 第一次儲存：新增主檔，建立新的使用者農場資料
				var profile = new UserFarmProfile
				{
					UserId = userId,
					FarmCity = farmCity,
					FarmType = farmType,
					CreatedAt = DateTime.UtcNow,
					UpdatedAt = DateTime.UtcNow
				};
				context.UserFarmProfiles.Add(profile);

				// 新增作物清單
				foreach(var (cropCode, cropName) in crops)
				{
					context.UserFarmCrops.Add(new UserFarmCrop
					{
						UserId = userId,
						CropCode = cropCode,
						CropName = cropName
					});
				}
			}
			else
			{
				// 已存在：更新主檔欄位
				// CreatedAt 不動，只有第一次建立時設定
				existing.FarmCity = farmCity;
				existing.FarmType = farmType;
				existing.UpdatedAt = DateTime.UtcNow;				

				// 作物清單：全刪全插（選 A）
				// 理由：農民種 3-10 種作物，數量少，全刪全插比 diff 比對簡單可靠
				context.UserFarmCrops.RemoveRange(existing.Crops);

				foreach (var (cropCode, cropName) in crops)
				{
					context.UserFarmCrops.Add(new UserFarmCrop
					{
						UserId = userId,
						CropCode = cropCode,
						CropName = cropName
					});
				}
			}
			// 一次 SaveChanges 把新增/更新/刪除全部送出去
			await context.SaveChangesAsync();
			
		}
	}
}
