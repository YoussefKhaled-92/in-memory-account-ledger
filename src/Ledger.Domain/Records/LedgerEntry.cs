namespace Ledger.Domain;

/// <summary>Immutable posting. Corrections are new entries referencing the original source event.</summary>
public sealed record LedgerEntry(long Sequence, string SourceId, string AccountId,
    int RecordedDay, int ValueDay, decimal Amount, EntryKind Kind, string? Reference = null);
