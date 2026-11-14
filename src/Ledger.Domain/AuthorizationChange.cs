namespace Ledger.Domain;

/// <summary>Each lifecycle transition is appended; no historical hold record is rewritten.</summary>
public sealed record AuthorizationChange(long Sequence, string SourceId, string AccountId,
    string AuthorizationId, int RecordedDay, int BookedDay, int ValueDay,
    decimal HoldAmount, AuthorizationStatus Status);
