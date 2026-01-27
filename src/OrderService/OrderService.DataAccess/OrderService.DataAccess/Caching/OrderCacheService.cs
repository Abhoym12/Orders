using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Order = OrderService.Engine.Models.Order;

namespace OrderService.DataAccess.Caching;

public interface IOrderCacheService
{
    Task<Order?> GetOrderAsync(Guid orderId);
    Task SetOrderAsync(Order order, TimeSpan? expiry = null);
    Task RemoveOrderAsync(Guid orderId);
}

public class OrderCacheService : IOrderCacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<OrderCacheService> _logger;
    private readonly TimeSpan _defaultExpiry = TimeSpan.FromMinutes(5);

    public OrderCacheService(IConnectionMultiplexer redis, ILogger<OrderCacheService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<Order?> GetOrderAsync(Guid orderId)
    {
        try
        {
            var db = _redis.GetDatabase();
            var key = GetKey(orderId);
            var value = await db.StringGetAsync(key);

            if (value.IsNullOrEmpty)
            {
                _logger.LogInformation("Cache miss for order {OrderId}", orderId);
                return null;
            }

            _logger.LogInformation("Cache hit for order {OrderId}", orderId);
            return JsonSerializer.Deserialize<Order>(value!);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get order {OrderId} from cache", orderId);
            return null;
        }
    }

    public async Task SetOrderAsync(Order order, TimeSpan? expiry = null)
    {
        try
        {
            var db = _redis.GetDatabase();
            var key = GetKey(order.OrderId);
            var value = JsonSerializer.Serialize(order);

            await db.StringSetAsync(key, value, expiry ?? _defaultExpiry);
            _logger.LogInformation("Cached order {OrderId} with TTL {Expiry}", order.OrderId, expiry ?? _defaultExpiry);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache order {OrderId}", order.OrderId);
        }
    }

    public async Task RemoveOrderAsync(Guid orderId)
    {
        try
        {
            var db = _redis.GetDatabase();
            var key = GetKey(orderId);
            await db.KeyDeleteAsync(key);
            _logger.LogInformation("Removed order {OrderId} from cache", orderId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove order {OrderId} from cache", orderId);
        }
    }

    private static string GetKey(Guid orderId) => $"order:{orderId}";
}
