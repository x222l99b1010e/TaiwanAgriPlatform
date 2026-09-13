using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TaiwanAgri.Core.Infrastructure.Data;

namespace TaiwanAgri.Web.HealthChecks
{
	/// <summary>
	/// 資料庫可不可以連得上。用 CanConnectAsync 而不是跑一句查詢：
	/// 這支檢查的目的是「確認連得到」與「把休眠中的資料庫叫醒」，不是驗證資料內容。
	/// <para>
	/// ⚠ 這支檢查刻意只掛在 /health/ready，不掛在 /health。部署形態是 Azure SQL
	/// serverless（閒置自動暫停、每月只有固定的 vCore 秒額度），任何定時 ping 只要碰到
	/// 資料庫就會一直把它叫醒、把額度燒光。所以「還活著嗎」與「可以服務了嗎」
	/// 必須是兩支不同的端點
	/// </para>
	/// </summary>
	public sealed class DatabaseHealthCheck : IHealthCheck
	{
		private readonly CoreDbContext _context;

		public DatabaseHealthCheck(CoreDbContext context)
		{
			_context = context;
		}

		public async Task<HealthCheckResult> CheckHealthAsync(
			HealthCheckContext context,
			CancellationToken cancellationToken = default)
		{
			try
			{
				return await _context.Database.CanConnectAsync(cancellationToken)
					? HealthCheckResult.Healthy("資料庫連線正常")
					: HealthCheckResult.Unhealthy("資料庫連線失敗");
			}
			catch (Exception ex)
			{
				// CanConnectAsync 對「連不上」是回 false，但連線字串本身有問題
				// （格式錯、憑證錯）時是拋例外。健康檢查端點自己不能因此回 500——
				// 那會讓「服務掛了」與「檢查掛了」在監控上長得一樣
				return HealthCheckResult.Unhealthy("資料庫連線失敗", ex);
			}
		}
	}
}
