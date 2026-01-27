using MediatR;
using Microsoft.Extensions.Logging;
using OrderService.DataAccess.Interfaces;
using OrderService.Engine.Enums;
using OrderService.Manager.Mappings;
using OrderService.Manager.Queries;
using Shared.Contracts.Dtos;

namespace OrderService.Manager.Handlers;

public class ListOrdersQueryHandler : IRequestHandler<ListOrdersQuery, List<OrderResponse>>
{
    private readonly IOrderRepository _orderRepository;
    private readonly ILogger<ListOrdersQueryHandler> _logger;

    public ListOrdersQueryHandler(
        IOrderRepository orderRepository,
        ILogger<ListOrdersQueryHandler> logger)
    {
        _orderRepository = orderRepository;
        _logger = logger;
    }

    public async Task<List<OrderResponse>> Handle(ListOrdersQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Listing orders with filters UserId={UserId}, Status={Status}",
            request.UserId, request.Status);

        OrderStatus? statusFilter = null;
        if (!string.IsNullOrEmpty(request.Status) &&
            Enum.TryParse<OrderStatus>(request.Status, ignoreCase: true, out var parsedStatus))
        {
            statusFilter = parsedStatus;
        }

        var orders = await _orderRepository.GetAllAsync(request.UserId, statusFilter, cancellationToken);

        _logger.LogInformation("Found {Count} orders", orders.Count);
        return orders.Select(o => o.ToResponse()).ToList();
    }
}
