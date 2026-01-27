using Dapper;
using Microsoft.Extensions.Logging;
using OrderService.DataAccess.Interfaces;
using OrderService.Engine.Enums;
using OrderService.Engine.Models;

namespace OrderService.DataAccess.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<OrderRepository> _logger;

    public OrderRepository(IDbConnectionFactory connectionFactory, ILogger<OrderRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<Order?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching order {OrderId}", orderId);

        const string orderSql = @"
            SELECT OrderId, UserId, Status, TotalAmount, CreatedAt, UpdatedAt
            FROM Orders
            WHERE OrderId = @OrderId";

        const string itemsSql = @"
            SELECT OrderItemId AS Id, OrderId, ProductId, ProductName, Quantity, UnitPrice AS Price
            FROM OrderItems
            WHERE OrderId = @OrderId";

        using var connection = _connectionFactory.CreateConnection();

        var order = await connection.QuerySingleOrDefaultAsync<Order>(
            new CommandDefinition(orderSql, new { OrderId = orderId }, cancellationToken: cancellationToken));

        if (order == null)
            return null;

        var items = (await connection.QueryAsync<OrderItem>(
            new CommandDefinition(itemsSql, new { OrderId = orderId }, cancellationToken: cancellationToken))).ToList();

        order.SetItems(items);

        return order;
    }

    public async Task<List<Order>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching orders for user {UserId}", userId);

        const string sql = @"
            SELECT OrderId, UserId, Status, TotalAmount, CreatedAt, UpdatedAt
            FROM Orders
            WHERE UserId = @UserId
            ORDER BY CreatedAt DESC";

        using var connection = _connectionFactory.CreateConnection();

        var orders = (await connection.QueryAsync<Order>(
            new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken))).ToList();

        await LoadOrderItemsAsync(connection, orders, cancellationToken);

        return orders;
    }

    public async Task<List<Order>> GetByStatusAsync(OrderStatus status, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching orders with status {Status}", status);

        const string sql = @"
            SELECT OrderId, UserId, Status, TotalAmount, CreatedAt, UpdatedAt
            FROM Orders
            WHERE Status = @Status";

        using var connection = _connectionFactory.CreateConnection();

        var orders = (await connection.QueryAsync<Order>(
            new CommandDefinition(sql, new { Status = (int)status }, cancellationToken: cancellationToken))).ToList();

        await LoadOrderItemsAsync(connection, orders, cancellationToken);

        return orders;
    }

    public async Task<List<Order>> GetByStatusAsync(OrderStatus status, int batchSize, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching {BatchSize} orders with status {Status}", batchSize, status);

        const string sql = @"
            SELECT TOP (@BatchSize) OrderId, UserId, Status, TotalAmount, CreatedAt, UpdatedAt
            FROM Orders
            WHERE Status = @Status
            ORDER BY CreatedAt";

        using var connection = _connectionFactory.CreateConnection();

        var orders = (await connection.QueryAsync<Order>(
            new CommandDefinition(sql, new { Status = (int)status, BatchSize = batchSize }, cancellationToken: cancellationToken))).ToList();

        await LoadOrderItemsAsync(connection, orders, cancellationToken);

        return orders;
    }

    public async Task AddAsync(Order order, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Adding order {OrderId}", order.OrderId);

        const string orderSql = @"
            INSERT INTO Orders (OrderId, UserId, Status, TotalAmount, CreatedAt, UpdatedAt)
            VALUES (@OrderId, @UserId, @Status, @TotalAmount, @CreatedAt, @UpdatedAt)";

        const string itemSql = @"
            INSERT INTO OrderItems (OrderItemId, OrderId, ProductId, ProductName, Quantity, UnitPrice)
            VALUES (@OrderItemId, @OrderId, @ProductId, @ProductName, @Quantity, @UnitPrice)";

        using var connection = _connectionFactory.CreateConnection();
        connection.Open();

        using var transaction = connection.BeginTransaction();

        try
        {
            await connection.ExecuteAsync(
                new CommandDefinition(orderSql, new
                {
                    order.OrderId,
                    order.UserId,
                    Status = (int)order.Status,
                    order.TotalAmount,
                    order.CreatedAt,
                    order.UpdatedAt
                }, transaction: transaction, cancellationToken: cancellationToken));

            foreach (var item in order.Items)
            {
                item.SetOrderId(order.OrderId);

                await connection.ExecuteAsync(
                    new CommandDefinition(itemSql, new
                    {
                        OrderItemId = item.Id,
                        item.OrderId,
                        item.ProductId,
                        item.ProductName,
                        item.Quantity,
                        UnitPrice = item.Price
                    }, transaction: transaction, cancellationToken: cancellationToken));
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task UpdateAsync(Order order, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating order {OrderId}", order.OrderId);

        const string sql = @"
            UPDATE Orders
            SET Status = @Status, TotalAmount = @TotalAmount, UpdatedAt = @UpdatedAt
            WHERE OrderId = @OrderId";

        using var connection = _connectionFactory.CreateConnection();

        await connection.ExecuteAsync(
            new CommandDefinition(sql, new
            {
                order.OrderId,
                Status = (int)order.Status,
                order.TotalAmount,
                order.UpdatedAt
            }, cancellationToken: cancellationToken));
    }

    public async Task<List<Order>> GetAllAsync(Guid? userId = null, OrderStatus? status = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching orders with filters UserId={UserId}, Status={Status}", userId, status);

        var sql = @"
            SELECT OrderId, UserId, Status, TotalAmount, CreatedAt, UpdatedAt
            FROM Orders
            WHERE 1=1";

        var parameters = new DynamicParameters();

        if (userId.HasValue)
        {
            sql += " AND UserId = @UserId";
            parameters.Add("UserId", userId.Value);
        }

        if (status.HasValue)
        {
            sql += " AND Status = @Status";
            parameters.Add("Status", (int)status.Value);
        }

        sql += " ORDER BY CreatedAt DESC";

        using var connection = _connectionFactory.CreateConnection();

        var orders = (await connection.QueryAsync<Order>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken))).ToList();

        await LoadOrderItemsAsync(connection, orders, cancellationToken);

        return orders;
    }

    private static async Task LoadOrderItemsAsync(System.Data.IDbConnection connection, List<Order> orders, CancellationToken cancellationToken)
    {
        if (orders.Count == 0)
            return;

        var orderIds = orders.Select(o => o.OrderId).ToArray();

        const string itemsSql = @"
            SELECT OrderItemId AS Id, OrderId, ProductId, ProductName, Quantity, UnitPrice AS Price
            FROM OrderItems
            WHERE OrderId IN @OrderIds";

        var allItems = (await connection.QueryAsync<OrderItem>(
            new CommandDefinition(itemsSql, new { OrderIds = orderIds }, cancellationToken: cancellationToken))).ToList();

        var itemsByOrderId = allItems.GroupBy(i => i.OrderId).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var order in orders)
        {
            if (itemsByOrderId.TryGetValue(order.OrderId, out var items))
            {
                order.SetItems(items);
            }
            else
            {
                order.SetItems(new List<OrderItem>());
            }
        }
    }
}
