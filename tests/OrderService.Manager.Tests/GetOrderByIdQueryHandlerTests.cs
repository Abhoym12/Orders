using FakeItEasy;
using Microsoft.Extensions.Logging;
using OrderService.DataAccess.Caching;
using OrderService.DataAccess.Interfaces;
using OrderService.Engine.Enums;
using OrderService.Engine.Models;
using OrderService.Manager.Handlers;
using OrderService.Manager.Queries;

namespace OrderService.Manager.Tests;

public class GetOrderByIdQueryHandlerTests
{
    private readonly IOrderRepository _orderRepository;
    private readonly IOrderCacheService _cacheService;
    private readonly ILogger<GetOrderByIdQueryHandler> _logger;
    private readonly GetOrderByIdQueryHandler _sut;

    public GetOrderByIdQueryHandlerTests()
    {
        _orderRepository = A.Fake<IOrderRepository>();
        _cacheService = A.Fake<IOrderCacheService>();
        _logger = A.Fake<ILogger<GetOrderByIdQueryHandler>>();

        _sut = new GetOrderByIdQueryHandler(
            _orderRepository,
            _cacheService,
            _logger);
    }

    [Fact]
    public async Task Handle_OrderExistsInCache_ReturnsFromCacheWithoutDbCall()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var query = new GetOrderByIdQuery(orderId);

        var cachedOrder = CreateTestOrder(userId, orderId);
        A.CallTo(() => _cacheService.GetOrderAsync(orderId))
            .Returns(cachedOrder);

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(orderId, result.OrderId);
        A.CallTo(() => _orderRepository.GetByIdAsync(A<Guid>._, A<CancellationToken>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task Handle_OrderNotInCache_FetchesFromDatabase()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var query = new GetOrderByIdQuery(orderId);

        A.CallTo(() => _cacheService.GetOrderAsync(orderId))
            .Returns((Order?)null);

        var dbOrder = CreateTestOrder(userId, orderId);
        A.CallTo(() => _orderRepository.GetByIdAsync(orderId, A<CancellationToken>._))
            .Returns(dbOrder);

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(orderId, result.OrderId);
        A.CallTo(() => _orderRepository.GetByIdAsync(orderId, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Handle_OrderNotInCache_CachesOrderAfterFetch()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var query = new GetOrderByIdQuery(orderId);

        A.CallTo(() => _cacheService.GetOrderAsync(orderId))
            .Returns((Order?)null);

        var dbOrder = CreateTestOrder(userId, orderId);
        A.CallTo(() => _orderRepository.GetByIdAsync(orderId, A<CancellationToken>._))
            .Returns(dbOrder);

        // Act
        await _sut.Handle(query, CancellationToken.None);

        // Assert
        A.CallTo(() => _cacheService.SetOrderAsync(dbOrder))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Handle_OrderNotFound_ReturnsNull()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var query = new GetOrderByIdQuery(orderId);

        A.CallTo(() => _cacheService.GetOrderAsync(orderId))
            .Returns((Order?)null);
        A.CallTo(() => _orderRepository.GetByIdAsync(orderId, A<CancellationToken>._))
            .Returns((Order?)null);

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_OrderNotFoundInDb_DoesNotCache()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var query = new GetOrderByIdQuery(orderId);

        A.CallTo(() => _cacheService.GetOrderAsync(orderId))
            .Returns((Order?)null);
        A.CallTo(() => _orderRepository.GetByIdAsync(orderId, A<CancellationToken>._))
            .Returns((Order?)null);

        // Act
        await _sut.Handle(query, CancellationToken.None);

        // Assert
        A.CallTo(() => _cacheService.SetOrderAsync(A<Order>._))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task Handle_CacheHit_ReturnsCorrectOrderResponse()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var query = new GetOrderByIdQuery(orderId);

        var cachedOrder = CreateTestOrder(userId, orderId);
        A.CallTo(() => _cacheService.GetOrderAsync(orderId))
            .Returns(cachedOrder);

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(orderId, result.OrderId);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(OrderStatus.Pending.ToString(), result.Status);
    }

    private static Order CreateTestOrder(Guid userId, Guid orderId)
    {
        var items = new List<OrderItem>
        {
            OrderItem.Create(Guid.NewGuid(), "Test Product", 1, 10.00m)
        };
        var order = Order.Create(userId, items);

        // Use reflection to set internal OrderId
        typeof(Order).GetProperty(nameof(Order.OrderId))!.SetValue(order, orderId);

        return order;
    }
}
