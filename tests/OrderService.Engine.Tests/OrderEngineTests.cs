using FakeItEasy;
using Microsoft.Extensions.Logging;
using OrderService.Engine.Enums;
using OrderService.Engine.Models;

namespace OrderService.Engine.Tests;

public class OrderEngineTests
{
    private readonly ILogger<OrderEngine> _logger;
    private readonly OrderEngine _sut;

    public OrderEngineTests()
    {
        _logger = A.Fake<ILogger<OrderEngine>>();
        _sut = new OrderEngine(_logger);
    }

    #region CreateOrder Tests

    [Fact]
    public void CreateOrder_WithValidItems_ReturnsOrder()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var items = new List<(Guid productId, string productName, int quantity, decimal price)>
        {
            (Guid.NewGuid(), "Product A", 2, 10.00m),
            (Guid.NewGuid(), "Product B", 1, 25.00m)
        };

        // Act
        var order = _sut.CreateOrder(userId, items);

        // Assert
        Assert.NotNull(order);
        Assert.Equal(userId, order.UserId);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(2, order.Items.Count);
        Assert.Equal(45.00m, order.TotalAmount);
    }

    [Fact]
    public void CreateOrder_LogsInformation()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var items = new List<(Guid productId, string productName, int quantity, decimal price)>
        {
            (Guid.NewGuid(), "Test Product", 1, 10.00m)
        };

        // Act
        _sut.CreateOrder(userId, items);

        // Assert - Verify logging was called (using FakeItEasy)
        A.CallTo(_logger)
            .Where(call => call.Method.Name == "Log")
            .MustHaveHappened();
    }

    #endregion

    #region CancelOrder Tests

    [Fact]
    public void CancelOrder_WhenPending_CancelsSuccessfully()
    {
        // Arrange
        var order = CreatePendingOrder();
        var reason = "Customer changed mind";

        // Act
        _sut.CancelOrder(order, reason);

        // Assert
        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public void CancelOrder_LogsInformation()
    {
        // Arrange
        var order = CreatePendingOrder();

        // Act
        _sut.CancelOrder(order, "Test reason");

        // Assert
        A.CallTo(_logger)
            .Where(call => call.Method.Name == "Log")
            .MustHaveHappened();
    }

    #endregion

    #region TransitionOrderStatus Tests

    [Fact]
    public void TransitionOrderStatus_ValidTransition_TransitionsSuccessfully()
    {
        // Arrange
        var order = CreatePendingOrder();

        // Act
        _sut.TransitionOrderStatus(order, OrderStatus.Processing);

        // Assert
        Assert.Equal(OrderStatus.Processing, order.Status);
    }

    [Fact]
    public void TransitionOrderStatus_LogsInformation()
    {
        // Arrange
        var order = CreatePendingOrder();

        // Act
        _sut.TransitionOrderStatus(order, OrderStatus.Processing);

        // Assert
        A.CallTo(_logger)
            .Where(call => call.Method.Name == "Log")
            .MustHaveHappened();
    }

    #endregion

    #region CanCancel Tests

    [Fact]
    public void CanCancel_WhenPending_ReturnsTrue()
    {
        // Arrange
        var order = CreatePendingOrder();

        // Act
        var result = _sut.CanCancel(order);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData(OrderStatus.Processing)]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Delivered)]
    [InlineData(OrderStatus.Cancelled)]
    public void CanCancel_WhenNotPending_ReturnsFalse(OrderStatus status)
    {
        // Arrange
        var order = CreateOrderWithStatus(status);

        // Act
        var result = _sut.CanCancel(order);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region CanTransitionTo Tests

    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Processing, true)]
    [InlineData(OrderStatus.Pending, OrderStatus.Cancelled, true)]
    [InlineData(OrderStatus.Processing, OrderStatus.Shipped, true)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Delivered, true)]
    [InlineData(OrderStatus.Pending, OrderStatus.Shipped, false)]
    [InlineData(OrderStatus.Pending, OrderStatus.Delivered, false)]
    [InlineData(OrderStatus.Processing, OrderStatus.Pending, false)]
    [InlineData(OrderStatus.Processing, OrderStatus.Cancelled, false)]
    [InlineData(OrderStatus.Delivered, OrderStatus.Pending, false)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Pending, false)]
    public void CanTransitionTo_ReturnsExpectedResult(OrderStatus currentStatus, OrderStatus targetStatus, bool expected)
    {
        // Arrange
        var order = CreateOrderWithStatus(currentStatus);

        // Act
        var result = _sut.CanTransitionTo(order, targetStatus);

        // Assert
        Assert.Equal(expected, result);
    }

    #endregion

    #region Helper Methods

    private static Order CreatePendingOrder()
    {
        var items = new List<OrderItem>
        {
            OrderItem.Create(Guid.NewGuid(), "Test Product", 1, 10.00m)
        };
        return Order.Create(Guid.NewGuid(), items);
    }

    private static Order CreateOrderWithStatus(OrderStatus status)
    {
        var order = CreatePendingOrder();

        // Use reflection to set internal status for testing
        var statusProperty = typeof(Order).GetProperty(nameof(Order.Status));
        statusProperty!.SetValue(order, status);

        return order;
    }

    #endregion
}
