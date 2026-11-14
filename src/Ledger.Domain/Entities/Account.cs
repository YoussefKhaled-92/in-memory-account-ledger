using System;

namespace Ledger.Domain;

/// <summary>Opening balances participate in every value-dated balance.</summary>
public sealed record Account(string Id, Currency Currency, decimal OpeningBalance)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Currency.Code))
        {
            throw new ArgumentException("Account and currency identifiers are required.");
        }

        if (Currency.Round(OpeningBalance) != OpeningBalance)
        {
            throw new ArgumentException("Opening balance exceeds currency precision.");
        }
    }
}
