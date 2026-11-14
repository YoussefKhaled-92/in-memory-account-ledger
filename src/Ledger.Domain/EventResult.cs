namespace Ledger.Domain;

public sealed record EventResult(long Sequence, FinancialEvent Input, int RecordedDay,
    bool Accepted, string? Error);
