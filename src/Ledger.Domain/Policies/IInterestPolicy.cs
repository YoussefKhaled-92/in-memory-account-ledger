namespace Ledger.Domain;

/// <summary>A daily strategy allows a future tiered or effective-dated interest product.</summary>
public interface IInterestPolicy
{
    decimal Calculate(Account account, int day, decimal closingBalance);
}
