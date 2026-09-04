using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using TaiwanAgri.Modules.Weather.Data;
using TaiwanAgri.Modules.Weather.Dtos.WorkerResponses;
using TaiwanAgri.Modules.Weather.Entities;

namespace TaiwanAgri.Modules.Weather.Services
{
	public class PestRuleEngine
	{
		private readonly ILogger<PestRuleEngine> _logger;
		private readonly IServiceScopeFactory _scopeFactory;

		public PestRuleEngine(ILogger<PestRuleEngine> logger, IServiceScopeFactory scopeFactory)
		{
			_logger = logger;
			_scopeFactory = scopeFactory;
		}

		// 已知效能債：規則引擎逐條規則各查一次 DB（N+1）。目前規則數量與觸發頻率下尚未構成瓶頸，
		// 未排程優化，待實際負載出現效能問題再處理。

		public async Task EvaluateAsync(CancellationToken cancellationToken)
		{
			using var scope = _scopeFactory.CreateScope();
			var db = scope.ServiceProvider.GetRequiredService<WeatherDbContext>();
			var now = DateTime.UtcNow;
			// 刪除已過期的通知
			var expired = await db.UserNotifications
				.Where(n => n.ExpireAt != null && n.ExpireAt < now)
				.ToListAsync(cancellationToken);
			db.UserNotifications.RemoveRange(expired);
			await db.SaveChangesAsync(cancellationToken);

			var activeRules = await db.PestRuleConfigs
				.Where(p => p.IsActive)
				.ToListAsync(cancellationToken);

			foreach (var rule in activeRules)
			{
				switch(rule.RuleType)
				{
					case "Numeric":
						if (rule.Threshold == null)
						{
							_logger.LogWarning("[PestRuleEngine] 規則 {RuleId} 的 Threshold 為 null，跳過", rule.Id);
							continue;
						}
						var match = await db.PestDecadeSummaries
							.Where(p => p.Average > rule.Threshold.Value)
							.ToListAsync(cancellationToken);
						foreach (var item in match)
						{
							var exists = await db.UserNotifications
								.AnyAsync(n => n.PestRuleConfigId == rule.Id && n.SourceRecordId == item.Id);
							if (exists)
							{
								_logger.LogInformation("[PestRuleEngine] 規則 {RuleId} 已存在相同的通知，跳過", rule.Id);
								continue;
							}
							_logger.LogInformation("[PestRuleEngine] 規則 {RuleId} 觸發新通知，來源記錄 Id: {SourceRecordId}", rule.Id, item.Id);
							var notification = new UserNotification
							{
								UserId = rule.UserId,
								PestRuleConfigId = rule.Id,
								SourceRecordId = item.Id,
								Message = $"規則 {rule.Id} 觸發：平均值 {item.Average} 超過閾值 {rule.Threshold.Value}",
								TriggeredAt = now,
								ExpireAt = now.AddDays(rule.ExpiryDays) // 數值型規則的通知過期天數
							};
							await db.UserNotifications.AddAsync(notification, cancellationToken);
						}
						await db.SaveChangesAsync(cancellationToken);
						break;
					case "Event":
						switch (rule.SourceTable)
						{
							case "PlantEpidemic":
								if (rule.FilterJson == null)
								{
									_logger.LogWarning("[PestRuleEngine] 規則 {RuleId} 的 FilterJson 為 null，跳過", rule.Id);
									continue;
								}
								var filter = JsonSerializer.Deserialize<PestRuleFilter>(rule.FilterJson);
								if (filter == null)
								{
									_logger.LogWarning("[PestRuleEngine] 規則 {RuleId} 的 FilterJson 反序列化失敗，跳過", rule.Id);
									continue;
								}
								var matchedAlerts = await db.PestAlerts
									.Where(p => p.Cities.Any(c => c.CityName.Contains(filter.City))
									&& p.Crops.Any(cr => cr.CropName.Contains(filter.PlantName)))
									.ToListAsync(cancellationToken);
								foreach (var item in matchedAlerts)
								{
									var exists = await db.UserNotifications
										.AnyAsync(n => n.PestRuleConfigId == rule.Id && n.SourceRecordId == item.Id);
									if (exists)
									{
										_logger.LogInformation("[PestRuleEngine] 規則 {RuleId} 已存在相同的通知，跳過", rule.Id);
										continue;
									}
									_logger.LogInformation("[PestRuleEngine] 規則 {RuleId} 觸發新通知，來源記錄 Id: {SourceRecordId}", rule.Id, item.Id);
									var notification = new UserNotification
									{
										UserId = rule.UserId,
										PestRuleConfigId = rule.Id,
										SourceRecordId = item.Id,
										Message = $"規則 {rule.Id} 觸發：植物疫情警報 - {item.Subject}",
										TriggeredAt = now,
										ExpireAt = now.AddDays(rule.ExpiryDays) // 事件型規則的通知過期天數
									};
									await db.UserNotifications.AddAsync(notification, cancellationToken);
								}
								await db.SaveChangesAsync(cancellationToken);
								break;
							case "TreePest":
								_logger.LogWarning("[PestRuleEngine] TreePest 尚未實作");
								break;
							default:
								_logger.LogWarning("[PestRuleEngine] 未知的 SourceTable: {SourceTable}", rule.SourceTable);
								break;
						}
						break;
					default:
						_logger.LogWarning("[PestRuleEngine] 未知的 RuleType: {RuleType}", rule.RuleType);
						break;
				}
			}
		}
	}
}
