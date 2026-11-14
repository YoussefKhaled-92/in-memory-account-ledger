using System.Globalization;
using Ledger.Core;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
var ledger = AssessmentScenario.Create();
Console.WriteLine("ARRIVAL ORDER (recorded clock is monotonic; balances use value dates)");
foreach (FinancialEvent input in AssessmentScenario.Events)
{
    EventResult result = ledger.Process(input);
    Console.WriteLine($"{input.Id} booked={input.BookedDay} recorded={result.RecordedDay} " +
        $"{(result.Accepted ? "ACCEPTED" : "REJECTED: " + result.Error)} " +
        $"| AED current={ledger.Balance("ACC-001", ledger.RecordedDay):F2} " +
        $"available={ledger.Available("ACC-001"):F2}");
    if (input.Id == "E8")
        Console.WriteLine("  End of Day 5 input checkpoint, before Day 5 close: " +
            string.Join(", ", Enumerable.Range(1, 5).Select(day =>
                $"D{day}={ledger.Balance("ACC-001", day):F2}")));
}
ledger.Finish();

Console.WriteLine("\nFINAL RESTATED DAILY REPORT (known after E10 and finalization)");
Console.WriteLine("Fees are retained assessments. Authorization decisions are as recorded, not restated.");
for (int day = 1; day <= ledger.WindowDays; day++)
{
    Console.WriteLine($"Day {day}");
    foreach (Account account in ledger.Accounts)
    {
        string format = "F" + account.Currency.DecimalPlaces;
        InterestAccrual interest = ledger.Accruals.Single(item => item.AccountId == account.Id && item.Day == day);
        var dayFees = ledger.Fees.Where(fee => fee.AccountId == account.Id && fee.Day == day).ToArray();
        Console.WriteLine($"  {account.Id} {account.Currency.Code} " +
            $"closing={ledger.Balance(account.Id, day).ToString(format)} " +
            $"interest-basis={interest.Basis.ToString(format)} accrual={interest.Amount.ToString(format)} " +
            $"fee-count={dayFees.Length} fee-total={dayFees.Sum(fee => fee.Amount).ToString(format)}");
        foreach (FeeAssessment fee in dayFees)
            Console.WriteLine($"    fee value-day={fee.Day}, assessed on recorded-day={fee.RecordedDay}");
        var states = ledger.AuthorizationHistory.Where(change => change.AccountId == account.Id && change.RecordedDay <= day)
            .GroupBy(change => change.AuthorizationId).Select(group => group.Last()).ToArray();
        Console.WriteLine("    authorizations as observed by day: " + (states.Length == 0 ? "none" :
            string.Join(", ", states.Select(state => $"{state.AuthorizationId}={state.Status}"))));
        var errors = ledger.Results.Where(result => result.Input.AccountId == account.Id
            && result.RecordedDay == day && !result.Accepted).ToArray();
        Console.WriteLine("    errors recorded this day: " + (errors.Length == 0 ? "none" :
            string.Join("; ", errors.Select(error => $"{error.Input.Id}: {error.Error}"))));
    }
}
Console.WriteLine("\nCAPITALIZATION (one credit per funded account, exact sum of daily rounded accruals)");
foreach (LedgerEntry entry in ledger.Entries.Where(entry => entry.Kind == EntryKind.Interest))
    Console.WriteLine($"{entry.AccountId}: {entry.Amount} value-day={entry.ValueDay}");
Console.WriteLine("BHD instalments: " + string.Join(" + ", ledger.Entries.Where(entry => entry.SourceId == "E10")
    .Select(entry => entry.Amount.ToString("F3"))));
