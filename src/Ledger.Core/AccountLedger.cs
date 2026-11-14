namespace Ledger.Core;

/// <summary>
/// Single-writer, in-memory ledger. Monetary records and lifecycle decisions are append-only.
/// Query state is derived; recorded arrival time never moves backwards for a late booked event.
/// </summary>
public sealed class AccountLedger
{
    private readonly Dictionary<string, Account> accounts;
    private readonly IOverdraftFeePolicy feePolicy;
    private readonly IInterestPolicy interestPolicy;
    private readonly List<LedgerEntry> entries = new();
    private readonly List<AuthorizationChange> authorizations = new();
    private readonly List<EventResult> results = new();
    private readonly List<FeeAssessment> fees = new();
    private readonly List<InterestAccrual> accruals = new();
    private long sequence;
    private int recordedDay = 1;
    private int closedThrough;
    private bool finished;

    public int WindowDays { get; }
    public int RecordedDay => recordedDay;
    public IReadOnlyList<Account> Accounts => Array.AsReadOnly(accounts.Values.ToArray());
    public IReadOnlyList<LedgerEntry> Entries => Array.AsReadOnly(entries.ToArray());
    public IReadOnlyList<AuthorizationChange> AuthorizationHistory => Array.AsReadOnly(authorizations.ToArray());
    public IReadOnlyList<EventResult> Results => Array.AsReadOnly(results.ToArray());
    public IReadOnlyList<FeeAssessment> Fees => Array.AsReadOnly(fees.ToArray());
    public IReadOnlyList<InterestAccrual> Accruals => Array.AsReadOnly(accruals.ToArray());

    public AccountLedger(IEnumerable<Account> accounts, IOverdraftFeePolicy feePolicy,
        IInterestPolicy interestPolicy, int windowDays)
    {
        if (windowDays <= 0) throw new ArgumentOutOfRangeException(nameof(windowDays));
        this.accounts = accounts.ToDictionary(account => account.Id);
        if (this.accounts.Count == 0) throw new ArgumentException("At least one account is required.");
        foreach (Account account in this.accounts.Values)
        {
            if (string.IsNullOrWhiteSpace(account.Id) || string.IsNullOrWhiteSpace(account.Currency.Code))
                throw new ArgumentException("Account and currency identifiers are required.");
            if (account.Currency.Round(account.OpeningBalance) != account.OpeningBalance)
                throw new ArgumentException("Opening balance exceeds currency precision.");
        }
        this.feePolicy = feePolicy;
        this.interestPolicy = interestPolicy;
        WindowDays = windowDays;
    }

    /// <summary>Includes value-dated entries known now, regardless of their booking date.</summary>
    public decimal Balance(string accountId, int day, bool includeInterest = true)
    {
        ValidateDay(day);
        Account account = GetAccount(accountId);
        return account.OpeningBalance + entries.Where(entry => entry.AccountId == accountId
            && entry.ValueDay <= day && (includeInterest || entry.Kind != EntryKind.Interest))
            .Sum(entry => entry.Amount);
    }

    public IReadOnlyList<AuthorizationChange> CurrentAuthorizations(string accountId)
    {
        GetAccount(accountId);
        return Array.AsReadOnly(authorizations.Where(change => change.AccountId == accountId)
            .GroupBy(change => change.AuthorizationId).Select(group => group.Last()).ToArray());
    }

    /// <summary>Current available funds; historical holds are not retroactively re-decided.</summary>
    public decimal Available(string accountId)
    {
        decimal holds = CurrentAuthorizations(accountId)
            .Where(change => change.Status == AuthorizationStatus.Active).Sum(change => change.HoldAmount);
        return Balance(accountId, recordedDay) - holds;
    }

    /// <summary>Identical retry returns its first result; ID reuse with different data is refused.</summary>
    public EventResult Process(FinancialEvent input)
    {
        EventResult? prior = results.FirstOrDefault(result => result.Input.Id == input.Id);
        if (prior != null)
        {
            if (prior.Input != input) throw new LedgerRuleException("Event ID reused with different data.");
            return prior;
        }
        if (finished) throw new LedgerRuleException("Window is finalized; new input is forbidden.");
        if (string.IsNullOrWhiteSpace(input.Id)) throw new LedgerRuleException("Event ID is required.");
        if (input.Id.StartsWith("FEE:", StringComparison.Ordinal) || input.Id.StartsWith("INTEREST:", StringComparison.Ordinal))
            throw new LedgerRuleException("Event ID uses a reserved system prefix.");
        ValidateDay(input.BookedDay);
        ValidateDay(input.ValueDay);
        // Close only days whose input boundary has passed. Late E10 must not rewind this clock.
        AdvanceTo(input.BookedDay);
        try
        {
            Apply(input, GetAccount(input.AccountId));
        }
        catch (LedgerRuleException exception)
        {
            return RecordResult(input, false, exception.Message);
        }
        EventResult result = RecordResult(input, true, null);
        // A backdated posting can change several already-closed days, in chronological order.
        AssessFeesThrough(closedThrough);
        return result;
    }

    /// <summary>End-of-input watermark: only now is every late input known and capitalization safe.</summary>
    public void Finish()
    {
        if (finished) return;
        recordedDay = WindowDays;
        AssessFeesThrough(WindowDays);
        closedThrough = WindowDays;
        var preparedAccruals = new List<InterestAccrual>();
        var preparedCredits = new List<(string AccountId, decimal Amount)>();
        foreach (Account account in accounts.Values)
        {
            var accountAccruals = new List<InterestAccrual>();
            for (int day = 1; day <= WindowDays; day++)
            {
                // Excluding capitalization prevents Day 6 from earning interest on itself.
                decimal basis = Balance(account.Id, day, includeInterest: false);
                decimal amount = interestPolicy.Calculate(account, day, basis);
                if (amount < 0m || account.Currency.Round(amount) != amount)
                    throw new LedgerRuleException("Interest policy must return nonnegative currency units.");
                accountAccruals.Add(new InterestAccrual(account.Id, day, basis, amount));
            }
            decimal total = accountAccruals.Sum(accrual => accrual.Amount);
            preparedAccruals.AddRange(accountAccruals);
            if (total > 0m) preparedCredits.Add((account.Id, total));
        }
        // Validate every account before committing any interest, so a bad extension cannot
        // capitalize one account and then leave another account half-finalized.
        accruals.AddRange(preparedAccruals);
        foreach (var credit in preparedCredits)
            Append($"INTEREST:{credit.AccountId}", credit.AccountId, WindowDays, credit.Amount, EntryKind.Interest);
        finished = true;
    }

    private void AdvanceTo(int day)
    {
        if (day <= recordedDay) return;
        recordedDay = day;
        AssessFeesThrough(day - 1);
        closedThrough = day - 1;
    }

    private void AssessFeesThrough(int lastDay)
    {
        for (int day = 1; day <= lastDay; day++)
        {
            foreach (Account account in accounts.Values)
            {
                if (fees.Any(fee => fee.AccountId == account.Id && fee.Day == day)) continue;
                if (Balance(account.Id, day, includeInterest: false) >= 0m) continue;
                decimal amount = feePolicy.GetFee(account, day);
                if (amount <= 0m || account.Currency.Round(amount) != amount)
                    throw new LedgerRuleException("Fee policy must return positive currency units.");
                // NON-NEGOTIABLE: at most one assessment per account/day, value-dated to that day.
                Append($"FEE:{account.Id}:{day}", account.Id, day, -amount, EntryKind.OverdraftFee);
                fees.Add(new FeeAssessment(account.Id, day, recordedDay, amount));
                // DESIGN DECISION: reversal does not silently refund assessed fees (AMBIGUITIES.md).
            }
        }
    }

    private void Apply(FinancialEvent input, Account account)
    {
        switch (input.Kind)
        {
            case EventKind.Credit:
                IReadOnlyList<decimal> parts = InstalmentAllocator.Split(input.Amount, input.Instalments, account.Currency);
                foreach (decimal part in parts)
                    Append(input.Id, account.Id, input.ValueDay, part, EntryKind.Credit);
                break;
            case EventKind.Debit:
                Append(input.Id, account.Id, input.ValueDay, -PositiveAmount(input, account), EntryKind.Debit);
                break;
            case EventKind.Authorization:
                Authorize(input, account);
                break;
            case EventKind.Settlement:
                Settle(input, account);
                break;
            case EventKind.Reversal:
                Reverse(input, account);
                break;
            case EventKind.Release:
                AuthorizationChange active = RequireActive(input);
                ChangeAuthorization(input, active.HoldAmount, AuthorizationStatus.Released);
                break;
            default:
                throw new LedgerRuleException("Unsupported event kind.");
        }
    }

    private void Authorize(FinancialEvent input, Account account)
    {
        RequireReference(input);
        if (CurrentAuthorizations(account.Id).Any(change => change.AuthorizationId == input.Reference))
            throw new LedgerRuleException("Authorization ID already used for this account.");
        decimal amount = PositiveAmount(input, account);
        // NON-NEGOTIABLE: no ledger posting for a hold; available funds must remain nonnegative.
        if (Available(account.Id) < amount)
        {
            ChangeAuthorization(input, amount, AuthorizationStatus.Rejected);
            throw new LedgerRuleException("Insufficient available balance for authorization.");
        }
        ChangeAuthorization(input, amount, AuthorizationStatus.Active);
    }

    private void Settle(FinancialEvent input, Account account)
    {
        AuthorizationChange active = RequireActive(input);
        decimal amount = PositiveAmount(input, account);
        if (amount > active.HoldAmount) throw new LedgerRuleException("Settlement exceeds active hold.");
        // Prior approval remains valid after backdating; do not rerun the affordability decision.
        Append(input.Id, account.Id, input.ValueDay, -amount, EntryKind.Settlement, input.Reference);
        ChangeAuthorization(input, active.HoldAmount, AuthorizationStatus.Settled);
        // A partial settlement is terminal and releases the unused part of the hold.
    }

    private void Reverse(FinancialEvent input, Account account)
    {
        RequireReference(input);
        LedgerEntry[] original = entries.Where(entry => entry.SourceId == input.Reference
            && entry.AccountId == account.Id).ToArray();
        if (original.Length == 0 || original.Any(entry => entry.Kind != EntryKind.Credit && entry.Kind != EntryKind.Debit))
            throw new LedgerRuleException("Only an existing credit/debit event can be reversed.");
        if (entries.Any(entry => entry.Kind == EntryKind.Reversal && entry.AccountId == account.Id
            && entry.Reference == input.Reference))
            throw new LedgerRuleException("Event has already been reversed.");
        if (original.Any(entry => entry.ValueDay != input.ValueDay))
            throw new LedgerRuleException("Reversal must use the original value date.");
        foreach (LedgerEntry entry in original)
            Append(input.Id, account.Id, input.ValueDay, -entry.Amount, EntryKind.Reversal, input.Reference);
    }

    private AuthorizationChange RequireActive(FinancialEvent input)
    {
        RequireReference(input);
        AuthorizationChange? active = CurrentAuthorizations(input.AccountId)
            .FirstOrDefault(change => change.AuthorizationId == input.Reference);
        if (active == null) throw new LedgerRuleException("Unknown authorization.");
        if (active.Status != AuthorizationStatus.Active) throw new LedgerRuleException("Authorization is not active.");
        return active;
    }

    private static decimal PositiveAmount(FinancialEvent input, Account account)
    {
        if (input.Amount <= 0m) throw new LedgerRuleException("Amount must be positive.");
        decimal amount = account.Currency.Round(input.Amount);
        if (amount <= 0m) throw new LedgerRuleException("Amount rounds to zero.");
        return amount;
    }

    private static void RequireReference(FinancialEvent input)
    {
        if (string.IsNullOrWhiteSpace(input.Reference)) throw new LedgerRuleException("Reference is required.");
    }

    private void ChangeAuthorization(FinancialEvent input, decimal amount, AuthorizationStatus status)
    {
        authorizations.Add(new AuthorizationChange(++sequence, input.Id, input.AccountId,
            input.Reference!, recordedDay, input.BookedDay, input.ValueDay, amount, status));
    }

    private void Append(string source, string accountId, int valueDay, decimal amount,
        EntryKind kind, string? reference = null)
    {
        entries.Add(new LedgerEntry(++sequence, source, accountId, recordedDay, valueDay, amount, kind, reference));
    }

    private EventResult RecordResult(FinancialEvent input, bool accepted, string? error)
    {
        var result = new EventResult(++sequence, input, recordedDay, accepted, error);
        results.Add(result);
        return result;
    }

    private Account GetAccount(string id)
    {
        if (!accounts.TryGetValue(id, out Account? account)) throw new LedgerRuleException("Unknown account.");
        return account;
    }

    private void ValidateDay(int day)
    {
        if (day < 1 || day > WindowDays) throw new LedgerRuleException("Day is outside the assessment window.");
    }
}
