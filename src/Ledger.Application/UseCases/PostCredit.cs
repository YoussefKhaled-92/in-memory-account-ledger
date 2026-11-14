namespace Ledger.Application;

public sealed class PostCredit : IEventHandler
{
    public EventKind Kind => EventKind.Credit;

    public void Handle(FinancialEvent input, OperationContext context)
    {
        foreach (decimal part in InstalmentAllocator.Split(input.Amount, input.Instalments, context.Account.Currency))
            context.Post(input, part, EntryKind.Credit);
    }
}
