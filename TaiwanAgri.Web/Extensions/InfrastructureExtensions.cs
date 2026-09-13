using StackExchange.Redis;
using TaiwanAgri.Web.HealthChecks;
using TaiwanAgri.Web.Services;

namespace TaiwanAgri.Web.Extensions
{
	public static class InfrastructureExtensions
	{
		/// <summary>CORS 原則名稱。原本叫 "MyPolicy"，名字說不出它是給誰用的</summary>
		public const string FrontendCorsPolicy = "FrontendCors";

		/// <summary>
		/// 標記「這項檢查會碰外部資源」的標籤。只有 /health/ready 會跑帶這個標籤的檢查
		/// </summary>
		public const string ReadinessTag = "ready";

		/// <summary>
		/// Development 未設定 CORS 來源時的啟動警告。
		/// 由 Program.cs 在 DI 容器建好之後記錄——設定階段還拿不到 ILogger，
		/// 而寫 Console 進不了正式的 log pipeline，等於警告了沒人收得到
		/// </summary>
		public const string CorsOriginsMissingWarning =
			"未設定 Cors:AllowedOrigins，本服務將拒絕所有跨來源請求。" +
			"目前只有同源請求（例如經由 Vite proxy）能通過；若前端要直接呼叫本 API，" +
			"請填入允許的來源，或以 Cors:SameOriginOnly = true 明確宣告不需要 CORS。";

		/// <summary>
		/// 未設定 Redis 連線字串時的啟動警告。降級本身是正常運作路徑，
		/// 但「以為連了 Redis、其實是行程內記憶體」必須留下痕跡，否則多執行個體部署時
		/// 快取不共用的症狀無從追溯
		/// </summary>
		public const string RedisNotConfiguredWarning =
			"未設定 ConnectionStrings:Redis，分散式快取改用行程內記憶體實作（AddDistributedMemoryCache）。" +
			"單一執行個體部署時這是正確行為；部署多個執行個體時快取不會共用，請改設 Redis 連線字串。";

		/// <summary>
		/// 未設定 RabbitMQ 主機時的啟動警告。同上：不註冊 consumer 是正常路徑，
		/// 但要看得出來「事件驅動的快取失效這一段沒有在跑」
		/// </summary>
		public const string MessageBrokerNotConfiguredWarning =
			"未設定 RabbitMQ:HostName，未註冊 PriceUpdatedConsumer，事件驅動的快取失效不會運作。" +
			"快取仍由 25 小時 TTL 兜底；要啟用事件驅動失效請填入 broker 主機。";

		/// <summary>
		/// <c>Cors:AllowedOrigins</c> 沒填、且沒有用 <c>Cors:SameOriginOnly</c> 宣告不需要 CORS。
		/// <para>
		/// 這個狀態下 <c>WithOrigins([])</c> 會拒絕所有跨來源請求，而且不留任何訊息——
		/// 前端在瀏覽器端全掛，伺服器端一切正常。本機察覺不到是因為 Vite proxy
		/// 讓請求變成同源、根本不經過 CORS。
		/// </para>
		/// <para>
		/// 之所以要 SameOriginOnly 這個旗標：「忘了填」與「刻意只走同源 proxy」在設定檔裡
		/// 長得一模一樣，程式無從分辨。把後者變成要寫下來的宣告，剩下的空白就只有一種解釋
		/// </para>
		/// </summary>
		public static bool IsCorsOriginsMissing(IConfiguration configuration) =>
			(configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? []).Length == 0
			&& !configuration.GetValue<bool>("Cors:SameOriginOnly");

		/// <summary>
		/// 有沒有設定 Redis 連線字串。沒設定時改註冊 <c>AddDistributedMemoryCache</c>——
		/// 那是 <c>IDistributedCache</c> 的另一個正式實作，不是「關掉快取」，
		/// 所以 Cache-Aside 的呼叫端一行都不用改。
		/// <para>
		/// 代價只有一個：多個執行個體之間不共用快取，所以這條路徑只在單一執行個體部署時成立
		/// （限流計數器同樣是 in-memory，依賴的是同一個前提）
		/// </para>
		/// </summary>
		public static bool IsRedisConfigured(IConfiguration configuration) =>
			!string.IsNullOrWhiteSpace(configuration.GetConnectionString("Redis"));

		/// <summary>
		/// 有沒有設定 RabbitMQ 主機。沒設定時不註冊 <see cref="PriceUpdatedConsumer"/>。
		/// <para>
		/// 之所以是「不註冊」而不是「註冊了連不上再說」：該類別在 <c>StartAsync</c> 裡
		/// 建連線且不接例外，而 <c>IHostedService.StartAsync</c> 拋例外的語意是
		/// 「這個服務起不來 ⇒ 整個 host 起不來」——連不上 broker 時整站回 503，
		/// 不是少一個背景功能。這不是推測：把主機指向不存在的位址時，程序會以
		/// Unhandled exception 結束、HTTP 連接埠根本沒開
		/// </para>
		/// <para>
		/// 少了它不影響服務完整性：這支 consumer 目前只負責快取失效，而快取另有 25 小時 TTL 兜底
		/// </para>
		/// </summary>
		public static bool IsMessageBrokerConfigured(IConfiguration configuration) =>
			!string.IsNullOrWhiteSpace(configuration["RabbitMQ:HostName"]);

		/// <summary>
		/// 啟動時檢查 CORS 來源設定，非 Development 環境缺設定就讓啟動直接失敗。
		/// <para>
		/// Development 只警告不中斷：本機經 Vite proxy 存取是同源，本來就不需要 CORS。
		/// 其他環境沒有這個豁免——設定漏了只會在使用者的瀏覽器裡現形，伺服器端毫無跡象，
		/// 啟動時失敗比上線後才發現便宜得多
		/// </para>
		/// </summary>
		public static void ValidateCorsConfiguration(IConfiguration configuration, IHostEnvironment environment)
		{
			if (environment.IsDevelopment() || !IsCorsOriginsMissing(configuration))
			{
				return;
			}

			throw new InvalidOperationException(
				"Cors:AllowedOrigins 未設定。前端若直接呼叫本 API，所有請求都會被瀏覽器擋掉，" +
				"而伺服器端不會留下任何錯誤紀錄。請填入允許的來源（例：https://your-frontend.example）；" +
				"若本服務只透過同源 proxy 對外、確實不需要 CORS，" +
				"請明確設定 Cors:SameOriginOnly = true。");
		}

		/// <summary>
		/// Redis 連線選項：連不上時要「快速失敗」，不只是「不要拋例外」。
		/// <para>
		/// 這組值是實測逼出來的。MarketService 的快取存取包上 try/catch 之後，
		/// Redis 指向不存在的主機時 <c>GET /api/market/prices</c> 確實從 500 變成 200——
		/// 但要 <b>12.45 秒</b>：讀一次、寫一次，各自在 backlog 裡排隊等到逾時。
		/// 降級成這樣跟壞掉沒有差別，所以預設的「斷線時把指令排進 backlog 等重連」
		/// 必須改成直接失敗，讓呼叫端的降級路徑立刻生效
		/// </para>
		/// <para>
		/// 這段知道「底下是 Redis」是刻意的，而且只有這裡可以知道——
		/// 這是組裝根（composition root），本來就負責決定用哪個實作；
		/// 相對地 MarketService 只認識 IDistributedCache，不為了接特定例外型別而加套件參考
		/// </para>
		/// </summary>
		public static ConfigurationOptions BuildRedisOptions(string connectionString)
		{
			var options = ConfigurationOptions.Parse(connectionString);

			// 啟動時連不上不讓建立連線的動作整個失敗——Redis 是加速層，
			// 它不可用時服務要照樣起得來（比照 RabbitMQ 那一側的判斷，只是這裡有原生開關可用）
			options.AbortOnConnectFail = false;

			// 斷線期間指令直接失敗，不排進 backlog 等逾時。這是 12.45 秒的成因
			options.BacklogPolicy = BacklogPolicy.FailFast;

			// 連線與指令的等待上限。使用者正在等這個請求，等 5 秒去確認一件已經知道的事沒有意義
			options.ConnectTimeout = 1000;
			options.ConnectRetry = 1;
			options.SyncTimeout = 1000;
			options.AsyncTimeout = 1000;

			return options;
		}

		public static IServiceCollection AddInfrastructure(
			this IServiceCollection services,
			IConfiguration configuration,
			IHostEnvironment environment)
		{
			// Redis（沒設連線字串就退回行程內記憶體實作，理由見 IsRedisConfigured）
			if (IsRedisConfigured(configuration))
			{
				services.AddStackExchangeRedisCache(options =>
				{
					options.ConfigurationOptions = BuildRedisOptions(configuration.GetConnectionString("Redis")!);
				});
			}
			else
			{
				services.AddDistributedMemoryCache();
			}

			// CORS
			ValidateCorsConfiguration(configuration, environment);
			var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

			services.AddCors(options =>
			{
				options.AddPolicy(FrontendCorsPolicy, policy =>
				{
					policy.WithOrigins(allowedOrigins)
						  .AllowAnyMethod()
						  .AllowAnyHeader()
						  .AllowCredentials();
				});
			});

			// RabbitMQ Consumer（沒設主機就不註冊，理由見 IsMessageBrokerConfigured）
			if (IsMessageBrokerConfigured(configuration))
			{
				services.AddHostedService<PriceUpdatedConsumer>();
			}

			// 健康檢查。兩支端點刻意分開，理由見 DatabaseHealthCheck 的註解：
			// /health 只回答「這個程序還活著嗎」（不碰任何外部資源），
			// /health/ready 才碰資料庫——後者兼作錄影／展示前的暖機入口，
			// 一次請求就把 App Service 的冷啟動與 Azure SQL 的喚醒都做完
			services.AddHealthChecks()
					.AddCheck<DatabaseHealthCheck>("database", tags: [ReadinessTag]);

			// Web API 基礎
			services.AddControllers();
			services.AddProblemDetails(); // 註冊標準錯誤格式服務
			services.AddEndpointsApiExplorer();
			services.AddSwaggerGen();

			return services;
		}
	}
}