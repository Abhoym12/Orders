namespace OrderService.Engine.Exceptions;

public class InvalidStateTransitionException : OrderDomainException
{
    public InvalidStateTransitionException(string currentState, string targetState)
        : base($"Invalid state transition from '{currentState}' to '{targetState}'")
    {
    }
}
