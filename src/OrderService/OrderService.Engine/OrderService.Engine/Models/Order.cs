using OrderService.Engine.Enums;
using OrderService.Engine.Exceptions;

namespace OrderService.Engine.Models;

public class Order
{
    public Guid OrderId { get; internal set; }
    public Guid UserId { get; internal set; }
    public OrderStatus Status { get; internal set; }
    public DateTime CreatedAt { get; internal set; }
    public DateTime? UpdatedAt { get; internal set; }
    public decimal TotalAmount { get; internal set; }
    public decimal AmountPaid { get; internal set; }

    private List<OrderItem> _items = new();
    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    public Order() { } // For Dapper

    public static Order Create(Guid userId, List<OrderItem> items)
    {
        if (items == null || items.Count == 0)
            throw new OrderDomainException("Order must have at least one item.");

        var order = new Order
        {
            OrderId = Guid.NewGuid(),
            UserId = userId,
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            TotalAmount = items.Sum(i => i.Price * i.Quantity)
        };

        foreach (var item in items)
        {
            order._items.Add(item);
        }

        return order;
    }

    // Used by Dapper repository to hydrate items
    internal void SetItems(List<OrderItem> items)
    {
        _items = items ?? new List<OrderItem>();
    }

    public void Cancel(string reason)
    {
        if (Status != OrderStatus.Pending)
        {
            throw new OrderCancellationException(Status.ToString());
        }

        Status = OrderStatus.Cancelled;
        UpdatedAt = DateTime.UtcNow;
    }

    public void TransitionTo(OrderStatus newStatus)
    {
        ValidateStateTransition(Status, newStatus);
        Status = newStatus;
        UpdatedAt = DateTime.UtcNow;
    }

    private static void ValidateStateTransition(OrderStatus current, OrderStatus target)
    {
        var validTransitions = new Dictionary<OrderStatus, OrderStatus[]>
        {
            { OrderStatus.Pending, new[] { OrderStatus.Processing, OrderStatus.Cancelled } },
            { OrderStatus.Processing, new[] { OrderStatus.Shipped } },
            { OrderStatus.Shipped, new[] { OrderStatus.Delivered } },
            { OrderStatus.Delivered, Array.Empty<OrderStatus>() },
            { OrderStatus.Cancelled, Array.Empty<OrderStatus>() }
        };

        if (!validTransitions.TryGetValue(current, out var allowed) || !allowed.Contains(target))
        {
            throw new InvalidStateTransitionException(current.ToString(), target.ToString());
        }
    }
}
