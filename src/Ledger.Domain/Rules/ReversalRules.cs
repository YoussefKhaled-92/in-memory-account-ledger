using System.Collections.Generic;
using System.Linq;

namespace Ledger.Domain;

/// <summary>Principal compensation never edits its source or implicitly refunds fees.</summary>
public static class ReversalRules
{
    public static void Validate(IReadOnlyList<LedgerEntry> original, bool alreadyReversed, int valueDay)
    {
        if (original.Count == 0 || original.Any(e => e.Kind != EntryKind.Credit && e.Kind != EntryKind.Debit))
        {
            throw new LedgerRuleException("Only an existing credit/debit event can be reversed.");
        }

        if (alreadyReversed)
        {
            throw new LedgerRuleException("Event has already been reversed.");
        }

        if (original.Any(e => e.ValueDay != valueDay))
        {
            throw new LedgerRuleException("Reversal must use the original value date.");
        }
    }
}
