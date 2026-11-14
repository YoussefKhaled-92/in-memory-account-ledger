using System.Collections.Generic;
using Ledger.Domain;

namespace Ledger.Application;

/// <summary>One single-writer journal boundary; no update/delete or mutable collections.</summary>
public interface ILedgerJournal
{
    IReadOnlyList<Account> Accounts
    {
        get;
    }

    IReadOnlyList<LedgerEntry> Entries
    {
        get;
    }

    IReadOnlyList<AuthorizationChange> AuthorizationHistory
    {
        get;
    }

    IReadOnlyList<EventResult> Results
    {
        get;
    }

    IReadOnlyList<FeeAssessment> Fees
    {
        get;
    }

    IReadOnlyList<InterestAccrual> Accruals
    {
        get;
    }

    void AppendPosting(string source, string accountId, int recordedDay, int valueDay,
        decimal amount, EntryKind kind, string? reference = null);
    void AppendAuthorization(FinancialEvent input, int recordedDay, decimal amount, AuthorizationStatus status);

    EventResult AppendResult(FinancialEvent input, int recordedDay, bool accepted, string? error);

    void AppendFee(FeeAssessment fee);
    void AppendAccruals(IReadOnlyList<InterestAccrual> accruals);
}
