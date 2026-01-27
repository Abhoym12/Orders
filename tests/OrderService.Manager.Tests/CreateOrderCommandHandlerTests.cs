using FakeItEasy;
using Microsoft.Extensions.Logging;
using OrderService.DataAccess.Caching;
using OrderService.DataAccess.Interfaces;
using OrderService.Engine.Enums;
using OrderService.Engine.Interfaces;
using OrderService.Engine.Models;
using OrderService.Manager.Commands;
using OrderService.Manager.Handlers;
using Shared.Contracts.Dtos;
using Shared.Messaging;

namespace OrderService.Manager.Tests;

public class CreateOrderCommandHandlerTests
{
    private readonly IOrderEngine _orderEngine;
    private readonly IOrderRepository _orderRepository;
    private readonly IOrderCacheService _cacheService;
    private readonly IKafkaProducer _kafkaProducer;
    private readonly ILogger<CreateOrderCommandHandler> _logger;
    private readonly CreateOrderCommandHandler _sut;

    public CreateOrderCommandHandlerTests()
    {
        _orderEngine = A.Fake<IOrderEngine>();
        _orderRepository = A.Fake<IOrderRepository>();
        _cacheService = A.Fake<IOrderCacheService>();
        _kafkaProducer = A.Fake<IKafkaProducer>();
        _logger = A.Fake<ILogger<CreateOrderCommandHandler>>();

        _sut = new CreateOrderCommandHandler(
            _orderEngine,
            _orderRepository,
            _cacheService,
            _kafkaProducer,
            _logger);
    }

    [Fact]
    public async Task Handle_ValidCommand_CreatesOrderAndReturnsResponse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var items = new List<CreateOrderItemRequest>
        {
            new(Guid.NewGuid(), "Product A", 2, 10.00m),
            new(Guid.NewGuid(), "Product B", 1, 25.00m)
        };
        var command = new CreateOrderCommand(userId, items);

        var expectedOrder = CreateTestOrder(userId);
        A.CallTo(() => _orderEngine.CreateOrder(userId, A<List<(Guid, string, int, decimal)>>._))
            .Returns(expectedOrder);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedOrder.OrderId, result.OrderId);
        Assert.Equal(userId, result.UserId);
    }

    [Fact]
    public async Task Handle_ValidCommand_SavesOrderToRepository()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateOrderCommand(userId, new List<CreateOrderItemRequest>
        {
            new(Guid.NewGuid(), "Test Product", 1, 10.00m)
        });

        var expectedOrder = CreateTestOrder(userId);
        A.CallTo(() => _orderEngine.CreateOrder(userId, A<List<(Guid, string, int, decimal)>>._))
            .Returns(expectedOrder);

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        A.CallTo(() => _orderRepository.AddAsync(expectedOrder, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Handle_ValidCommand_CachesOrder()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateOrderCommand(userId, new List<CreateOrderItemRequest>
        {
            new(Guid.NewGuid(), "Test Product", 1, 10.00m)
        });

        var expectedOrder = CreateTestOrder(userId);
        A.CallTo(() => _orderEngine.CreateOrder(userId, A<List<(Guid, string, int, decimal)>>._))
            .Returns(expectedOrder);

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        A.CallTo(() => _cacheService.SetOrderAsync(expectedOrder))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Handle_ValidCommand_PublishesOrderCreatedEvent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateOrderCommand(userId, new List<CreateOrderItemRequest>
        {
            new(Guid.NewGuid(), "Test Product", 1, 10.00m)
        });

        var expectedOrder = CreateTestOrder(userId);
        A.CallTo(() => _orderEngine.CreateOrder(userId, A<List<(Guid, string, int, decimal)>>._))
            .Returns(expectedOrder);

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert - verify Kafka was called with expected topic and key
        A.CallTo(_kafkaProducer)
            .Where(call => call.Method.Name == "PublishAsync"
                && call.Arguments[0]!.Equals(KafkaTopics.OrderCreated)
                && call.Arguments[1]!.Equals(expectedOrder.OrderId.ToString()))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Handle_EngineThrowsException_PropagatesException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateOrderCommand(userId, new List<CreateOrderItemRequest>
        {
            new(Guid.NewGuid(), "Test Product", 1, 10.00m)
        });

        A.CallTo(() => _orderEngine.CreateOrder(userId, A<List<(Guid, string, int, decimal)>>._))
            .Throws(new InvalidOperationException("Engine error"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_RepositoryThrowsException_DoesNotPublishEvent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateOrderCommand(userId, new List<CreateOrderItemRequest>
        {
            new(Guid.NewGuid(), "Test Product", 1, 10.00m)
        });

        var expectedOrder = CreateTestOrder(userId);
        A.CallTo(() => _orderEngine.CreateOrder(userId, A<List<(Guid, string, int, decimal)>>._))
            .Returns(expectedOrder);
        A.CallTo(() => _orderRepository.AddAsync(A<Order>._, A<CancellationToken>._))
            .Throws(new Exception("Database error"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() =>
            _sut.Handle(command, CancellationToken.None));

        A.CallTo(_kafkaProducer)
            .Where(call => call.Method.Name == "PublishAsync")
            .MustNotHaveHappened();
    }

    private static Order CreateTestOrder(Guid userId)
    {
        var items = new List<OrderItem>
        {
            OrderItem.Create(Guid.NewGuid(), "Test Product", 1, 10.00m)
        };
        return Order.Create(userId, items);
    }
}
