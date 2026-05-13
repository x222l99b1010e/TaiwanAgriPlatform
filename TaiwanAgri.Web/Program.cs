using Microsoft.EntityFrameworkCore;
using TaiwanAgri.Core.Entities;
using TaiwanAgri.Modules.Market.Data;
using TaiwanAgri.Modules.Market.Services;
using TaiwanAgri.Web.Data;

namespace TaiwanAgri.Web
{
	public class Program
	{
		public static void Main(string[] args)
		{
			var builder = WebApplication.CreateBuilder(args);

			// Add services to the container.
			var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
			builder.Services.AddDbContext<ApplicationDbContext>(options =>
				options.UseSqlServer(connectionString));

			builder.Services.AddDefaultIdentity<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
				.AddEntityFrameworkStores<ApplicationDbContext>();
			builder.Services.AddDbContext<MarketDbContext>(options =>
				options.UseSqlServer(
					builder.Configuration.GetConnectionString("DefaultConnection")));
			builder.Services.AddControllers();
			builder.Services.AddProblemDetails(); // 註冊標準錯誤格式服務
			builder.Services.AddCors(options =>
			{
				options.AddPolicy("MyPolicy", policy =>
				{
					policy.WithOrigins("http://localhost:5173")
						.AllowAnyMethod()
						.AllowAnyHeader()
						.AllowCredentials();
				});
			});
			// 註冊 IMarketService 及其對應的實作 MarketService
			builder.Services.AddScoped<IMarketService, MarketService>();
			// builder 區段加：
			//Install - Package Swashbuckle.AspNetCore - ProjectName TaiwanAgri.Web
			builder.Services.AddEndpointsApiExplorer();
			builder.Services.AddSwaggerGen();
			var app = builder.Build();

			// Configure the HTTP request pipeline.
			if (app.Environment.IsDevelopment())
			{
				// 開發者模式：看到詳細的報錯
				app.UseDeveloperExceptionPage();
				// Swagger UI 只在開發環境啟用，正式環境不暴露 API 文件
				app.UseSwagger();
				app.UseSwaggerUI(); // 預設路徑：/swagger
				app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
			}
			else
			{
				// 正式環境：
				// 1. 回傳不含敏感資訊的標準錯誤 JSON (Problem Details)
				app.UseExceptionHandler();
				app.UseStatusCodePages();  // 自動處理 400-599 的狀態碼

				// 2. 強制 HTTPS 安全傳輸
				app.UseHsts();
			}

			if (!app.Environment.IsDevelopment())
			{
				app.UseHttpsRedirection();
			}
			app.UseRouting();
			app.UseCors("MyPolicy");
			app.UseAuthentication(); // 既然有 Identity，這行通常要加在 Authorization 之前
			app.UseAuthorization();
			app.MapControllers();

			app.Run();
		}
	}
}
