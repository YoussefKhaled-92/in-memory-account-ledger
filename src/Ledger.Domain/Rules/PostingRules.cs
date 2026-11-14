namespace Ledger.Domain;

/// <summary>Shared monetary and reference invariants, independent of application state.</summary>
public static class PostingRules
{
    public static decimal PositiveAmount(FinancialEvent input, Account account)
    {
        if (input.Amount <= 0m)
        {
            throw new LedgerRuleException("Amount must be positive.");
        }

        decimal amount = account.Currency.Round(input.Amount);

        if (amount <= 0m)
        {
            throw new LedgerRuleException("Amount rounds to zero.");
        }

        return amount;
    }

    public static void RequireReference(FinancialEvent input)
    {
        if (string.IsNullOrWhiteSpace(input.Reference))
        {
            throw new LedgerRuleException("Reference is required.");
        }
    }
}
