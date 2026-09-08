using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TaiwanAgri.Core.Entities;
using TaiwanAgri.Core.Infrastructure.Data;
using TaiwanAgri.Core.Services;
using TaiwanAgri.Tests.Helpers;

namespace TaiwanAgri.Tests.Core
{
	/// <summary>
	/// NavService.GetNavModulesAsync——決定登入者的側邊選單看得到哪些項目。
	/// 這支方法的核心設計是「三種異常情境統一回退 Guest 權限，不靜默消失」：
	/// 未登入、已登入但沒有角色宣告、角色名稱查不到，三條路都走 Guest。
	/// 值得測是因為壞掉的症狀是「選單多了或少了幾項」——沒有例外、沒有錯誤頁，
	/// 而權限相關的多顯示一項就是越權
	/// </summary>
	public class NavServiceTests
	{
		private const string GuestRoleId = "guest-role-id";
		private const string AdminRoleId = "admin-role-id";

		private static CoreDbContext CreateDbContext(string databaseName) =>
			new(new DbContextOptionsBuilder<CoreDbContext>()
				.UseInMemoryDatabase(databaseName)
				.Options);

		private static NavModule Module(
			int id, string name, int sortOrder, int? parentId = null, bool isActive = true) => new()
			{
				Id = id,
				Name = name,
				Route = $"/{name.ToLowerInvariant()}",
				Icon = "mdi-leaf",
				IsActive = isActive,
				SortOrder = sortOrder,
				ParentId = parentId
			};

		private static RoleModulePermission Permission(string roleId, int moduleId, bool canView = true) =>
			new() { RoleId = roleId, ModuleId = moduleId, CanView = canView };

		/// <summary>
		/// 種一份「Guest 看得到模組 1、Admin 看得到模組 1 與 2」的權限表；
		/// 模組 1 底下有子項目 11，模組 2 底下有 21
		/// </summary>
		private static async Task SeedAsync(CoreDbContext db)
		{
			db.NavModules.AddRange(
				Module(1, "Market", 1),
				Module(11, "MarketPrices", 1, parentId: 1),
				Module(2, "Admin", 2),
				Module(21, "AdminUsers", 1, parentId: 2));
			db.RoleModulePermissions.AddRange(
				Permission(GuestRoleId, 1), Permission(GuestRoleId, 11),
				Permission(AdminRoleId, 1), Permission(AdminRoleId, 11),
				Permission(AdminRoleId, 2), Permission(AdminRoleId, 21));
			await db.SaveChangesAsync();
		}

		/// <summary>Guest 一定存在（Seed 保證），Admin 依測試需要決定要不要設定</summary>
		private static NavService CreateService(CoreDbContext db, bool withAdminRole = true)
		{
			var roleManager = IdentityTestHelpers.MockRoleManager();
			roleManager.SetupRole("Guest", GuestRoleId);
			if (withAdminRole)
				roleManager.SetupRole("Admin", AdminRoleId);
			else
				roleManager.SetupMissingRole("Admin");

			return new NavService(roleManager.Object, db, NullLogger<NavService>.Instance);
		}

		// ── 角色解析的四條路 ─────────────────────────────────────────────────

		/// <summary>
		/// 未登入者拿 Guest 權限。這是最常被執行的一條路——首頁在沒登入時就會呼叫一次
		/// </summary>
		[Fact]
		public async Task 未登入時套用Guest權限()
		{
			using var db = CreateDbContext(nameof(未登入時套用Guest權限));
			await SeedAsync(db);

			var result = await CreateService(db).GetNavModulesAsync(isAuthenticated: false, roleName: null);

			Assert.Equal("Market", Assert.Single(result).Name);
		}

		/// <summary>
		/// 已登入且角色有效時套用該角色的權限，這條確認回退邏輯沒有把所有人都當成 Guest
		/// </summary>
		[Fact]
		public async Task 已登入且角色存在時套用該角色權限()
		{
			using var db = CreateDbContext(nameof(已登入且角色存在時套用該角色權限));
			await SeedAsync(db);

			var result = await CreateService(db).GetNavModulesAsync(isAuthenticated: true, roleName: "Admin");

			Assert.Equal(["Market", "Admin"], result.Select(m => m.Name));
		}

		/// <summary>
		/// 角色名稱在資料庫裡查不到時回退 Guest，而不是回空選單或拋例外。
		/// 回空選單的症狀是「登入之後整個側邊欄不見了」，使用者完全無從得知發生什麼事
		/// </summary>
		[Fact]
		public async Task 角色名稱不存在時回退Guest權限()
		{
			using var db = CreateDbContext(nameof(角色名稱不存在時回退Guest權限));
			await SeedAsync(db);

			var result = await CreateService(db, withAdminRole: false)
				.GetNavModulesAsync(isAuthenticated: true, roleName: "Admin");

			Assert.Equal("Market", Assert.Single(result).Name);
		}

		/// <summary>
		/// 已登入但 JWT 裡沒有角色宣告時同樣回退 Guest。
		/// 空字串與空白字串都要走這條，因為缺漏的角色宣告不見得是 null
		/// </summary>
		[Theory]
		[InlineData(null)]
		[InlineData("")]
		[InlineData("   ")]
		public async Task 已登入但缺少角色宣告時回退Guest權限(string? roleName)
		{
			using var db = CreateDbContext($"{nameof(已登入但缺少角色宣告時回退Guest權限)}-{roleName ?? "null"}");
			await SeedAsync(db);

			var result = await CreateService(db).GetNavModulesAsync(isAuthenticated: true, roleName);

			Assert.Equal("Market", Assert.Single(result).Name);
		}

		/// <summary>
		/// Guest 角色本身不存在屬於種子資料缺失，這是系統性錯誤而不是使用者輸入問題，
		/// 所以直接拋例外而不是再往下回退。這條守的是「fail-fast 而不是回空選單」——
		/// 回空選單會讓一個部署錯誤看起來像是權限設定問題
		/// </summary>
		[Fact]
		public async Task Guest角色不存在時直接拋例外而不是回空選單()
		{
			using var db = CreateDbContext(nameof(Guest角色不存在時直接拋例外而不是回空選單));
			await SeedAsync(db);
			var roleManager = IdentityTestHelpers.MockRoleManager();
			roleManager.SetupMissingRole("Guest");
			var service = new NavService(roleManager.Object, db, NullLogger<NavService>.Instance);

			await Assert.ThrowsAsync<InvalidOperationException>(
				() => service.GetNavModulesAsync(isAuthenticated: false, roleName: null));
		}

		// ── 樹狀組裝與篩選 ───────────────────────────────────────────────────

		/// <summary>
		/// 子項目只掛到自己的父模組底下。組裝時比對的是 ParentId，
		/// 比錯的症狀是所有子項目都掛到第一個父模組下，而選單看起來還是「有東西」
		/// </summary>
		[Fact]
		public async Task 子項目只掛在自己的父模組底下()
		{
			using var db = CreateDbContext(nameof(子項目只掛在自己的父模組底下));
			await SeedAsync(db);

			var result = await CreateService(db).GetNavModulesAsync(isAuthenticated: true, roleName: "Admin");

			Assert.Equal("MarketPrices", Assert.Single(result.Single(m => m.Name == "Market").Children).Name);
			Assert.Equal("AdminUsers", Assert.Single(result.Single(m => m.Name == "Admin").Children).Name);
		}

		/// <summary>
		/// 子項目有權限、但父模組沒權限時，子項目不會單獨浮出來變成頂層項目——
		/// 因為第三段查詢的範圍限定在「有權限的頂層模組的 Id」之內。
		/// 這是權限模型的關鍵邊界：漏掉它等於讓使用者看見一個他不該看見的模組底下的功能
		/// </summary>
		[Fact]
		public async Task 父模組無權限時其子項目不會浮出成為頂層()
		{
			using var db = CreateDbContext(nameof(父模組無權限時其子項目不會浮出成為頂層));
			await SeedAsync(db);
			// 額外給 Guest 一個「子項目有權限、父模組沒權限」的組合
			db.RoleModulePermissions.Add(Permission(GuestRoleId, 21));
			await db.SaveChangesAsync();

			var result = await CreateService(db).GetNavModulesAsync(isAuthenticated: false, roleName: null);

			Assert.Equal("Market", Assert.Single(result).Name);
			Assert.DoesNotContain(result.SelectMany(m => m.Children), c => c.Name == "AdminUsers");
		}

		/// <summary>
		/// CanView 為 false 的權限列存在但不生效。權限表是「有列且 CanView 為真」才算有權限，
		/// 只看「有沒有這一列」的症狀是關掉權限沒有效果
		/// </summary>
		[Fact]
		public async Task 權限列的CanView為false時不顯示該模組()
		{
			using var db = CreateDbContext(nameof(權限列的CanView為false時不顯示該模組));
			db.NavModules.Add(Module(1, "Market", 1));
			db.RoleModulePermissions.Add(Permission(GuestRoleId, 1, canView: false));
			await db.SaveChangesAsync();

			var result = await CreateService(db).GetNavModulesAsync(isAuthenticated: false, roleName: null);

			Assert.Empty(result);
		}

		/// <summary>
		/// 停用的模組不顯示，父子兩層都要套用這個條件。
		/// 只在父層檢查的症狀是「停用的子功能還掛在啟用的父模組底下」
		/// </summary>
		[Fact]
		public async Task 停用的模組與子項目都不顯示()
		{
			using var db = CreateDbContext(nameof(停用的模組與子項目都不顯示));
			db.NavModules.AddRange(
				Module(1, "Market", 1),
				Module(11, "MarketPrices", 1, parentId: 1, isActive: false),
				Module(2, "Weather", 2, isActive: false));
			db.RoleModulePermissions.AddRange(
				Permission(GuestRoleId, 1), Permission(GuestRoleId, 11), Permission(GuestRoleId, 2));
			await db.SaveChangesAsync();

			var result = await CreateService(db).GetNavModulesAsync(isAuthenticated: false, roleName: null);

			Assert.Equal("Market", Assert.Single(result).Name);
			Assert.Empty(result[0].Children);
		}

		/// <summary>
		/// 父層與子層各自依 SortOrder 排序。選單順序是使用者每天看到的東西，
		/// 排序失效不會報錯、只會讓項目位置每次部署都可能不同
		/// </summary>
		[Fact]
		public async Task 父層與子層都依SortOrder排序()
		{
			using var db = CreateDbContext(nameof(父層與子層都依SortOrder排序));
			db.NavModules.AddRange(
				Module(1, "Second", 2),
				Module(2, "First", 1),
				Module(11, "ChildB", 2, parentId: 2),
				Module(12, "ChildA", 1, parentId: 2));
			db.RoleModulePermissions.AddRange(
				Permission(GuestRoleId, 1), Permission(GuestRoleId, 2),
				Permission(GuestRoleId, 11), Permission(GuestRoleId, 12));
			await db.SaveChangesAsync();

			var result = await CreateService(db).GetNavModulesAsync(isAuthenticated: false, roleName: null);

			Assert.Equal(["First", "Second"], result.Select(m => m.Name));
			Assert.Equal(["ChildA", "ChildB"], result[0].Children.Select(c => c.Name));
		}

		/// <summary>
		/// 一個角色什麼權限都沒有時回空清單而不是 null，前端才能直接 v-for
		/// </summary>
		[Fact]
		public async Task 完全沒有權限時回傳空清單()
		{
			using var db = CreateDbContext(nameof(完全沒有權限時回傳空清單));
			db.NavModules.Add(Module(1, "Market", 1));
			await db.SaveChangesAsync();

			Assert.Empty(await CreateService(db).GetNavModulesAsync(isAuthenticated: false, roleName: null));
		}
	}
}
