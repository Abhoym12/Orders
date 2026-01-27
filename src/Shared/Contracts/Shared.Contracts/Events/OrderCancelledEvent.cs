namespace Shared.Contracts.Events;

public record OrderCancelledEvent(
    Guid OrderId,
    Guid UserId,
    string Reason,
    DateTime CancelledAt);
