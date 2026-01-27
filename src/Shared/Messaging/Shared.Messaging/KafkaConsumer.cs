using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Shared.Messaging;

public class KafkaConsumer : IKafkaConsumer, IDisposable
{
    private readonly KafkaSettings _settings;
    private readonly ILogger<KafkaConsumer> _logger;
    private IConsumer<string, string>? _consumer;

    public KafkaConsumer(IOptions<KafkaSettings> settings, ILogger<KafkaConsumer> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task ConsumeAsync<T>(string topic, Func<T, Task> handler, CancellationToken cancellationToken = default) where T : class
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _settings.BootstrapServers,
            GroupId = _settings.GroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        _consumer = new ConsumerBuilder<string, string>(config).Build();
        _consumer.Subscribe(topic);

        _logger.LogInformation("Started consuming from topic {Topic}", topic);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var result = _consumer.Consume(cancellationToken);
                    if (result?.Message?.Value != null)
                    {
                        var message = JsonSerializer.Deserialize<T>(result.Message.Value);
                        if (message != null)
                        {
                            await handler(message);
                            _consumer.Commit(result);
                            _logger.LogInformation("Processed message from {Topic} at offset {Offset}",
                                topic, result.Offset);
                        }
                    }
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Error consuming message from {Topic}", topic);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Consumer for {Topic} was cancelled", topic);
        }
    }

    public void Dispose()
    {
        _consumer?.Close();
        _consumer?.Dispose();
    }
}
