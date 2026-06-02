using Microsoft.Extensions.Caching.Distributed;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace TaiwanAgri.Web.Services
{
	public class PriceUpdatedConsumer : BackgroundService
	{
		private readonly ILogger<PriceUpdatedConsumer> _logger;
		private readonly IDistributedCache _cache;
		private IConnection? _connection;
		private IChannel? _channel;
		private string _queueName = string.Empty;

		public PriceUpdatedConsumer(ILogger<PriceUpdatedConsumer> logger, IDistributedCache cache)
		{
			_logger = logger;
			_cache = cache;
		}

		public override async Task StartAsync(CancellationToken cancellationToken)
		{
			// 建立連線（應用程式啟動時執行一次）
			var factory = new ConnectionFactory { HostName = "localhost" };
			_connection = await factory.CreateConnectionAsync(cancellationToken);
			_channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

			// 宣告同一個 exchange（必須與 Publisher 設定完全一致）
			await _channel.ExchangeDeclareAsync(
				exchange: "agri.events",
				type: ExchangeType.Topic,
				durable: true,
				cancellationToken: cancellationToken);

			// 宣告 Queue 並綁定到 exchange
			var queueResult = await _channel.QueueDeclareAsync(cancellationToken: cancellationToken);
			_queueName = queueResult.QueueName;
			await _channel.QueueBindAsync(
				queue: queueResult.QueueName,
				exchange: "agri.events",
				routingKey: "agri.market.priceUpdated",
				cancellationToken: cancellationToken);

			_logger.LogInformation("[PriceUpdatedConsumer] 已連線 RabbitMQ，等待事件...");

			await base.StartAsync(cancellationToken);
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			var consumer = new AsyncEventingBasicConsumer(_channel!);

			consumer.ReceivedAsync += async (_, ea) =>
			{
				var body = Encoding.UTF8.GetString(ea.Body.ToArray());
				_logger.LogInformation("[PriceUpdatedConsumer] 收到事件：{Body}，開始清除 Redis cache", body);

				// 骨架階段：清除所有 market:prices 開頭的 key
				// W15 之後會改為精確 invalidation
				_logger.LogInformation("[PriceUpdatedConsumer] Cache invalidation 預留位置（W15 實作）");

				await _channel!.BasicAckAsync(ea.DeliveryTag, multiple: false);
			};

			await _channel!.BasicConsumeAsync(
				queue: _queueName,
				autoAck: false,
				consumer: consumer,
				cancellationToken: stoppingToken);

			// 保持存活直到應用程式關閉
			await Task.Delay(Timeout.Infinite, stoppingToken);
		}

		public override async Task StopAsync(CancellationToken cancellationToken)
		{
			// 應用程式關閉時清理連線
			if (_channel is not null) await _channel.CloseAsync(cancellationToken);
			if (_connection is not null) await _connection.CloseAsync(cancellationToken);
			await base.StopAsync(cancellationToken);
		}
	}
}