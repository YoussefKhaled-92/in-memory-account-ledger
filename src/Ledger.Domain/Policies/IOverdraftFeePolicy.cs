namespace Ledger.Domain;

/// <summary>Replace this policy to vary charges by product or effective business date.</summary>
public interface IOverdraftFeePolicy
{
    decimal GetFee(Account account, int assessmentDay);
}
