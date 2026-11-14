namespace Ledger.Application;

/// <summary>Strategy registry: composition changes when an operation is added; dispatch does not.</summary>
public sealed class EventHandlerRegistry
{
    private readonly IReadOnlyDictionary<EventKind, IEventHandler> handlers;

    public EventHandlerRegistry(IEnumerable<IEventHandler> handlers)
    {
        var registrations = new Dictionary<EventKind, IEventHandler>();
        foreach (var handler in handlers)
        {
            if (string.IsNullOrWhiteSpace(handler.Kind.Name))
                throw new ArgumentException("Handler kind is required.", nameof(handlers));
            if (!registrations.TryAdd(handler.Kind, handler))
                throw new ArgumentException($"Duplicate handler for {handler.Kind}.", nameof(handlers));
        }
        this.handlers = registrations;
    }

    public void Dispatch(FinancialEvent input, OperationContext context)
    {
        if (!handlers.TryGetValue(input.Kind, out var handler))
            throw new LedgerRuleException("Unsupported event kind.");
        handler.Handle(input, context);
    }
}
