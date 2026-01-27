using MediatR;
using Microsoft.Extensions.Logging;
using OrderService.DataAccess.Caching;
using OrderService.DataAccess.Interfaces;
using OrderService.Engine.Interfaces;
using OrderService.Manager.Commands;
using OrderService.Manager.Mappings;
using Shared.Contracts.Dtos;
using Shared.Messaging;

namespace OrderService.Manager.Handlers;

public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, OrderResponse>
{
    private readonly IOrderEngine _orderEngine;
    private readonly IOrderRepository _orderRepository;
    private readonly IOrderCacheService _cacheService;
    private readonly IKafkaProducer _kafkaProducer;
    private readonly ILogger<CreateOrderCommandHandler> _logger;

    public CreateOrderCommandHandler(
        IOrderEngine orderEngine,
        IOrderRepository orderRepository,
        IOrderCacheService cacheService,
        IKafkaProducer kafkaProducer,
        ILogger<CreateOrderCommandHandler> logger)
    {
        _orderEngine = orderEngine;
        _orderRepository = orderRepository;
        _cacheService = cacheService;
        _kafkaProducer = kafkaProducer;
        _logger = logger;
    }

    public async Task<OrderResponse> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating order for user {UserId}", request.UserId);

        var items = request.Items
            .Select(i => (i.ProductId, i.ProductName, i.Quantity, i.Price))
            .ToList();

        var order = _orderEngine.CreateOrder(request.UserId, items);

        await _orderRepository.AddAsync(order, cancellationToken);
        await _cacheService.SetOrderAsync(order);

        // Publish event - don't fail the order if Kafka is unavailable
        try
        {
            var orderCreatedEvent = order.ToCreatedEvent();
            await _kafkaProducer.PublishAsync(
                KafkaTopics.OrderCreated,
                order.OrderId.ToString(),
                orderCreatedEvent,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish OrderCreated event for order {OrderId}. Event will be retried later.", order.OrderId);
        }

        _logger.LogInformation("Order {OrderId} created successfully", order.OrderId);
        return order.ToResponse();
    }
}
