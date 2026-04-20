using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;
using System.Text;
using TaiwanAgri.Modules.Weather.Services;

namespace TaiwanAgri.Worker
{
	public class PestRuleEngineWorker : BackgroundService
	{
		private readonly ILogger<PestRuleEngineWorker> _logger;
		private readonly PestRuleEngine _pestRuleEngine;
		public PestRuleEngineWorker(ILogger<PestRuleEngineWorker> logger, PestRuleEngine pestRuleEngine)
		{
			_logger = logger;
			_pestRuleEngine = pestRuleEngine;
		}
		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			while(!stoppingToken.IsCancellationRequested)
			{
				try
				{
					await _pestRuleEngine.EvaluateAsync(stoppingToken);
				}
				catch (Exception ex) 
				{
					_logger.LogError(ex, "[PestRuleEngineWorker] 執行失敗");
				}
				await Task.Delay(TimeSpan.FromDays(1), stoppingToken); // 每天執行一次
			}
		}
	}
}
