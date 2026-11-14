using Ledger.Core;
using System.Globalization;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
var suite = new List<(string Name, Action Body)>();
void Test(string name, Action body) => suite.Add((name, body));
void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new Exception($"Expected {expected}; actual {actual}.");
}
void Throws(Action body)
{
    try { body(); }
    catch (LedgerRuleException) { return; }
    throw new Exception("Expected LedgerRuleException.");
}
AccountLedger Through(int count)
{
    var ledger = AssessmentScenario.Create();
    foreach (var input in AssessmentScenario.Events.Take(count)) ledger.Process(input);
    return ledger;
}
FinancialEvent Credit(string id, decimal amount, int day = 1) => new(id, day, day, EventKind.Credit, "ACC-001", amount);
FinancialEvent Hold(string id, decimal amount, string reference = "H", int day = 1) => new(id, day, day, EventKind.Authorization, "ACC-001", amount, reference);

Test("Criterion 1: raw Day 2 principal is -370 at E8", () =>
{
    var ledger = Through(8);
    decimal principal = ledger.Entries.Where(entry => entry.AccountId == "ACC-001" && entry.ValueDay <= 2
        && entry.Kind != EntryKind.OverdraftFee).Sum(entry => entry.Amount);
    Equal(-370m, principal);
    Equal(-395m, ledger.Balance("ACC-001", 2));
});
Test("Criterion 2 refused: E7 affects Day 2 and Day 4 immediately, Day 5 on close", () =>
{
    var ledger = Through(7);
    Equal("2,4", string.Join(",", ledger.Fees.Select(fee => fee.Day)));
    ledger.Process(AssessmentScenario.Events[7]);
    ledger.Process(AssessmentScenario.Events[8]);
    Equal("2,4,5", string.Join(",", ledger.Fees.Select(fee => fee.Day)));
    Equal(75m, ledger.Fees.Sum(fee => fee.Amount));
});
Test("Day 3 stays positive after the Day 2 fee and is not charged", () =>
{
    var ledger = Through(8);
    Equal(5m, ledger.Balance("ACC-001", 3));
    Equal(false, ledger.Fees.Any(fee => fee.Day == 3));
});
Test("Criterion 3: Auth-A settles and releases unused 15", () =>
{
    var ledger = Through(5);
    Equal(true, ledger.Results.Last().Accepted);
    Equal(465m, ledger.Balance("ACC-001", 4));
    Equal(465m, ledger.Available("ACC-001"));
    Equal(AuthorizationStatus.Settled, ledger.CurrentAuthorizations("ACC-001").Single().Status);
});
Test("Criterion 4: unknown authorization cannot debit funds", () =>
{
    var ledger = Through(5);
    int before = ledger.Entries.Count;
    Equal(false, ledger.Process(AssessmentScenario.Events[5]).Accepted);
    Equal(before, ledger.Entries.Count);
    Equal(465m, ledger.Balance("ACC-001", 4));
});
Test("Criterion 5: approved hold changes available only", () =>
{
    var ledger = Through(3);
    Equal(250m, ledger.Balance("ACC-001", 2));
    Equal(50m, ledger.Available("ACC-001"));
});
Test("Auth-B is rejected at its original decision point and never retried after E9", () =>
{
    var ledger = AssessmentScenario.Replay();
    Equal(AuthorizationStatus.Rejected, ledger.CurrentAuthorizations("ACC-001").Single(a => a.AuthorizationId == "Auth-B").Status);
    Equal(false, ledger.Results.Single(r => r.Input.Id == "E8").Accepted);
    Equal(390.93m, ledger.Available("ACC-001"));
});
Test("Criterion 6 refused: reversing E7 retains 75 of assessed fees", () =>
{
    var ledger = Through(9);
    Equal(390m, ledger.Balance("ACC-001", 6));
    Equal(75m, ledger.Fees.Sum(f => f.Amount));
    Equal(1, ledger.Entries.Count(e => e.Kind == EntryKind.Reversal));
});
Test("Criterion 7 refused: BHD minor units conserved across unequal rounded parts", () =>
{
    var parts = InstalmentAllocator.Split(10m, 3, AssessmentScenario.Bhd);
    Equal("3.334,3.333,3.333", string.Join(",", parts.Select(p => p.ToString("F3"))));
    Equal(10m, parts.Sum());
});
Test("Criterion 8: capitalization is exactly the sum of daily rounded accruals", () =>
{
    var ledger = AssessmentScenario.Replay();
    foreach (Account account in ledger.Accounts)
        Equal(ledger.Accruals.Where(a => a.AccountId == account.Id).Sum(a => a.Amount),
            ledger.Entries.Single(e => e.AccountId == account.Id && e.Kind == EntryKind.Interest).Amount);
    Equal(0.93m, ledger.Entries.Single(e => e.AccountId == "ACC-001" && e.Kind == EntryKind.Interest).Amount);
    Equal(0.008m, ledger.Entries.Single(e => e.AccountId == "ACC-002" && e.Kind == EntryKind.Interest).Amount);
});
Test("Final AED daily balances agree with independent hand calculation", () =>
{
    var ledger = AssessmentScenario.Replay();
    decimal[] expected = {250m, 225m, 625m, 415m, 390m, 390.93m};
    for (int day = 1; day <= 6; day++) Equal(expected[day - 1], ledger.Balance("ACC-001", day));
});
Test("Final daily AED accruals are restated, positive only, excluding capitalization", () =>
{
    var ledger = AssessmentScenario.Replay();
    Equal("0.10,0.09,0.25,0.17,0.16,0.16", string.Join(",", ledger.Accruals
        .Where(a => a.AccountId == "ACC-001").Select(a => a.Amount.ToString("F2"))));
});
Test("E10 remains last, its booked day does not rewind recorded time", () =>
{
    var ledger = AssessmentScenario.Replay();
    Equal("E1,E2,E3,E4,E5,E6,E7,E8,E9,E10", string.Join(",", ledger.Results.Select(r => r.Input.Id)));
    Equal(5, ledger.Results.Last().Input.BookedDay);
    Equal(6, ledger.Results.Last().RecordedDay);
    Equal(10m, ledger.Balance("ACC-002", 5));
    Equal(10.008m, ledger.Balance("ACC-002", 6));
});
Test("Backdating preserves every previously appended entry and decision", () =>
{
    var ledger = Through(6);
    LedgerEntry[] before = ledger.Entries.ToArray();
    AuthorizationChange[] decisions = ledger.AuthorizationHistory.ToArray();
    foreach (var input in AssessmentScenario.Events.Skip(6)) ledger.Process(input);
    ledger.Finish();
    Equal(true, before.SequenceEqual(ledger.Entries.Take(before.Length)));
    Equal(true, decisions.SequenceEqual(ledger.AuthorizationHistory.Take(decisions.Length)));
});
Test("Public collection views cannot mutate ledger state", () =>
{
    var ledger = Through(1);
    Equal(true, ((IList<LedgerEntry>)ledger.Entries).IsReadOnly);
    var oldView = ledger.Entries;
    ledger.Process(AssessmentScenario.Events[1]);
    Equal(1, oldView.Count);
    Equal(2, ledger.Entries.Count);
});
Test("Identical input retry is idempotent", () =>
{
    var ledger = Through(1);
    var first = ledger.Results.Single();
    Equal(first, ledger.Process(AssessmentScenario.Events[0]));
    Equal(1, ledger.Entries.Count);
    Equal(1, ledger.Results.Count);
});
Test("Conflicting input ID is refused before financial mutation", () =>
{
    var ledger = Through(1);
    Throws(() => ledger.Process(Credit("E1", 1m)));
    Equal(1200m, ledger.Balance("ACC-001", 1));
});
Test("Repeated declined authorization returns original rejection without extra history", () =>
{
    var ledger = AssessmentScenario.Create();
    var input = Hold("H1", 1m);
    Equal(false, ledger.Process(input).Accepted);
    Equal(false, ledger.Process(input).Accepted);
    Equal(1, ledger.AuthorizationHistory.Count);
});
Test("Exact available boundary approves; one minor unit above rejects", () =>
{
    var ledger = AssessmentScenario.Create();
    ledger.Process(Credit("C", 100m));
    Equal(true, ledger.Process(Hold("H1", 100m)).Accepted);
    Equal(false, ledger.Process(Hold("H2", 0.01m, "H2")).Accepted);
    Equal(0m, ledger.Available("ACC-001"));
});
Test("Settlement above hold rejects without closing hold or debiting", () =>
{
    var ledger = Through(3);
    Equal(false, ledger.Process(new("S", 2, 2, EventKind.Settlement, "ACC-001", 201m, "Auth-A")).Accepted);
    Equal(250m, ledger.Balance("ACC-001", 2));
    Equal(AuthorizationStatus.Active, ledger.CurrentAuthorizations("ACC-001").Single().Status);
});
Test("Repeated settlement with new ID cannot debit twice", () =>
{
    var ledger = Through(5);
    Equal(false, ledger.Process(new("S2", 4, 4, EventKind.Settlement, "ACC-001", 185m, "Auth-A")).Accepted);
    Equal(465m, ledger.Balance("ACC-001", 4));
});
Test("Authorization identity is account scoped", () =>
{
    var ledger = Through(3);
    Equal(false, ledger.Process(new("S", 2, 2, EventKind.Settlement, "ACC-002", 1m, "Auth-A")).Accepted);
    Equal(0m, ledger.Balance("ACC-002", 2));
});
Test("Explicit release ends a hold and leaves ledger unchanged", () =>
{
    var ledger = Through(3);
    Equal(true, ledger.Process(new("R", 2, 2, EventKind.Release, "ACC-001", Reference: "Auth-A")).Accepted);
    Equal(250m, ledger.Available("ACC-001"));
    Equal(AuthorizationStatus.Released, ledger.CurrentAuthorizations("ACC-001").Single().Status);
    Equal(false, ledger.Process(new("S", 2, 2, EventKind.Settlement, "ACC-001", 1m, "Auth-A")).Accepted);
});
Test("Settlement affordability is not retrospectively re-decided", () =>
{
    var ledger = Through(3);
    ledger.Process(new("BACK", 3, 1, EventKind.Debit, "ACC-001", 500m));
    Equal(true, ledger.Process(new("S", 3, 3, EventKind.Settlement, "ACC-001", 185m, "Auth-A")).Accepted);
});
Test("Duplicate reversal is rejected with no duplicate compensating entry", () =>
{
    var ledger = Through(9);
    Equal(false, ledger.Process(new("R2", 6, 2, EventKind.Reversal, "ACC-001", Reference: "E7")).Accepted);
    Equal(1, ledger.Entries.Count(e => e.Kind == EntryKind.Reversal));
});
Test("Unknown reversal and reversal at different value date are rejected", () =>
{
    var ledger = Through(7);
    Equal(false, ledger.Process(new("RX", 5, 2, EventKind.Reversal, "ACC-001", Reference: "missing")).Accepted);
    Equal(false, ledger.Process(new("RY", 5, 3, EventKind.Reversal, "ACC-001", Reference: "E7")).Accepted);
});
Test("Reversing an instalment credit compensates every part exactly", () =>
{
    var ledger = Through(10);
    Equal(true, ledger.Process(new("R10", 6, 5, EventKind.Reversal, "ACC-002", Reference: "E10")).Accepted);
    Equal(0m, ledger.Balance("ACC-002", 5));
    Equal(3, ledger.Entries.Count(e => e.SourceId == "R10"));
});
Test("Finalization and fee assessment are idempotent", () =>
{
    var ledger = AssessmentScenario.Replay();
    int entries = ledger.Entries.Count;
    ledger.Finish();
    Equal(entries, ledger.Entries.Count);
    Equal(3, ledger.Fees.Count);
    Equal(12, ledger.Accruals.Count);
    Equal(3, ledger.Fees.Select(f => (f.AccountId, f.Day)).Distinct().Count());
});
Test("New events after finalization are refused", () =>
{
    var ledger = AssessmentScenario.Replay();
    Throws(() => ledger.Process(Credit("LATE", 1m, 6)));
});
Test("Fee amount is configurable and participates in downstream daily basis", () =>
{
    var ledger = new AccountLedger(new[] {new Account("ACC-001", AssessmentScenario.Aed, 0m)},
        new FixedOverdraftFeePolicy(new Dictionary<string, decimal> { ["AED"] = 30m }), new DailyInterestPolicy(0m), 2);
    ledger.Process(new("D", 1, 1, EventKind.Debit, "ACC-001", 1m));
    ledger.Finish();
    Equal(-61m, ledger.Balance("ACC-001", 2));
    Equal(60m, ledger.Fees.Sum(f => f.Amount));
});
Test("Interest rate is configurable, positive balances only", () =>
{
    var policy = new DailyInterestPolicy(0.001m);
    var account = new Account("A", AssessmentScenario.Aed, 0m);
    Equal(1m, policy.Calculate(account, 1, 1000m));
    Equal(0m, policy.Calculate(account, 1, -1000m));
    Equal(0m, policy.Calculate(account, 1, 0m));
});
Test("Rounding is AwayFromZero for both currency precisions", () =>
{
    Equal(1.01m, AssessmentScenario.Aed.Round(1.005m));
    Equal(-1.01m, AssessmentScenario.Aed.Round(-1.005m));
    Equal(1.001m, AssessmentScenario.Bhd.Round(1.0005m));
});
Test("Allocator property: conserve total across currency, amount, and part counts", () =>
{
    foreach (Currency currency in new[] {AssessmentScenario.Aed, AssessmentScenario.Bhd})
    for (int units = 1; units <= 50; units++)
    for (int count = 1; count <= 7; count++)
    {
        decimal amount = units / (currency.DecimalPlaces == 2 ? 100m : 1000m);
        var parts = InstalmentAllocator.Split(amount, count, currency);
        Equal(amount, parts.Sum());
        Equal(true, parts.All(p => p == currency.Round(p) && p >= 0m));
        Equal(true, parts.Max() - parts.Min() <= (currency.DecimalPlaces == 2 ? 0.01m : 0.001m));
    }
});
Test("Invalid amount and instalment count reject without principal entries", () =>
{
    var ledger = AssessmentScenario.Create();
    Equal(false, ledger.Process(Credit("ZERO", 0m)).Accepted);
    Equal(false, ledger.Process(Credit("NEG", -1m)).Accepted);
    Equal(false, ledger.Process(Credit("TINY", 0.001m)).Accepted);
    Equal(false, ledger.Process(new("PARTS", 1, 1, EventKind.Credit, "ACC-001", 1m, Instalments: 0)).Accepted);
    Equal(0, ledger.Entries.Count);
});
Test("Unknown account rejects and records an error", () =>
{
    var ledger = AssessmentScenario.Create();
    Equal(false, ledger.Process(new("X", 1, 1, EventKind.Credit, "missing", 1m)).Accepted);
    Equal(1, ledger.Results.Count);
});
Test("Unsupported BHD overdraft policy fails explicitly, no invented FX fee", () =>
{
    var ledger = AssessmentScenario.Create();
    ledger.Process(new("D", 1, 1, EventKind.Debit, "ACC-002", 1m));
    Throws(ledger.Finish);
    Equal(0, ledger.Fees.Count);
});
Test("Invalid dates are refused before input replay", () =>
{
    var ledger = AssessmentScenario.Create();
    Throws(() => ledger.Process(Credit("D0", 1m, 0)));
    Throws(() => ledger.Process(Credit("D7", 1m, 7)));
    Equal(0, ledger.Results.Count);
});

Test("Reserved system source IDs cannot collide with external input", () =>
{
    var ledger = AssessmentScenario.Create();
    Throws(() => ledger.Process(Credit("FEE:ACC-001:1", 1m)));
    Throws(() => ledger.Process(Credit("INTEREST:ACC-001", 1m)));
    Equal(0, ledger.Results.Count);
});
Test("Invalid later-account interest policy cannot partly capitalize an earlier account", () =>
{
    var ledger = new AccountLedger(new[] {
        new Account("A", AssessmentScenario.Aed, 100m),
        new Account("B", AssessmentScenario.Bhd, 100m)
    }, new FixedOverdraftFeePolicy(new Dictionary<string, decimal> { ["AED"] = 25m }),
        new InvalidBhdInterestPolicy(), 1);
    Throws(ledger.Finish);
    Equal(0, ledger.Accruals.Count);
    Equal(0, ledger.Entries.Count);
});

// Deliberate red test against OUR model, not an assertion of a false assessment criterion.
// This reveals that an abandoned active hold never expires automatically. Finish() is
// only an interest/fee watermark; it is not an authorization timeout scheduler. In production
// an append-only Expired transition with a network-specific deadline is required.
// Keep this as a real failing assertion, opt-in, not skipped and not swallowed as success.
if (args.Contains("--known-failure"))
{
    Test("KNOWN LIMITATION: abandoned authorization should eventually expire", () =>
    {
        var ledger = Through(3);
        ledger.Finish();
        Equal(0, ledger.CurrentAuthorizations("ACC-001").Count(a => a.Status == AuthorizationStatus.Active));
    });
}

int failed = 0;
foreach (var test in suite)
{
    try { test.Body(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception exception) { failed++; Console.WriteLine($"FAIL {test.Name}: {exception.Message}"); }
}
Console.WriteLine($"{suite.Count - failed} passed; {failed} failed; {suite.Count} total.");
if (!args.Contains("--known-failure"))
    Console.WriteLine("Required annotated failing test: rerun with --known-failure (expected exit 1).");
return failed == 0 ? 0 : 1;

sealed class InvalidBhdInterestPolicy : IInterestPolicy
{
    public decimal Calculate(Account account, int day, decimal closingBalance)
    {
        return account.Currency.Code == "BHD" ? -1m : 1m;
    }
}
