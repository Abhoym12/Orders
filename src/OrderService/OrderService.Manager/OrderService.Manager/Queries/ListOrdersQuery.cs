using MediatR;
using Shared.Contracts.Dtos;

namespace OrderService.Manager.Queries;

public record ListOrdersQuery(Guid? UserId = null, string? Status = null) : IRequest<List<OrderResponse>>;
