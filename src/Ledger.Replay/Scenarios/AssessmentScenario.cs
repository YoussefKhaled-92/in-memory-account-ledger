using System;
using System.Collections.Generic;
using Ledger.Domain;

namespace Ledger.Core;

/// <summary>The composition root keeps specified scenario constants outside business services.</summary>
public static class AssessmentScenario
{
    public static readonly Currency Aed = new("AED", 2);

    public static readonly Currency Bhd = new("BHD", 3);

    public const int WindowDays = 6;

    public const decimal AedOverdraftFee = 25.00m;

    public const decimal DailyRate = 0.0004m;

    public static AccountLedger Create()
    {
        return new AccountLedger(
            new[] { new Account("ACC-001", Aed, 0.00m), new Account("ACC-002", Bhd, 0.000m) },
            new FixedOverdraftFeePolicy(new Dictionary<string, decimal> { ["AED"] = AedOverdraftFee }),
            new DailyInterestPolicy(DailyRate), WindowDays);
    }

    public static IReadOnlyList<FinancialEvent> Events => Array.AsReadOnly(new[]
    {
        new FinancialEvent("E1", 1, 1, EventKind.Credit, "ACC-001", 1200m),
        new FinancialEvent("E2", 1, 1, EventKind.Debit, "ACC-001", 950m),
        new FinancialEvent("E3", 2, 2, EventKind.Authorization, "ACC-001", 200m, "Auth-A"),
        new FinancialEvent("E4", 3, 3, EventKind.Credit, "ACC-001", 400m),
        new FinancialEvent("E5", 4, 4, EventKind.Settlement, "ACC-001", 185m, "Auth-A"),
        new FinancialEvent("E6", 4, 4, EventKind.Settlement, "ACC-001", 180m, "Auth-Z"),
        new FinancialEvent("E7", 5, 2, EventKind.Debit, "ACC-001", 620m),
        new FinancialEvent("E8", 5, 5, EventKind.Authorization, "ACC-001", 90m, "Auth-B"),
        new FinancialEvent("E9", 6, 2, EventKind.Reversal, "ACC-001", Reference: "E7"),
        new FinancialEvent("E10", 5, 5, EventKind.Credit, "ACC-002", 10.000m, Instalments: 3)
    });

    public static AccountLedger Replay()
    {
        AccountLedger ledger = Create();

        foreach (FinancialEvent input in Events)
        {
            ledger.Process(input);
        }

        ledger.Finish();

        return ledger;
    }
}
