using OrderService.Engine.Enums;
using OrderService.Engine.Exceptions;
using OrderService.Engine.Models;

namespace OrderService.Engine.Tests;

public class OrderCreationTests
{
    [Fact]
    public void Create_WithValidItems_ReturnsOrderWithPendingStatus()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var items = new List<OrderItem>
        {
            OrderItem.Create(Guid.NewGuid(), "Product A", 2, 10.00m),
            OrderItem.Create(Guid.NewGuid(), "Product B", 1, 15.00m)
        };

        // Act
        var order = Order.Create(userId, items);

        // Assert
        Assert.NotEqual(Guid.Empty, order.OrderId);
        Assert.Equal(userId, order.UserId);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(2, order.Items.Count);
    }

    [Fact]
    public void Create_WithEmptyItems_ThrowsOrderDomainException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var items = new List<OrderItem>();

        // Act & Assert
        var exception = Assert.Throws<OrderDomainException>(() => Order.Create(userId, items));
        Assert.Contains("at least one item", exception.Message);
    }

    [Fact]
    public void Create_WithNullItems_ThrowsOrderDomainException()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act & Assert
        Assert.Throws<OrderDomainException>(() => Order.Create(userId, null!));
    }

    [Fact]
    public void Create_CalculatesTotalCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var items = new List<OrderItem>
        {
            OrderItem.Create(Guid.NewGuid(), "Product A", 2, 10.00m),  // 20.00
            OrderItem.Create(Guid.NewGuid(), "Product B", 3, 5.00m)    // 15.00
        };

        // Act
        var order = Order.Create(userId, items);

        // Assert
        Assert.Equal(35.00m, order.TotalAmount);
    }

    [Fact]
    public void Create_SetsCreatedAtToCurrentTime()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var items = new List<OrderItem>
        {
            OrderItem.Create(Guid.NewGuid(), "Test Product", 1, 10.00m)
        };
        var beforeCreation = DateTime.UtcNow;

        // Act
        var order = Order.Create(userId, items);
        var afterCreation = DateTime.UtcNow;

        // Assert
        Assert.InRange(order.CreatedAt, beforeCreation, afterCreation);
        Assert.Null(order.UpdatedAt);
    }

    [Fact]
    public void Create_GeneratesUniqueOrderIds()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var items = new List<OrderItem>
        {
            OrderItem.Create(Guid.NewGuid(), "Test Product", 1, 10.00m)
        };

        // Act
        var order1 = Order.Create(userId, items);
        var order2 = Order.Create(userId, items);

        // Assert
        Assert.NotEqual(order1.OrderId, order2.OrderId);
    }
}
