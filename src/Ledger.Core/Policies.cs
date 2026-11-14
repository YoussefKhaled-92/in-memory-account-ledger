namespace Ledger.Core;

/// <summary>Replace this policy to vary charges by product or effective business date.</summary>
public interface IOverdraftFeePolicy
{
    decimal GetFee(Account account, int assessmentDay);
}

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

/// <summary>A daily strategy allows a future tiered or effective-dated interest product.</summary>
public interface IInterestPolicy
{
    decimal Calculate(Account account, int day, decimal closingBalance);
}

public sealed class DailyInterestPolicy : IInterestPolicy
{
    private readonly decimal dailyRate;

    public DailyInterestPolicy(decimal dailyRate)
    {
        if (dailyRate < 0m) throw new ArgumentOutOfRangeException(nameof(dailyRate));
        this.dailyRate = dailyRate;
    }

    public decimal Calculate(Account account, int day, decimal closingBalance)
    {
        return closingBalance <= 0m ? 0m : account.Currency.Round(closingBalance * dailyRate);
    }
}

/// <summary>Allocate integer minor units; earlier instalments receive the indivisible remainder.</summary>
public static class InstalmentAllocator
{
    public static IReadOnlyList<decimal> Split(decimal amount, int count, Currency currency)
    {
        if (count <= 0) throw new LedgerRuleException("Instalment count must be positive.");
        if (amount <= 0m) throw new LedgerRuleException("Amount must be positive.");
        decimal rounded = currency.Round(amount);
        if (rounded == 0m) throw new LedgerRuleException("Amount rounds to zero.");
        decimal factor = 1m;
        for (int place = 0; place < currency.DecimalPlaces; place++) factor *= 10m;
        decimal units = rounded * factor;
        decimal baseUnits = decimal.Floor(units / count);
        decimal remainder = units - baseUnits * count;
        var parts = new decimal[count];
        for (int index = 0; index < count; index++)
            parts[index] = (baseUnits + (index < remainder ? 1m : 0m)) / factor;
        return Array.AsReadOnly(parts);
    }
}
