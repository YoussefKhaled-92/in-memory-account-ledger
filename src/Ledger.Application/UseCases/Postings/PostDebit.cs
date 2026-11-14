using Ledger.Domain;

namespace Ledger.Application;

public sealed class PostDebit : IEventHandler
{
    public EventKind Kind => EventKind.Debit;

    public void Handle(FinancialEvent input, OperationContext context)
    {
        context.Post(input, -PostingRules.PositiveAmount(input, context.Account), EntryKind.Debit);
    }
}
