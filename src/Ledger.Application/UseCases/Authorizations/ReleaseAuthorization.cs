using Ledger.Domain;

namespace Ledger.Application;

public sealed class ReleaseAuthorization : IEventHandler
{
    public EventKind Kind => EventKind.Release;

    public void Handle(FinancialEvent input, OperationContext context)
    {
        Authorization active = context.Queries.RequireActive(input);

        context.Transition(input, active.HoldAmount, AuthorizationStatus.Released);
    }
}
