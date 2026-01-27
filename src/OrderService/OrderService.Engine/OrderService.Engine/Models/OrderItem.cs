namespace OrderService.Engine.Models;

public class OrderItem
{
    public Guid Id { get; internal set; }
    public Guid OrderId { get; internal set; }
    public Guid ProductId { get; internal set; }
    public string ProductName { get; internal set; } = string.Empty;
    public int Quantity { get; internal set; }
    public decimal Price { get; internal set; }

    public OrderItem() { } // For Dapper

    public static OrderItem Create(Guid productId, string productName, int quantity, decimal price)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero.", nameof(quantity));

        if (price < 0)
            throw new ArgumentException("Price cannot be negative.", nameof(price));

        return new OrderItem
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            ProductName = productName ?? string.Empty,
            Quantity = quantity,
            Price = price
        };
    }

    internal void SetOrderId(Guid orderId)
    {
        OrderId = orderId;
    }
}
