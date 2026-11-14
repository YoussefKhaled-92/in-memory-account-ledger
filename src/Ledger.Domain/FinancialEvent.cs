namespace Ledger.Domain;

/// <summary>BookedDay is supplied metadata; arrival order is the authoritative processing order.</summary>
public sealed record FinancialEvent(string Id, int BookedDay, int ValueDay,
    EventKind Kind, string AccountId, decimal Amount = 0m, string? Reference = null,
    int Instalments = 1);
