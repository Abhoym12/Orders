using MediatR;
using Microsoft.Extensions.Logging;
using OrderService.DataAccess.Caching;
using OrderService.DataAccess.Interfaces;
using OrderService.Engine.Exceptions;
using OrderService.Engine.Interfaces;
using OrderService.Manager.Commands;
using OrderService.Manager.Mappings;
using Shared.Contracts.Dtos;
using Shared.Contracts.Events;
using Shared.Messaging;

namespace OrderService.Manager.Handlers;

public class CancelOrderCommandHandler : IRequestHandler<CancelOrderCommand, OrderResponse>
{
    private readonly IOrderEngine _orderEngine;
    private readonly IOrderRepository _orderRepository;
    private readonly IOrderCacheService _cacheService;
    private readonly IKafkaProducer _kafkaProducer;
    private readonly ILogger<CancelOrderCommandHandler> _logger;

    public CancelOrderCommandHandler(
        IOrderEngine orderEngine,
        IOrderRepository orderRepository,
        IOrderCacheService cacheService,
        IKafkaProducer kafkaProducer,
        ILogger<CancelOrderCommandHandler> logger)
    {
        _orderEngine = orderEngine;
        _orderRepository = orderRepository;
        _cacheService = cacheService;
        _kafkaProducer = kafkaProducer;
        _logger = logger;
    }

    public async Task<OrderResponse> Handle(CancelOrderCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Cancelling order {OrderId} for user {UserId}", request.OrderId, request.UserId);

        var order = await _orderRepository.GetByIdAsync(request.OrderId, cancellationToken);

        if (order == null)
        {
            _logger.LogWarning("Order {OrderId} not found", request.OrderId);
            throw new OrderDomainException($"Order with ID {request.OrderId} not found.");
        }

        if (order.UserId != request.UserId)
        {
            _logger.LogWarning("User {UserId} not authorized to cancel order {OrderId}", request.UserId, request.OrderId);
            throw new OrderDomainException("You are not authorized to cancel this order.");
        }

        var previousStatus = order.Status.ToString();
        _orderEngine.CancelOrder(order, request.Reason);

        await _orderRepository.UpdateAsync(order, cancellationToken);
        await _cacheService.RemoveOrderAsync(order.OrderId);

        var cancelledEvent = new OrderCancelledEvent(
            order.OrderId,
            order.UserId,
            request.Reason,
            DateTime.UtcNow);

        // Publish event - don't fail the cancellation if Kafka is unavailable
        try
        {
            await _kafkaProducer.PublishAsync(
                KafkaTopics.OrderCancelled,
                order.OrderId.ToString(),
                cancelledEvent,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish OrderCancelled event for order {OrderId}. Event will be retried later.", order.OrderId);
        }

        _logger.LogInformation("Order {OrderId} cancelled successfully", order.OrderId);
        return order.ToResponse();
    }
}
