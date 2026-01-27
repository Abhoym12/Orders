using OrderService.Engine.Enums;
using OrderService.Engine.Models;

namespace OrderService.DataAccess.Interfaces;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<List<Order>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<List<Order>> GetByStatusAsync(OrderStatus status, CancellationToken cancellationToken = default);
    Task<List<Order>> GetByStatusAsync(OrderStatus status, int batchSize, CancellationToken cancellationToken = default);
    Task AddAsync(Order order, CancellationToken cancellationToken = default);
    Task UpdateAsync(Order order, CancellationToken cancellationToken = default);
    Task<List<Order>> GetAllAsync(Guid? userId = null, OrderStatus? status = null, CancellationToken cancellationToken = default);
}
