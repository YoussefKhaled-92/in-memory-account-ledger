namespace Ledger.Core;

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

/// <summary>Opening balances participate in every value-dated balance.</summary>
public sealed record Account(string Id, Currency Currency, decimal OpeningBalance);

public enum EventKind { Credit, Debit, Authorization, Settlement, Reversal, Release }
public enum EntryKind { Credit, Debit, Settlement, Reversal, OverdraftFee, Interest }
public enum AuthorizationStatus { Active, Settled, Released, Rejected }

/// <summary>BookedDay is supplied metadata; arrival order is the authoritative processing order.</summary>
public sealed record FinancialEvent(string Id, int BookedDay, int ValueDay,
    EventKind Kind, string AccountId, decimal Amount = 0m, string? Reference = null,
    int Instalments = 1);

/// <summary>Immutable posting. Corrections are new entries referencing the original source event.</summary>
public sealed record LedgerEntry(long Sequence, string SourceId, string AccountId,
    int RecordedDay, int ValueDay, decimal Amount, EntryKind Kind, string? Reference = null);

/// <summary>Each lifecycle transition is appended; no historical hold record is rewritten.</summary>
public sealed record AuthorizationChange(long Sequence, string SourceId, string AccountId,
    string AuthorizationId, int RecordedDay, int BookedDay, int ValueDay,
    decimal HoldAmount, AuthorizationStatus Status);

public sealed record EventResult(long Sequence, FinancialEvent Input, int RecordedDay,
    bool Accepted, string? Error);
public sealed record InterestAccrual(string AccountId, int Day, decimal Basis, decimal Amount);

/// <summary>A retained assessment is a charge, not a recomputed prediction of today's entitlement.</summary>
public sealed record FeeAssessment(string AccountId, int Day, int RecordedDay, decimal Amount);

public sealed class LedgerRuleException(string message) : Exception(message);
