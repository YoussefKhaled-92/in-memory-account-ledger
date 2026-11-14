using System;
using System.Collections.Generic;
using System.Linq;
using Ledger.Domain;

namespace Ledger.Application;

/// <summary>Synchronous, scan-based projections. The journal remains the source of truth.</summary>
public sealed class LedgerQueries(ILedgerJournal journal, int windowDays)
{
    public Account GetAccount(string id) => journal.Accounts.FirstOrDefault(a => a.Id == id)
        ?? throw new LedgerRuleException("Unknown account.");

    public void ValidateDay(int day)
    {
        if (day < 1 || day > windowDays)
        {
            throw new LedgerRuleException("Day is outside the assessment window.");
        }
    }

    public decimal Balance(string accountId, int day, bool includeInterest = true)
    {
        ValidateDay(day);

        Account account = GetAccount(accountId);

        return account.OpeningBalance + journal.Entries.Where(e => e.AccountId == accountId
            && e.ValueDay <= day && (includeInterest || e.Kind != EntryKind.Interest)).Sum(e => e.Amount);
    }

    public IReadOnlyList<AuthorizationChange> CurrentAuthorizations(string accountId)
    {
        GetAccount(accountId);

        return Array.AsReadOnly(journal.AuthorizationHistory.Where(c => c.AccountId == accountId)
            .GroupBy(c => c.AuthorizationId).Select(g => g.Last()).ToArray());
    }

    public decimal Available(string accountId, int recordedDay) => Balance(accountId, recordedDay)
        - CurrentAuthorizations(accountId).Where(c => c.Status == AuthorizationStatus.Active).Sum(c => c.HoldAmount);

    public Authorization RequireActive(FinancialEvent input)
    {
        PostingRules.RequireReference(input);

        var latest = CurrentAuthorizations(input.AccountId).FirstOrDefault(c => c.AuthorizationId == input.Reference)
            ?? throw new LedgerRuleException("Unknown authorization.");
        var authorization = new Authorization(latest);

        authorization.RequireActive();

        return authorization;
    }
}
