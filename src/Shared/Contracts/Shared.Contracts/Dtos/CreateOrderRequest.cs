namespace Shared.Contracts.Dtos;

public record CreateOrderRequest(
    List<CreateOrderItemRequest> Items);

public record CreateOrderItemRequest(
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal Price);
