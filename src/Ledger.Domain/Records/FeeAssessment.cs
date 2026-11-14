namespace Ledger.Domain;

/// <summary>A retained assessment is a charge, not a recomputed prediction of today's entitlement.</summary>
public sealed record FeeAssessment(string AccountId, int Day, int RecordedDay, decimal Amount);
