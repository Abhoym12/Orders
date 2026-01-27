using OrderService.Engine.Enums;
using OrderService.Engine.Exceptions;
using OrderService.Engine.Models;

namespace OrderService.Engine.Tests;

public class OrderCancellationTests
{
    [Fact]
    public void Cancel_WhenPending_SetsStatusToCancelled()
    {
        // Arrange
        var order = CreatePendingOrder();

        // Act
        order.Cancel("Customer changed mind");

        // Assert
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.NotNull(order.UpdatedAt);
    }

    [Fact]
    public void Cancel_WhenPending_SetsUpdatedAt()
    {
        // Arrange
        var order = CreatePendingOrder();
        var beforeCancel = DateTime.UtcNow;

        // Act
        order.Cancel("Test reason");
        var afterCancel = DateTime.UtcNow;

        // Assert
        Assert.NotNull(order.UpdatedAt);
        Assert.InRange(order.UpdatedAt.Value, beforeCancel, afterCancel);
    }

    [Fact]
    public void Cancel_WhenProcessing_ThrowsOrderCancellationException()
    {
        // Arrange
        var order = CreateOrderWithStatus(OrderStatus.Processing);

        // Act & Assert
        Assert.Throws<OrderCancellationException>(() => order.Cancel("Test reason"));
    }

    [Fact]
    public void Cancel_WhenShipped_ThrowsOrderCancellationException()
    {
        // Arrange
        var order = CreateOrderWithStatus(OrderStatus.Shipped);

        // Act & Assert
        Assert.Throws<OrderCancellationException>(() => order.Cancel("Test reason"));
    }

    [Fact]
    public void Cancel_WhenDelivered_ThrowsOrderCancellationException()
    {
        // Arrange
        var order = CreateOrderWithStatus(OrderStatus.Delivered);

        // Act & Assert
        Assert.Throws<OrderCancellationException>(() => order.Cancel("Test reason"));
    }

    [Fact]
    public void Cancel_WhenAlreadyCancelled_ThrowsOrderCancellationException()
    {
        // Arrange
        var order = CreateOrderWithStatus(OrderStatus.Cancelled);

        // Act & Assert
        Assert.Throws<OrderCancellationException>(() => order.Cancel("Test reason"));
    }

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
}
