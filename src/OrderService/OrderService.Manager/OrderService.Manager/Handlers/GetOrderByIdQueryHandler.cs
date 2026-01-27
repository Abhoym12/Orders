using MediatR;
using Microsoft.Extensions.Logging;
using OrderService.DataAccess.Caching;
using OrderService.DataAccess.Interfaces;
using OrderService.Manager.Mappings;
using OrderService.Manager.Queries;
using Shared.Contracts.Dtos;

namespace OrderService.Manager.Handlers;

public class GetOrderByIdQueryHandler : IRequestHandler<GetOrderByIdQuery, OrderResponse?>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IOrderCacheService _cacheService;
    private readonly ILogger<GetOrderByIdQueryHandler> _logger;

    public GetOrderByIdQueryHandler(
        IOrderRepository orderRepository,
        IOrderCacheService cacheService,
        ILogger<GetOrderByIdQueryHandler> logger)
    {
        _orderRepository = orderRepository;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<OrderResponse?> Handle(GetOrderByIdQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fetching order {OrderId}", request.OrderId);

        // Try cache first (cache-aside pattern)
        var cachedOrder = await _cacheService.GetOrderAsync(request.OrderId);
        if (cachedOrder != null)
        {
            _logger.LogInformation("Order {OrderId} found in cache", request.OrderId);
            return cachedOrder.ToResponse();
        }

        // Fallback to database
        var order = await _orderRepository.GetByIdAsync(request.OrderId, cancellationToken);
        if (order == null)
        {
            _logger.LogWarning("Order {OrderId} not found", request.OrderId);
            return null;
        }

        // Cache for future requests
        await _cacheService.SetOrderAsync(order);

        return order.ToResponse();
    }
}
