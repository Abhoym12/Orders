namespace Shared.Contracts.Dtos;

public record OrderResponse(
    Guid OrderId,
    Guid UserId,
    string Status,
    decimal TotalAmount,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    List<OrderItemResponse> Items);

public record OrderItemResponse(
    Guid Id,
    Guid ProductId,
    int Quantity,
    decimal Price);
