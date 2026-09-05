using Microsoft.AspNetCore.Identity;
using TaiwanAgri.Core.Dtos;
using TaiwanAgri.Core.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace TaiwanAgri.Core.Services
{
	public class NavService : INavService
	{
		private readonly RoleManager<IdentityRole> _roleManager;
		private readonly CoreDbContext _context;
		private readonly ILogger<NavService> _logger;
		public NavService(RoleManager<IdentityRole> roleManager, CoreDbContext coreDbContext, ILogger<NavService> logger)
		{
			_roleManager = roleManager;
			_context = coreDbContext;
			_logger = logger;
		}
		public async Task<List<NavModuleDto>> GetNavModulesAsync(bool isAuthenticated, string? roleName, CancellationToken cancellationToken = default)
		{
			// 第一段：決定要查哪個 RoleId（各種異常情境統一回退 Guest，不靜默消失）
			var targetRoleId = await ResolveRoleIdOrGuestAsync(isAuthenticated, roleName);
			// 第二段：查資料庫，根據 RoleId 查詢 NavModuleDto
			// 1. 用 RoleId 查出所有有權限的 ModuleId 清單
			var permittedModuleIds = await _context.RoleModulePermissions
				//這行非常重要，已經篩選出有權限的 RoleId，後續只處理這個 RoleId 的模組權限
				.Where(rmp => rmp.RoleId == targetRoleId && rmp.CanView)
				.Select(rmp => rmp.ModuleId)
				.ToListAsync(cancellationToken);
			// 2. 查出有權限的頂層模組（NavModules WHERE ParentId == null（只取頂層）），並依 SortOrder 排序
			var navModules = await _context.NavModules
				.Where(nm => nm.ParentId == null && nm.IsActive && permittedModuleIds.Contains(nm.Id))
				.OrderBy(nm => nm.SortOrder) // 在資料庫端先排序
				.ToListAsync(cancellationToken);
			// 3. 抽出頂層 ID，撈出對應且有權限的子模組
			// ❌ 在 DB 查詢裡用 .Any() 比對 in-memory list，EF Core 翻譯較複雜
			//navModules.Any(nm => nm.Id == cnm.ParentId)
			//✅ 先把 ID 抽出來，用 Contains
			var topLevelIds = navModules.Select(nm => nm.Id).ToList();
			var childNavModules = await _context.NavModules
			   .Where(cnm => cnm.ParentId != null && cnm.IsActive && topLevelIds.Contains(cnm.ParentId!.Value) && permittedModuleIds.Contains(cnm.Id))
			   .OrderBy(cnm => cnm.SortOrder) // 子模組也在資料庫端先排序
			   .ToListAsync(cancellationToken);

			//第三段：組裝回傳
			//→ 從 childNavModules 找出 ParentId == 這個模組的 Id 的子功能
			//→  組成 NavChildDto
			//→ 塞進 NavModuleDto.Children
			//→ 組成 NavModuleDto
			return navModules.Select(nm => new NavModuleDto
			{
				Name = nm.Name,
				Route = nm.Route,
				Icon = nm.Icon,
				SortOrder = nm.SortOrder,
				Children = childNavModules
					.Where(c => c.ParentId == nm.Id)  // ← 這裡的 nm.Id 是當前頂層模組
					.Select(c => new NavChildDto
					{
						Name = c.Name,
						Route = c.Route,
						Icon = c.Icon,
						SortOrder = c.SortOrder
			
					})
					.ToList()
			}).ToList();
		}

		/// <summary>
		/// 解析要查詢權限的 RoleId。
		/// 未登入、已登入但缺 Role Claim、Role 名稱不存在三種情境都回退 Guest 權限；
		/// Guest 角色本身不存在屬於 Seed 缺失的系統性錯誤，直接 fail-fast
		/// </summary>
		private async Task<string> ResolveRoleIdOrGuestAsync(bool isAuthenticated, string? roleName)
		{
			if (isAuthenticated && !string.IsNullOrWhiteSpace(roleName))
			{
				// 傳入的是 role name（如 "Admin"），需透過 RoleManager 解析成真正的 GUID
				var role = await _roleManager.FindByNameAsync(roleName);
				if (role != null)
					return role.Id;

				_logger.LogWarning("Role '{RoleName}' 不存在，回退至 Guest 權限顯示", roleName);
			}
			else if (isAuthenticated)
			{
				_logger.LogWarning("已登入用戶缺少 Role Claim，回退至 Guest 權限顯示");
			}

			var guestRole = await _roleManager.FindByNameAsync("Guest");
			if (guestRole == null)
				throw new InvalidOperationException("Guest role not found");
			return guestRole.Id;
		}
	}
}
