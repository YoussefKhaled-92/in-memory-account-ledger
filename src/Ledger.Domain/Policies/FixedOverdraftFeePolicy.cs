namespace Ledger.Domain;

/// <summary>No currency conversion is invented. Missing policies fail explicitly on a negative close.</summary>
public sealed class FixedOverdraftFeePolicy : IOverdraftFeePolicy
{
    private readonly Dictionary<string, decimal> fees;

    public FixedOverdraftFeePolicy(IReadOnlyDictionary<string, decimal> fees)
    {
        this.fees = new Dictionary<string, decimal>(fees);
        if (this.fees.Values.Any(value => value <= 0m))
            throw new ArgumentException("Fees must be positive.", nameof(fees));
    }

    public decimal GetFee(Account account, int assessmentDay)
    {
        if (!fees.TryGetValue(account.Currency.Code, out decimal fee))
            throw new LedgerRuleException($"No overdraft fee policy for {account.Currency.Code}.");
        if (account.Currency.Round(fee) != fee)
            throw new LedgerRuleException("Configured fee exceeds currency precision.");
        return fee;
    }
}
