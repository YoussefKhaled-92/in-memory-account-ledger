namespace Ledger.Domain;

public sealed record InterestAccrual(string AccountId, int Day, decimal Basis, decimal Amount);
