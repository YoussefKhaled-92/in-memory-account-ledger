using System.Collections.Generic;
using System.Linq;
using Ledger.Domain;

namespace Ledger.Application;

/// <summary>Prepare every account's daily rounded accruals before committing any capitalization.</summary>
public sealed class CapitalizeInterest(ILedgerJournal journal, LedgerQueries queries, IInterestPolicy policy)
{
    public void Execute(int windowDays, int recordedDay)
    {
        var preparedAccruals = new List<InterestAccrual>();
        var preparedCredits = new List<(string AccountId, decimal Amount)>();

        foreach (Account account in journal.Accounts)
        {
            var accountAccruals = new List<InterestAccrual>();

            for (int day = 1; day <= windowDays; day++)
            {
                decimal basis = queries.Balance(account.Id, day, includeInterest: false);
                decimal amount = policy.Calculate(account, day, basis);

                if (amount < 0m || account.Currency.Round(amount) != amount)
                {
                    throw new LedgerRuleException("Interest policy must return nonnegative currency units.");
                }

                accountAccruals.Add(new InterestAccrual(account.Id, day, basis, amount));
            }

            decimal total = accountAccruals.Sum(a => a.Amount);

            preparedAccruals.AddRange(accountAccruals);

            if (total > 0m)
            {
                preparedCredits.Add((account.Id, total));
            }
        }

        journal.AppendAccruals(preparedAccruals);

        foreach (var credit in preparedCredits)
        {
            journal.AppendPosting($"INTEREST:{credit.AccountId}", credit.AccountId, recordedDay, windowDays,
                credit.Amount, EntryKind.Interest);
        }
    }
}
