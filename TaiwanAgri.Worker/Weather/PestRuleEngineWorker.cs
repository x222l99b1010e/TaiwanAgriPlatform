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

		protected override Task SyncAsync(CancellationToken stoppingToken)
			=> _pestRuleEngine.EvaluateAsync(stoppingToken);
	}
}
