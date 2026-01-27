using MediatR;
using Shared.Contracts.Dtos;

namespace OrderService.Manager.Queries;

public record GetOrderByIdQuery(Guid OrderId) : IRequest<OrderResponse?>;
