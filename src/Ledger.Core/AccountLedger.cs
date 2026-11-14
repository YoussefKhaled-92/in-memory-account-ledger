using Ledger.Infrastructure.InMemory;

namespace Ledger.Core;

/// <summary>Compatibility facade and default composition. Workflows live in Application.</summary>
public sealed class AccountLedger
{
    private readonly ILedgerJournal journal;
    private readonly LedgerSession session;
    private readonly LedgerQueries queries;
    private readonly ProcessEvent process;
    private readonly FinalizeLedger finalize;

    public AccountLedger(IEnumerable<Account> accounts, IOverdraftFeePolicy feePolicy,
        IInterestPolicy interestPolicy, int windowDays, IEnumerable<IEventHandler>? additionalHandlers = null)
    {
        session = new LedgerSession(windowDays);
        journal = new InMemoryLedgerJournal(accounts);
        queries = new LedgerQueries(journal, windowDays);
        IEventHandler[] standard = [new PostCredit(), new PostDebit(), new AuthorizeFunds(),
            new SettleAuthorization(), new ReversePosting(), new ReleaseAuthorization()];
        var registry = new EventHandlerRegistry(standard.Concat(additionalHandlers ?? []));
        var fees = new AssessOverdraftFees(journal, queries, feePolicy);
        process = new ProcessEvent(journal, queries, session, registry, fees);
        finalize = new FinalizeLedger(session, fees, new CapitalizeInterest(journal, queries, interestPolicy));
    }

    public int WindowDays => session.WindowDays;
    public int RecordedDay => session.RecordedDay;
    public IReadOnlyList<Account> Accounts => journal.Accounts;
    public IReadOnlyList<LedgerEntry> Entries => journal.Entries;
    public IReadOnlyList<AuthorizationChange> AuthorizationHistory => journal.AuthorizationHistory;
    public IReadOnlyList<EventResult> Results => journal.Results;
    public IReadOnlyList<FeeAssessment> Fees => journal.Fees;
    public IReadOnlyList<InterestAccrual> Accruals => journal.Accruals;
    public decimal Balance(string accountId, int day, bool includeInterest = true) => queries.Balance(accountId, day, includeInterest);
    public IReadOnlyList<AuthorizationChange> CurrentAuthorizations(string accountId) => queries.CurrentAuthorizations(accountId);
    public decimal Available(string accountId) => queries.Available(accountId, session.RecordedDay);
    public EventResult Process(FinancialEvent input) => process.Execute(input);
    public void Finish() => finalize.Execute();
}
