namespace Ledger.Domain;

/// <summary>Currency precision is product configuration, never a floating-point approximation.</summary>
public sealed record Currency(string Code, int DecimalPlaces)
{
    public decimal Round(decimal amount)
    {
        if (DecimalPlaces < 0 || DecimalPlaces > 28)
            throw new ArgumentOutOfRangeException(nameof(DecimalPlaces));
        return decimal.Round(amount, DecimalPlaces, MidpointRounding.AwayFromZero);
    }
}
