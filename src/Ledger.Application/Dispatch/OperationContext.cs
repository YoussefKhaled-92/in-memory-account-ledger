using Ledger.Domain;

namespace Ledger.Application;

/// <summary>Validated account and arrival day, with the synchronous journal/query boundary.</summary>
public sealed record OperationContext(Account Account, int RecordedDay, ILedgerJournal Journal, LedgerQueries Queries)
{
    public void Post(FinancialEvent input, decimal amount, EntryKind kind, string? reference = null) =>
        Journal.AppendPosting(input.Id, Account.Id, RecordedDay, input.ValueDay, amount, kind, reference);

    public void Transition(FinancialEvent input, decimal amount, AuthorizationStatus status) =>
        Journal.AppendAuthorization(input, RecordedDay, amount, status);
}
