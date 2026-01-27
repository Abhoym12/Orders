using MediatR;
using Shared.Contracts.Dtos;

namespace OrderService.Manager.Commands;

public record CancelOrderCommand(
    Guid OrderId,
    Guid UserId,
    string Reason) : IRequest<OrderResponse>;
