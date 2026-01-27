namespace Shared.Messaging;

public static class KafkaTopics
{
    public const string OrderCreated = "order-created";
    public const string OrderStatusChanged = "order-status-changed";
    public const string OrderCancelled = "order-cancelled";
}
