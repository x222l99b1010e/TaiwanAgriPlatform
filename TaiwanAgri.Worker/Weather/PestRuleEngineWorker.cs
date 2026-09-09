using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;
using System.Text;
using TaiwanAgri.Modules.Weather.Services;
using TaiwanAgri.Worker;

namespace TaiwanAgri.Worker.Weather
{
	public class PestRuleEngineWorker : ScheduledSyncWorkerBase
	{
		private readonly PestRuleEngine _pestRuleEngine;
		public PestRuleEngineWorker(ILogger<PestRuleEngineWorker> logger, PestRuleEngine pestRuleEngine)
			: base(logger)
		{
			_pestRuleEngine = pestRuleEngine;
		}

		protected override TimeSpan Interval => TimeSpan.FromDays(1); // 每天執行一次
		protected override string LogPrefix => "[PestRuleEngineWorker]";

		// userId 傳 null＝評估全系統所有人的規則，這是排程的職責。
		// 使用者手動觸發的那條路徑會帶自己的 userId，把範圍與例外的影響收在呼叫者身上
		protected override Task SyncAsync(CancellationToken stoppingToken)
			=> _pestRuleEngine.EvaluateAsync(null, stoppingToken);
	}
}
