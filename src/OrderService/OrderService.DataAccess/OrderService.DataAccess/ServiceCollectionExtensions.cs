using Microsoft.Extensions.DependencyInjection;
using OrderService.DataAccess.Caching;
using OrderService.DataAccess.Interfaces;
using OrderService.DataAccess.Repositories;
using StackExchange.Redis;

namespace OrderService.DataAccess;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDataAccess(this IServiceCollection services, string connectionString, string redisConnectionString)
    {
        services.AddSingleton<IDbConnectionFactory>(_ => new SqlConnectionFactory(connectionString));

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(redisConnectionString));

        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOrderCacheService, OrderCacheService>();

        return services;
    }
}
