namespace Ledger.Domain;

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
