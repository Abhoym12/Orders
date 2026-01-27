using MediatR;
using Shared.Contracts.Dtos;

namespace OrderService.Manager.Commands;

public record CreateOrderCommand(
    Guid UserId,
    List<CreateOrderItemRequest> Items) : IRequest<OrderResponse>;
