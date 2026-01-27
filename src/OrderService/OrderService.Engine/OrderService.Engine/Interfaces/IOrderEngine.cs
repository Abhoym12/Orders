using OrderService.Engine.Enums;
using OrderService.Engine.Models;

namespace OrderService.Engine.Interfaces;

public interface IOrderEngine
{
    Order CreateOrder(Guid userId, List<(Guid productId, string productName, int quantity, decimal price)> items);
    void CancelOrder(Order order, string reason);
    void TransitionOrderStatus(Order order, OrderStatus newStatus);
    bool CanCancel(Order order);
    bool CanTransitionTo(Order order, OrderStatus newStatus);
}
