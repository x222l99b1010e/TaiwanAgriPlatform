using Microsoft.AspNetCore.Identity;
using Moq;

namespace TaiwanAgri.Tests.Helpers
{
	/// <summary>
	/// Identity 的 UserManager 與 RoleManager 沒有介面可以假，只能對類別本身下 Mock，
	/// 而它們的建構子分別要九個與五個參數。這裡把那串參數包起來，
	/// 免得每個測試都重抄一次——抄錯的成本很高，因為建構子參數變動時錯誤訊息不會指向真正的原因。
	/// <para>
	/// 傳 null 是安全的：被測程式碼只走 FindByNameAsync／FindByEmailAsync 這類已被 Mock 掉的
	/// 虛擬方法，不會碰到那些依賴。真的碰到會是 NullReferenceException，
	/// 那代表被測方法用了預期外的路徑，本來就該讓測試失敗
	/// </para>
	/// </summary>
	public static class IdentityTestHelpers
	{
		public static Mock<RoleManager<IdentityRole>> MockRoleManager() =>
			new(Mock.Of<IRoleStore<IdentityRole>>(), null!, null!, null!, null!);

		public static Mock<UserManager<TUser>> MockUserManager<TUser>() where TUser : class =>
			new(Mock.Of<IUserStore<TUser>>(), null!, null!, null!, null!, null!, null!, null!, null!);

		/// <summary>找得到角色時回傳它，找不到回 null——RoleManager 的 FindByNameAsync 語意</summary>
		public static void SetupRole(this Mock<RoleManager<IdentityRole>> mock, string roleName, string roleId) =>
			mock.Setup(m => m.FindByNameAsync(roleName))
				.ReturnsAsync(new IdentityRole(roleName) { Id = roleId });

		/// <summary>明確設定某個角色名稱查不到，用來測回退路徑</summary>
		public static void SetupMissingRole(this Mock<RoleManager<IdentityRole>> mock, string roleName) =>
			mock.Setup(m => m.FindByNameAsync(roleName)).ReturnsAsync((IdentityRole?)null);
	}
}
