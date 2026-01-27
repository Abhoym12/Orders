using OrderService.Engine.Enums;
using OrderService.Engine.Exceptions;
using OrderService.Engine.Models;

namespace OrderService.Engine.Tests;

public class OrderStateTransitionTests
{
    #region Valid Transitions

    [Fact]
    public void TransitionTo_FromPendingToProcessing_Succeeds()
    {
        // Arrange
        var order = CreatePendingOrder();

        // Act
        order.TransitionTo(OrderStatus.Processing);

        // Assert
        Assert.Equal(OrderStatus.Processing, order.Status);
    }

    [Fact]
    public void TransitionTo_FromPendingToCancelled_Succeeds()
    {
        // Arrange
        var order = CreatePendingOrder();

        // Act
        order.TransitionTo(OrderStatus.Cancelled);

        // Assert
        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public void TransitionTo_FromProcessingToShipped_Succeeds()
    {
        // Arrange
        var order = CreateOrderWithStatus(OrderStatus.Processing);

        // Act
        order.TransitionTo(OrderStatus.Shipped);

        // Assert
        Assert.Equal(OrderStatus.Shipped, order.Status);
    }

    [Fact]
    public void TransitionTo_FromShippedToDelivered_Succeeds()
    {
        // Arrange
        var order = CreateOrderWithStatus(OrderStatus.Shipped);

        // Act
        order.TransitionTo(OrderStatus.Delivered);

        // Assert
        Assert.Equal(OrderStatus.Delivered, order.Status);
    }

    [Fact]
    public void TransitionTo_SetsUpdatedAt()
    {
        // Arrange
        var order = CreatePendingOrder();
        var beforeTransition = DateTime.UtcNow;

        // Act
        order.TransitionTo(OrderStatus.Processing);
        var afterTransition = DateTime.UtcNow;

        // Assert
        Assert.NotNull(order.UpdatedAt);
        Assert.InRange(order.UpdatedAt.Value, beforeTransition, afterTransition);
    }

    #endregion

    #region Invalid Transitions from Pending

    [Fact]
    public void TransitionTo_FromPendingToShipped_ThrowsInvalidStateTransitionException()
    {
        // Arrange
        var order = CreatePendingOrder();

        // Act & Assert
        Assert.Throws<InvalidStateTransitionException>(() => order.TransitionTo(OrderStatus.Shipped));
    }

    [Fact]
    public void TransitionTo_FromPendingToDelivered_ThrowsInvalidStateTransitionException()
    {
        // Arrange
        var order = CreatePendingOrder();

        // Act & Assert
        Assert.Throws<InvalidStateTransitionException>(() => order.TransitionTo(OrderStatus.Delivered));
    }

    #endregion

    #region Invalid Transitions from Processing

    [Fact]
    public void TransitionTo_FromProcessingToPending_ThrowsInvalidStateTransitionException()
    {
        // Arrange
        var order = CreateOrderWithStatus(OrderStatus.Processing);

        // Act & Assert
        Assert.Throws<InvalidStateTransitionException>(() => order.TransitionTo(OrderStatus.Pending));
    }

    [Fact]
    public void TransitionTo_FromProcessingToCancelled_ThrowsInvalidStateTransitionException()
    {
        // Arrange
        var order = CreateOrderWithStatus(OrderStatus.Processing);

        // Act & Assert
        Assert.Throws<InvalidStateTransitionException>(() => order.TransitionTo(OrderStatus.Cancelled));
    }

    [Fact]
    public void TransitionTo_FromProcessingToDelivered_ThrowsInvalidStateTransitionException()
    {
        // Arrange
        var order = CreateOrderWithStatus(OrderStatus.Processing);

        // Act & Assert
        Assert.Throws<InvalidStateTransitionException>(() => order.TransitionTo(OrderStatus.Delivered));
    }

    #endregion

    #region Invalid Transitions from Shipped

    [Fact]
    public void TransitionTo_FromShippedToPending_ThrowsInvalidStateTransitionException()
    {
        // Arrange
        var order = CreateOrderWithStatus(OrderStatus.Shipped);

        // Act & Assert
        Assert.Throws<InvalidStateTransitionException>(() => order.TransitionTo(OrderStatus.Pending));
    }

    [Fact]
    public void TransitionTo_FromShippedToProcessing_ThrowsInvalidStateTransitionException()
    {
        // Arrange
        var order = CreateOrderWithStatus(OrderStatus.Shipped);

        // Act & Assert
        Assert.Throws<InvalidStateTransitionException>(() => order.TransitionTo(OrderStatus.Processing));
    }

    [Fact]
    public void TransitionTo_FromShippedToCancelled_ThrowsInvalidStateTransitionException()
    {
        // Arrange
        var order = CreateOrderWithStatus(OrderStatus.Shipped);

        // Act & Assert
        Assert.Throws<InvalidStateTransitionException>(() => order.TransitionTo(OrderStatus.Cancelled));
    }

    #endregion

    #region Invalid Transitions from Delivered (Terminal State)

    [Theory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.Processing)]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Cancelled)]
    public void TransitionTo_FromDeliveredToAnyStatus_ThrowsInvalidStateTransitionException(OrderStatus targetStatus)
    {
        // Arrange
        var order = CreateOrderWithStatus(OrderStatus.Delivered);

        // Act & Assert
        Assert.Throws<InvalidStateTransitionException>(() => order.TransitionTo(targetStatus));
    }

    #endregion

    #region Invalid Transitions from Cancelled (Terminal State)

    [Theory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.Processing)]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Delivered)]
    public void TransitionTo_FromCancelledToAnyStatus_ThrowsInvalidStateTransitionException(OrderStatus targetStatus)
    {
        // Arrange
        var order = CreateOrderWithStatus(OrderStatus.Cancelled);

        // Act & Assert
        Assert.Throws<InvalidStateTransitionException>(() => order.TransitionTo(targetStatus));
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
