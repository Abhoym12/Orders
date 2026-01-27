namespace Shared.Contracts.Events;

public record OrderCreatedEvent(
    Guid OrderId,
    Guid UserId,
    decimal TotalAmount,
    DateTime CreatedAt,
    List<OrderItemDto> Items);

public record OrderItemDto(
    Guid ProductId,
    int Quantity,
    decimal Price);
