namespace OrderService.Engine.Exceptions;

public class OrderCancellationException : OrderDomainException
{
    public OrderCancellationException(string status)
        : base($"Cannot cancel order with status '{status}'. Only orders with 'Pending' status can be cancelled.")
    {
    }
}
