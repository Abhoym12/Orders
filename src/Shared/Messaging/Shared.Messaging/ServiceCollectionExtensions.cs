using Microsoft.Extensions.DependencyInjection;

namespace Shared.Messaging;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKafkaMessaging(this IServiceCollection services, Action<KafkaSettings> configure)
    {
        services.Configure(configure);
        services.AddSingleton<IKafkaProducer, KafkaProducer>();
        services.AddTransient<IKafkaConsumer, KafkaConsumer>();
        return services;
    }
}
