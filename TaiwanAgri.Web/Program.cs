using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TaiwanAgri.Core.Infrastructure;
using TaiwanAgri.Core.Infrastructure.Data;
using TaiwanAgri.Modules.User.Data;
using TaiwanAgri.Web.Extensions;
using TaiwanAgri.Web.Middlewares;

namespace TaiwanAgri.Web
{
	public class Program
	{
		public static async Task Main(string[] args)
		{
			var builder = WebApplication.CreateBuilder(args);

			builder.Services.AddIdentityModule(builder.Configuration);
			builder.Services.AddMarketModule(builder.Configuration);
			builder.Services.AddWeatherModule(builder.Configuration);
			builder.Services.AddCoreModule(builder.Configuration);
			builder.Services.AddInfrastructure(builder.Configuration);
			builder.Services.AddUserModule(builder.Configuration);

			var app = builder.Build();

			// Seed 初始資料（角色、核心資料）
			using (var scope = app.Services.CreateScope())
			{
				var coreContext = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
				var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
				await DbInitializer.SeedAsync(coreContext, roleManager);
			}

			// Configure the HTTP request pipeline.
			if (app.Environment.IsDevelopment())
			{
				// 開發者模式：看到詳細的報錯
				//app.UseDeveloperExceptionPage();

				// Swagger UI 只在開發環境啟用，正式環境不暴露 API 文件
				app.UseSwagger();
				app.UseSwaggerUI(); // 預設路徑：/swagger
				app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
			}
			else
			{
				// 正式環境：
				// 1. 回傳不含敏感資訊的標準錯誤 JSON (Problem Details)
				//app.UseExceptionHandler();

				app.UseStatusCodePages(); // 自動處理 400-599 的狀態碼

				// 2. 強制 HTTPS 安全傳輸
				app.UseHsts();
			}

			if (!app.Environment.IsDevelopment())
			{
				app.UseHttpsRedirection();
			}

			app.UseMiddleware<GlobalExceptionMiddleware>();
			app.UseRouting();
			app.UseCors("MyPolicy");
			app.UseAuthentication(); // 既然有 Identity，這行通常要加在 Authorization 之前
			app.UseAuthorization();
			app.MapControllers();

			app.Run();
		}
	}
}