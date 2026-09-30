using Microsoft.EntityFrameworkCore;

namespace TaiwanAgri.Core.Extensions
{
	/// <summary>
	/// 所有 DbContext 共用的 SQL Server 連線設定：遇到暫時性錯誤自動重試。
	/// Web 與 Worker 兩個 DI 容器都走這一支，重試參數只在這裡改。
	/// <para>
	/// 部署目標 Azure SQL serverless 閒置會自動暫停，下一個連線才把它叫醒；
	/// 恢復的 30～60 秒內，每個連線都會拿到 40613（資料庫目前無法使用）。
	/// 不重試的話，Web 啟動時寫種子資料那一步會讓整個程式崩潰，平台連續重開失敗還會封鎖站台；
	/// Worker 的每日同步與「站台開著、資料庫自己睡著之後的第一個請求」也會直接失敗。
	/// </para>
	/// <para>
	/// EF 內建的暫時性錯誤清單已含 40613，不必另外加錯誤碼。上限 6 次、單次最長等 30 秒：
	/// 指數退避的累計等待約一分鐘，再加上每次連線本身花的時間，蓋得過恢復期。
	/// </para>
	/// <para>
	/// ⚠ 開了重試之後，程式自己開的交易（<c>BeginTransaction</c>）必須包進
	/// <c>Database.CreateExecutionStrategy().ExecuteAsync(...)</c>，否則 EF 會丟
	/// <see cref="InvalidOperationException"/>——整批重做的單位要由呼叫端決定，EF 無從代勞。
	/// </para>
	/// </summary>
	public static class SqlServerRetryExtensions
	{
		public const int MaxRetryCount = 6;

		public static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(30);

		public static DbContextOptionsBuilder UseSqlServerWithRetry(
			this DbContextOptionsBuilder options, string? connectionString) =>
			options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(
				maxRetryCount: MaxRetryCount,
				maxRetryDelay: MaxRetryDelay,
				errorNumbersToAdd: null));
	}
}
