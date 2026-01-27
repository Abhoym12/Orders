namespace Shared.Contracts.Events;

public record OrderStatusChangedEvent(
    Guid OrderId,
    string PreviousStatus,
    string NewStatus,
    DateTime ChangedAt);
