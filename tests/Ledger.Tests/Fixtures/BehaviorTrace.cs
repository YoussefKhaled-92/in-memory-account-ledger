using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Ledger.Core;

// Captured against fcc7c713 before refactoring. The complete public history at each
// boundary catches ordering changes that a final-balance assertion cannot see.

static class BehaviorTrace
{
    public static string Capture()
    {
        var frames = new List<object>();
        var ledger = AssessmentScenario.Create();

        Snapshot("opening");

        foreach (var input in AssessmentScenario.Events)
        {
            ledger.Process(input);
            Snapshot(input.Id);
        }

        ledger.Finish();
        Snapshot("finished");
        ledger.Finish();
        ledger.Process(AssessmentScenario.Events[0]);
        Snapshot("finish-and-input-retries");

        return JsonSerializer.Serialize(frames, new JsonSerializerOptions { WriteIndented = true });

        void Snapshot(string label) => frames.Add(new
        {
            label,
            ledger.RecordedDay,
            ledger.WindowDays,
            ledger.Accounts,
            ledger.Entries,
            ledger.AuthorizationHistory,
            Results = ledger.Results.Select(r => new
            {
                r.Sequence,
                Input = new
                {
                    r.Input.Id,
                    r.Input.BookedDay,
                    r.Input.ValueDay,
                    Kind = r.Input.Kind.ToString(),
                    r.Input.AccountId,
                    r.Input.Amount,
                    r.Input.Reference,
                    r.Input.Instalments
                },
                r.RecordedDay,
                r.Accepted,
                r.Error
            }).ToArray(),
            ledger.Fees,
            ledger.Accruals,
            Balances = ledger.Accounts.Select(a => new
            {
                a.Id,
                Available = ledger.Available(a.Id),
                Current = ledger.CurrentAuthorizations(a.Id),
                Daily = Enumerable.Range(1, ledger.WindowDays).Select(d => new
                {
                    Day = d,
                    Balance = ledger.Balance(a.Id, d),
                    Basis = ledger.Balance(a.Id, d, includeInterest: false)
                }).ToArray()
            }).ToArray()
        });
    }
}
