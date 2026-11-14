namespace Ledger.Application;

/// <summary>Chronological cascading assessment; previous charges are retained, at most once per account/day.</summary>
public sealed class AssessOverdraftFees(ILedgerJournal journal, LedgerQueries queries, IOverdraftFeePolicy policy)
{
    public void Execute(int lastDay, int recordedDay)
    {
        for (int day = 1; day <= lastDay; day++)
        foreach (Account account in journal.Accounts)
        {
            if (journal.Fees.Any(f => f.AccountId == account.Id && f.Day == day)) continue;
            if (queries.Balance(account.Id, day, includeInterest: false) >= 0m) continue;
            decimal amount = policy.GetFee(account, day);
            if (amount <= 0m || account.Currency.Round(amount) != amount)
                throw new LedgerRuleException("Fee policy must return positive currency units.");
            journal.AppendPosting($"FEE:{account.Id}:{day}", account.Id, recordedDay, day, -amount, EntryKind.OverdraftFee);
            journal.AppendFee(new FeeAssessment(account.Id, day, recordedDay, amount));
        }
    }
}
