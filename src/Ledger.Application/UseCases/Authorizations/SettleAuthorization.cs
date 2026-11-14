using Ledger.Domain;

namespace Ledger.Application;

public sealed class SettleAuthorization : IEventHandler
{
    public EventKind Kind => EventKind.Settlement;

    public void Handle(FinancialEvent input, OperationContext context)
    {
        Authorization active = context.Queries.RequireActive(input);
        decimal amount = PostingRules.PositiveAmount(input, context.Account);

        active.ValidateSettlement(amount);

        // Prior approval survives backdating; a partial capture releases all residual hold.
        context.Post(input, -amount, EntryKind.Settlement, input.Reference);
        context.Transition(input, active.HoldAmount, AuthorizationStatus.Settled);
    }
}
