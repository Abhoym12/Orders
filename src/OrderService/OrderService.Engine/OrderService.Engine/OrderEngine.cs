using Microsoft.Extensions.Logging;
using OrderService.Engine.Enums;
using OrderService.Engine.Interfaces;
using OrderService.Engine.Models;

namespace OrderService.Engine;

public class OrderEngine : IOrderEngine
{
    private readonly ILogger<OrderEngine> _logger;

    public OrderEngine(ILogger<OrderEngine> logger)
    {
        _logger = logger;
    }

    public Order CreateOrder(Guid userId, List<(Guid productId, string productName, int quantity, decimal price)> items)
    {
        _logger.LogInformation("Creating order for user {UserId} with {ItemCount} items", userId, items.Count);

        var orderItems = items
            .Select(i => OrderItem.Create(i.productId, i.productName, i.quantity, i.price))
            .ToList();

        var order = Order.Create(userId, orderItems);

        _logger.LogInformation("Order {OrderId} created successfully for user {UserId}", order.OrderId, userId);
        return order;
    }

    public void CancelOrder(Order order, string reason)
    {
        _logger.LogInformation("Attempting to cancel order {OrderId}. Reason: {Reason}", order.OrderId, reason);
        order.Cancel(reason);
        _logger.LogInformation("Order {OrderId} cancelled successfully", order.OrderId);
    }

    public void TransitionOrderStatus(Order order, OrderStatus newStatus)
    {
        var previousStatus = order.Status;
        _logger.LogInformation("Transitioning order {OrderId} from {PreviousStatus} to {NewStatus}",
            order.OrderId, previousStatus, newStatus);

        order.TransitionTo(newStatus);

        _logger.LogInformation("Order {OrderId} transitioned successfully to {NewStatus}", order.OrderId, newStatus);
    }

    public bool CanCancel(Order order)
    {
        return order.Status == OrderStatus.Pending;
    }

    public bool CanTransitionTo(Order order, OrderStatus newStatus)
    {
        var validTransitions = new Dictionary<OrderStatus, OrderStatus[]>
        {
            { OrderStatus.Pending, new[] { OrderStatus.Processing, OrderStatus.Cancelled } },
            { OrderStatus.Processing, new[] { OrderStatus.Shipped } },
            { OrderStatus.Shipped, new[] { OrderStatus.Delivered } },
            { OrderStatus.Delivered, Array.Empty<OrderStatus>() },
            { OrderStatus.Cancelled, Array.Empty<OrderStatus>() }
        };

        return validTransitions.TryGetValue(order.Status, out var allowed) && allowed.Contains(newStatus);
    }
}
