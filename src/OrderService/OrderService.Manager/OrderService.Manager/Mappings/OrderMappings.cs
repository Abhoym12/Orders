using OrderService.Engine.Models;
using Shared.Contracts.Dtos;
using Shared.Contracts.Events;

namespace OrderService.Manager.Mappings;

public static class OrderMappings
{
    public static OrderResponse ToResponse(this Order order)
    {
        return new OrderResponse(
            order.OrderId,
            order.UserId,
            order.Status.ToString(),
            order.TotalAmount,
            order.CreatedAt,
            order.UpdatedAt,
            order.Items.Select(i => new OrderItemResponse(
                i.Id,
                i.ProductId,
                i.Quantity,
                i.Price)).ToList());
    }

    public static OrderCreatedEvent ToCreatedEvent(this Order order)
    {
        return new OrderCreatedEvent(
            order.OrderId,
            order.UserId,
            order.TotalAmount,
            order.CreatedAt,
            order.Items.Select(i => new OrderItemDto(
                i.ProductId,
                i.Quantity,
                i.Price)).ToList());
    }
}
