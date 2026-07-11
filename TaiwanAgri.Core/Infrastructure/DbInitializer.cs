using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TaiwanAgri.Core.Entities;
using TaiwanAgri.Core.Infrastructure.Data;

namespace TaiwanAgri.Core.Infrastructure
{
	public static class DbInitializer
	{
		public static async Task SeedAsync(CoreDbContext coreContext, RoleManager<IdentityRole> roleManager)
		{
			// 檢查是否有尚未套用的 Migration，若有則提早拋出友善錯誤，避免後續操作資料表時才爆掉
			var pendingMigrations = await coreContext.Database.GetPendingMigrationsAsync();
			if (pendingMigrations.Any())
				throw new InvalidOperationException(
					$"CoreDbContext 有 {pendingMigrations.Count()} 筆尚未套用的 Migration，" +
					$"請先執行 Update-Database 再啟動應用程式。\n" +
					$"待套用：{string.Join(", ", pendingMigrations)}");

			await SeedRoleAsync(roleManager);
			await SeedNavModulesAsync(coreContext);
			await SeedRoleModulePermissionsAsync(coreContext, roleManager);
		}

		private static async Task SeedRoleAsync(RoleManager<IdentityRole> roleManager)
		{
			if (!await roleManager.RoleExistsAsync("Guest"))
				await roleManager.CreateAsync(new IdentityRole("Guest"));

			if (!await roleManager.RoleExistsAsync("Admin"))
				await roleManager.CreateAsync(new IdentityRole("Admin"));
		}

		private static async Task SeedNavModulesAsync(CoreDbContext context)
		{
			if (context.NavModules.Any()) return;

			var modules = new List<NavModule>
			{
				new() { Name = "市場行情", Route = "/market",      Icon = "mdi-chart-line",      IsActive = true, SortOrder = 1, Children = new List<NavModule>
					{
						new() { Name = "行情查詢",   Route = "/market/prices",    Icon = "mdi-chart-areaspline",       IsActive = true, SortOrder = 1 },
						new() { Name = "天災記錄",   Route = "/market/disasters", Icon = "mdi-weather-lightning-rainy", IsActive = true, SortOrder = 2 },
						new() { Name = "休市日查詢", Route = "/market/rest-days", Icon = "mdi-calendar-remove",        IsActive = true, SortOrder = 3 },
						new() { Name = "畜禽行情",   Route = "/market/pork",      Icon = "mdi-pig",                    IsActive = true, SortOrder = 4 },
					}
				},
				new() { Name = "青農戰情室", Route = "/weather",     Icon = "mdi-weather-cloudy",  IsActive = true, SortOrder = 2, Children = new List<NavModule>
					{
						new() { Name = "農場氣象",   Route = "/weather/station",       Icon = "mdi-thermometer",     IsActive = true, SortOrder = 1 },
						new() { Name = "雨量趨勢",   Route = "/weather/rainfall",      Icon = "mdi-water",           IsActive = true, SortOrder = 2 },
						new() { Name = "病蟲害警報", Route = "/weather/pest-alerts",   Icon = "mdi-bug",             IsActive = true, SortOrder = 3 },
						new() { Name = "旬報查詢",   Route = "/weather/pest-decade",   Icon = "mdi-file-chart",      IsActive = true, SortOrder = 4 },
						// 智慧提示暫停用（IsActive = false），與正式環境 DB 現況一致（2026-07-11 同步）
						new() { Name = "智慧提示",   Route = "/weather/notifications", Icon = "mdi-bell-ring",       IsActive = false, SortOrder = 5 },
					}
				},
				new() { Name = "食安透明網", Route = "/food-safety", Icon = "mdi-shield-check",   IsActive = true, SortOrder = 3, Children = new List<NavModule>
					{
						new() { Name = "今日菜價快覽",   Route = "/food-safety/today-veg",              Icon = "mdi-food-apple",   IsActive = true, SortOrder = 1 },
						new() { Name = "農產品追溯查詢", Route = "/food-safety/traceability",           Icon = "mdi-magnify",      IsActive = true, SortOrder = 2 },
						new() { Name = "農藥違規警示牆", Route = "/food-safety/pest-violation",         Icon = "mdi-spray-bottle", IsActive = true, SortOrder = 3 },
						new() { Name = "有機驗證查詢",   Route = "/food-safety/organic-certifications", Icon = "mdi-certificate",  IsActive = true, SortOrder = 4 },
					}
				},
				new() { Name = "毛小孩地圖", Route = "/pet",          Icon = "mdi-paw",             IsActive = true, SortOrder = 4 },
			};

			context.NavModules.AddRange(modules);
			await context.SaveChangesAsync();
		}

		private static async Task SeedRoleModulePermissionsAsync(CoreDbContext context, RoleManager<IdentityRole> roleManager)
		{
			if (context.RoleModulePermissions.Any()) return;

			var guestRole = await roleManager.FindByNameAsync("Guest");
			var adminRole = await roleManager.FindByNameAsync("Admin");
			if (guestRole == null || adminRole == null) return;

			var allModuleIds = context.NavModules.Select(m => m.Id).ToList();

			var permissions = allModuleIds.SelectMany(moduleId => new[]
			{
				new RoleModulePermission { RoleId = guestRole.Id, ModuleId = moduleId, CanView = true },
				new RoleModulePermission { RoleId = adminRole.Id, ModuleId = moduleId, CanView = true },
			}).ToList();

			context.RoleModulePermissions.AddRange(permissions);
			await context.SaveChangesAsync();
		}
	}
}