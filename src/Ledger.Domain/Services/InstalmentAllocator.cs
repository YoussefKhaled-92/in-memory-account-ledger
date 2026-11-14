using System;
using System.Collections.Generic;

namespace Ledger.Domain;

/// <summary>Allocate integer minor units; earlier instalments receive the indivisible remainder.</summary>
public static class InstalmentAllocator
{
    public static IReadOnlyList<decimal> Split(decimal amount, int count, Currency currency)
    {
        if (count <= 0)
        {
            throw new LedgerRuleException("Instalment count must be positive.");
        }

        if (amount <= 0m)
        {
            throw new LedgerRuleException("Amount must be positive.");
        }

        decimal rounded = currency.Round(amount);

        if (rounded == 0m)
        {
            throw new LedgerRuleException("Amount rounds to zero.");
        }

        decimal factor = 1m;

        for (int place = 0; place < currency.DecimalPlaces; place++)
        {
            factor *= 10m;
        }

        decimal units = rounded * factor;
        decimal baseUnits = decimal.Floor(units / count);
        decimal remainder = units - baseUnits * count;
        var parts = new decimal[count];

        for (int index = 0; index < count; index++)
        {
            parts[index] = (baseUnits + (index < remainder ? 1m : 0m)) / factor;
        }

        return Array.AsReadOnly(parts);
    }
}
