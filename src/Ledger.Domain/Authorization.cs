namespace Ledger.Domain;

/// <summary>Current reservation derived from its immutable transition history.</summary>
public sealed class Authorization(AuthorizationChange latest)
{
    public decimal HoldAmount => latest.HoldAmount;

    public void RequireActive()
    {
        if (latest.Status != AuthorizationStatus.Active)
            throw new LedgerRuleException("Authorization is not active.");
    }

    public void ValidateSettlement(decimal amount)
    {
        RequireActive();
        if (amount > HoldAmount) throw new LedgerRuleException("Settlement exceeds active hold.");
    }

    public static bool CanReserve(decimal available, decimal amount) => available >= amount;
}
