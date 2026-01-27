using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using OrderService.Engine;
using OrderService.Engine.Interfaces;
using OrderService.Manager.Behaviors;

namespace OrderService.Manager;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddManager(this IServiceCollection services)
    {
        // Register MediatR
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(ServiceCollectionExtensions).Assembly);
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        });

        // Register FluentValidation validators
        services.AddValidatorsFromAssembly(typeof(ServiceCollectionExtensions).Assembly);

        // Register Engine services
        services.AddScoped<IOrderEngine, OrderEngine>();

        return services;
    }
}
