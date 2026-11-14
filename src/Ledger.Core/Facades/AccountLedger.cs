using System;
using System.Collections.Generic;
using Ledger.Application;
using Ledger.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace Ledger.Core;

/// <summary>Compatibility facade and default composition. Workflows live in Application.</summary>
public sealed class AccountLedger : IDisposable
{
    private readonly ServiceProvider services;
    private bool disposed;
    private readonly ILedgerJournal journal;
    private readonly LedgerSession session;
    private readonly LedgerQueries queries;
    private readonly ProcessEvent process;
    private readonly FinalizeLedger finalize;

    public AccountLedger(
        IEnumerable<Account> accounts,
        IOverdraftFeePolicy feePolicy,
        IInterestPolicy interestPolicy,
        int windowDays,
        IEnumerable<IEventHandler>? additionalHandlers = null)
    {
        services = LedgerComposition.Build(accounts, feePolicy, interestPolicy, windowDays, additionalHandlers);

        try
        {
            session = services.GetRequiredService<LedgerSession>();
            journal = services.GetRequiredService<ILedgerJournal>();
            queries = services.GetRequiredService<LedgerQueries>();
            process = services.GetRequiredService<ProcessEvent>();
            finalize = services.GetRequiredService<FinalizeLedger>();
        }
        catch
        {
            services.Dispose();

            throw;
        }
    }

    public int WindowDays => session.WindowDays;

    public int RecordedDay => session.RecordedDay;

    public IReadOnlyList<Account> Accounts => journal.Accounts;

    public IReadOnlyList<LedgerEntry> Entries => journal.Entries;

    public IReadOnlyList<AuthorizationChange> AuthorizationHistory => journal.AuthorizationHistory;

    public IReadOnlyList<EventResult> Results => journal.Results;

    public IReadOnlyList<FeeAssessment> Fees => journal.Fees;

    public IReadOnlyList<InterestAccrual> Accruals => journal.Accruals;

    public decimal Balance(string accountId, int day, bool includeInterest = true) =>
        queries.Balance(accountId, day, includeInterest);

    public IReadOnlyList<AuthorizationChange> CurrentAuthorizations(string accountId) =>
        queries.CurrentAuthorizations(accountId);

    public decimal Available(string accountId) => queries.Available(accountId, session.RecordedDay);

    public EventResult Process(FinancialEvent input)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        return process.Execute(input);
    }

    public void Finish()
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        finalize.Execute();
    }

    /// <summary>Release the private container. Does not finalize or dispose caller-owned policies/handlers.</summary>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        services.Dispose();
    }
}
