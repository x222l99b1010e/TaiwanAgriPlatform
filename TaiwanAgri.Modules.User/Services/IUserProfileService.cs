using TaiwanAgri.Modules.User.Entities;

namespace TaiwanAgri.Modules.User.Services
{
	public interface IUserProfileService
	{
		/// <summary>
		/// 取得指定使用者的農場設定檔。若尚未建立則回傳 null。
		/// </summary>
		/// <param name="userId"></param>
		/// <returns></returns>
		Task<UserFarmProfile?> GetUserFarmProfileAsync(string userId);
		/// <summary>
		/// 以 Upsert 語意更新農場設定檔：
		/// 若該 userId 已有設定檔則更新欄位；若無則新增一筆。
		/// <para>
		/// ⚠️ 注意：crops 欄位採全量取代（先刪後寫），
		/// 呼叫端必須每次傳入完整的作物清單，不可只傳差異。
		/// </para>
		/// </summary>
		/// <param name="userId"></param>
		/// <param name="farmCity"></param>
		/// <param name="farmType"></param>
		/// <param name="crops"></param>
		/// <returns></returns>
		Task UpsertUserFarmProfileAsync(
				string userId,
				string? farmCity,
				string? farmType,
				List<(string CropCode, string CropName)> crops);
	}
}
