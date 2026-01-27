using FakeItEasy;
using Microsoft.Extensions.Logging;
using OrderService.DataAccess.Caching;
using OrderService.DataAccess.Interfaces;
using OrderService.Engine.Enums;
using OrderService.Engine.Exceptions;
using OrderService.Engine.Interfaces;
using OrderService.Engine.Models;
using OrderService.Manager.Commands;
using OrderService.Manager.Handlers;
using Shared.Messaging;

namespace OrderService.Manager.Tests;

public class CancelOrderCommandHandlerTests
{
    private readonly IOrderEngine _orderEngine;
    private readonly IOrderRepository _orderRepository;
    private readonly IOrderCacheService _cacheService;
    private readonly IKafkaProducer _kafkaProducer;
    private readonly ILogger<CancelOrderCommandHandler> _logger;
    private readonly CancelOrderCommandHandler _sut;

    public CancelOrderCommandHandlerTests()
    {
        _orderEngine = A.Fake<IOrderEngine>();
        _orderRepository = A.Fake<IOrderRepository>();
        _cacheService = A.Fake<IOrderCacheService>();
        _kafkaProducer = A.Fake<IKafkaProducer>();
        _logger = A.Fake<ILogger<CancelOrderCommandHandler>>();

        _sut = new CancelOrderCommandHandler(
            _orderEngine,
            _orderRepository,
            _cacheService,
            _kafkaProducer,
            _logger);
    }

    [Fact]
    public async Task Handle_OrderExistsAndCanCancel_CancelsOrderSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var reason = "Changed my mind";
        var command = new CancelOrderCommand(orderId, userId, reason);

        var order = CreateTestOrder(userId, orderId, OrderStatus.Pending);
        A.CallTo(() => _orderRepository.GetByIdAsync(orderId, A<CancellationToken>._))
            .Returns(order);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        A.CallTo(() => _orderEngine.CancelOrder(order, reason))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Handle_OrderNotFound_ThrowsOrderDomainException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var command = new CancelOrderCommand(orderId, userId, "Test reason");

        A.CallTo(() => _orderRepository.GetByIdAsync(orderId, A<CancellationToken>._))
            .Returns((Order?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<OrderDomainException>(() =>
            _sut.Handle(command, CancellationToken.None));
        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public async Task Handle_UserNotOwner_ThrowsOrderDomainException()
    {
        // Arrange
        var ownerUserId = Guid.NewGuid();
        var requestingUserId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var command = new CancelOrderCommand(orderId, requestingUserId, "Test reason");

        var order = CreateTestOrder(ownerUserId, orderId, OrderStatus.Pending);
        A.CallTo(() => _orderRepository.GetByIdAsync(orderId, A<CancellationToken>._))
            .Returns(order);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<OrderDomainException>(() =>
            _sut.Handle(command, CancellationToken.None));
        Assert.Contains("not authorized", exception.Message);
    }

    [Fact]
    public async Task Handle_ValidCancel_UpdatesOrderInRepository()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var command = new CancelOrderCommand(orderId, userId, "Test reason");

        var order = CreateTestOrder(userId, orderId, OrderStatus.Pending);
        A.CallTo(() => _orderRepository.GetByIdAsync(orderId, A<CancellationToken>._))
            .Returns(order);

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        A.CallTo(() => _orderRepository.UpdateAsync(order, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Handle_ValidCancel_RemovesOrderFromCache()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var command = new CancelOrderCommand(orderId, userId, "Test reason");

        var order = CreateTestOrder(userId, orderId, OrderStatus.Pending);
        A.CallTo(() => _orderRepository.GetByIdAsync(orderId, A<CancellationToken>._))
            .Returns(order);

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        A.CallTo(() => _cacheService.RemoveOrderAsync(orderId))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Handle_ValidCancel_PublishesOrderCancelledEvent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var command = new CancelOrderCommand(orderId, userId, "Test reason");

        var order = CreateTestOrder(userId, orderId, OrderStatus.Pending);
        A.CallTo(() => _orderRepository.GetByIdAsync(orderId, A<CancellationToken>._))
            .Returns(order);

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert - verify Kafka was called with expected topic and key
        A.CallTo(_kafkaProducer)
            .Where(call => call.Method.Name == "PublishAsync"
                && call.Arguments[0]!.Equals(KafkaTopics.OrderCancelled)
                && call.Arguments[1]!.Equals(orderId.ToString()))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Handle_EngineCancelThrows_PropagatesException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var command = new CancelOrderCommand(orderId, userId, "Test reason");

        var order = CreateTestOrder(userId, orderId, OrderStatus.Processing);
        A.CallTo(() => _orderRepository.GetByIdAsync(orderId, A<CancellationToken>._))
            .Returns(order);
        A.CallTo(() => _orderEngine.CancelOrder(order, A<string>._))
            .Throws(new OrderCancellationException("Processing"));

        // Act & Assert
        await Assert.ThrowsAsync<OrderCancellationException>(() =>
            _sut.Handle(command, CancellationToken.None));
    }

    private static Order CreateTestOrder(Guid userId, Guid orderId, OrderStatus status)
    {
        var items = new List<OrderItem>
        {
            OrderItem.Create(Guid.NewGuid(), "Test Product", 1, 10.00m)
        };
        var order = Order.Create(userId, items);

        // Use reflection to set internal properties
        typeof(Order).GetProperty(nameof(Order.OrderId))!.SetValue(order, orderId);
        typeof(Order).GetProperty(nameof(Order.Status))!.SetValue(order, status);

        return order;
    }
}
