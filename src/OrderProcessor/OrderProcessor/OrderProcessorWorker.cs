using Microsoft.Extensions.Options;
using OrderService.DataAccess.Interfaces;
using OrderService.Engine.Enums;
using OrderService.Engine.Interfaces;
using Shared.Contracts.Events;
using Shared.Messaging;

namespace OrderProcessor;

public class OrderProcessorWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IKafkaProducer _kafkaProducer;
    private readonly OrderProcessorSettings _settings;
    private readonly ILogger<OrderProcessorWorker> _logger;

    public OrderProcessorWorker(
        IServiceScopeFactory scopeFactory,
        IKafkaProducer kafkaProducer,
        IOptions<OrderProcessorSettings> settings,
        ILogger<OrderProcessorWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _kafkaProducer = kafkaProducer;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OrderProcessor started. Polling every {Interval} minutes, batch size: {BatchSize}",
            _settings.PollingIntervalMinutes, _settings.BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingOrdersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing pending orders");
            }

            await Task.Delay(TimeSpan.FromMinutes(_settings.PollingIntervalMinutes), stoppingToken);
        }
    }

    private async Task ProcessPendingOrdersAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting to process pending orders");

        using var scope = _scopeFactory.CreateScope();
        var orderRepository = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
        var orderEngine = scope.ServiceProvider.GetRequiredService<IOrderEngine>();

        var pendingOrders = await orderRepository.GetByStatusAsync(
            OrderStatus.Pending,
            _settings.BatchSize,
            cancellationToken);

        pendingOrders = pendingOrders.Where(x => x.TotalAmount == x.AmountPaid).ToList();

        if (pendingOrders.Count == 0)
        {
            _logger.LogInformation("No pending orders to process");
            return;
        }

        _logger.LogInformation("Found {Count} pending orders to process", pendingOrders.Count);

        await Parallel.ForEachAsync(pendingOrders, cancellationToken, async (order, ct) =>
        {
            try
            {
                var previousStatus = order.Status.ToString();

                orderEngine.TransitionOrderStatus(order, OrderStatus.Processing);

                // Use a new scope for each parallel update to avoid DbContext threading issues
                using var updateScope = _scopeFactory.CreateScope();
                var updateRepository = updateScope.ServiceProvider.GetRequiredService<IOrderRepository>();
                await updateRepository.UpdateAsync(order, ct);

                var statusChangedEvent = new OrderStatusChangedEvent(
                    order.OrderId,
                    previousStatus,
                    order.Status.ToString(),
                    DateTime.UtcNow);

                await _kafkaProducer.PublishAsync(
                    KafkaTopics.OrderStatusChanged,
                    order.OrderId.ToString(),
                    statusChangedEvent,
                    ct);

                _logger.LogInformation("Order {OrderId} transitioned from {PreviousStatus} to {NewStatus}",
                    order.OrderId, previousStatus, order.Status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process order {OrderId}", order.OrderId);
            }
        });

        _logger.LogInformation("Completed processing batch of {Count} orders", pendingOrders.Count);
    }
}
