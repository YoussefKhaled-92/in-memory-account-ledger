namespace Ledger.Infrastructure.InMemory;

/// <summary>Authoritative immutable histories. Every getter returns a detached read-only snapshot.</summary>
public sealed class InMemoryLedgerJournal : ILedgerJournal
{
    private readonly Dictionary<string, Account> accounts;
    private readonly List<LedgerEntry> entries = new();
    private readonly List<AuthorizationChange> authorizations = new();
    private readonly List<EventResult> results = new();
    private readonly List<FeeAssessment> fees = new();
    private readonly List<InterestAccrual> accruals = new();
    private long sequence;

    public InMemoryLedgerJournal(IEnumerable<Account> accounts)
    {
        this.accounts = accounts.ToDictionary(a => a.Id);
        if (this.accounts.Count == 0) throw new ArgumentException("At least one account is required.");
        foreach (Account account in this.accounts.Values) account.Validate();
    }

    public IReadOnlyList<Account> Accounts => Array.AsReadOnly(accounts.Values.ToArray());
    public IReadOnlyList<LedgerEntry> Entries => Array.AsReadOnly(entries.ToArray());
    public IReadOnlyList<AuthorizationChange> AuthorizationHistory => Array.AsReadOnly(authorizations.ToArray());
    public IReadOnlyList<EventResult> Results => Array.AsReadOnly(results.ToArray());
    public IReadOnlyList<FeeAssessment> Fees => Array.AsReadOnly(fees.ToArray());
    public IReadOnlyList<InterestAccrual> Accruals => Array.AsReadOnly(accruals.ToArray());

    public void AppendPosting(string source, string accountId, int recordedDay, int valueDay,
        decimal amount, EntryKind kind, string? reference = null) =>
        entries.Add(new LedgerEntry(++sequence, source, accountId, recordedDay, valueDay, amount, kind, reference));

    public void AppendAuthorization(FinancialEvent input, int recordedDay, decimal amount, AuthorizationStatus status) =>
        authorizations.Add(new AuthorizationChange(++sequence, input.Id, input.AccountId, input.Reference!,
            recordedDay, input.BookedDay, input.ValueDay, amount, status));

    public EventResult AppendResult(FinancialEvent input, int recordedDay, bool accepted, string? error)
    {
        var result = new EventResult(++sequence, input, recordedDay, accepted, error);
        results.Add(result);
        return result;
    }

    public void AppendFee(FeeAssessment fee) => fees.Add(fee);
    public void AppendAccruals(IReadOnlyList<InterestAccrual> prepared) => accruals.AddRange(prepared);
}
