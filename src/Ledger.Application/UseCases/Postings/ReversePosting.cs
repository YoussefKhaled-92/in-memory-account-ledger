using System.Linq;
using Ledger.Domain;

namespace Ledger.Application;

public sealed class ReversePosting : IEventHandler
{
    public EventKind Kind => EventKind.Reversal;

    public void Handle(FinancialEvent input, OperationContext context)
    {
        PostingRules.RequireReference(input);

        var entries = context.Journal.Entries;
        var original = entries.Where(e => e.SourceId == input.Reference && e.AccountId == context.Account.Id).ToArray();
        bool reversed = entries.Any(e => e.Kind == EntryKind.Reversal && e.AccountId == context.Account.Id
            && e.Reference == input.Reference);
        ReversalRules.Validate(original, reversed, input.ValueDay);

        foreach (var entry in original)
        {
            context.Post(input, -entry.Amount, EntryKind.Reversal, input.Reference);
        }
    }
}
