using System;
using System.Collections.Generic;
using System.Linq;
using Ledger.Application;
using Ledger.Core;
using Ledger.Domain;

static class CompositionTests
{
    public static void Register(Action<string, Action> test)
    {
        test("Containers isolate journal, sequences, holds, fees, accruals and finality per ledger", () =>
        {
            using var first = AssessmentScenario.Create();
            using var second = AssessmentScenario.Create();
            first.Process(new("C", 1, 1, EventKind.Credit, "ACC-001", 100m));
            first.Process(new("H", 1, 1, EventKind.Authorization, "ACC-001", 20m, "H"));
            first.Process(new("D", 2, 2, EventKind.Debit, "ACC-001", 200m));
            first.Finish();

            Check(first.Fees.Count > 0 && first.Accruals.Count > 0);
            Check(second.RecordedDay == 1 && second.Entries.Count == 0 && second.Results.Count == 0);
            Check(second.AuthorizationHistory.Count == 0 && second.Fees.Count == 0 && second.Accruals.Count == 0);
            Check(second.Available("ACC-001") == 0m);
            var result = second.Process(new("C", 1, 1, EventKind.Credit, "ACC-001", 10m));
            Check(result.Accepted && result.Sequence == 2 && second.Entries.Single().Sequence == 1);
            Check(second.Balance("ACC-001", 1) == 10m);
        });

        test("Per-ledger container preserves independent fee and interest configuration", () =>
        {
            using var first = Create(5m, 0m);
            using var second = Create(7m, 0.01m);
            first.Process(new("D", 1, 1, EventKind.Debit, "A", 1m));
            second.Process(new("C", 1, 1, EventKind.Credit, "A", 100m));
            first.Finish();
            second.Finish();
            Check(first.Balance("A", 2) == -11m && first.Fees.Sum(f => f.Amount) == 10m);
            Check(second.Balance("A", 2) == 102m && second.Fees.Count == 0);
        });

        test("Facade eagerly rejects duplicate handlers and invalid configuration", () =>
        {
            Expect<ArgumentException>(() => Create(5m, 0m, [new PostCredit()]));
            Expect<ArgumentOutOfRangeException>(() => new AccountLedger(
                [new Account("A", AssessmentScenario.Aed, 0m)],
                new FixedOverdraftFeePolicy(new Dictionary<string, decimal> { ["AED"] = 5m }),
                new DailyInterestPolicy(0m), 0));
            Expect<ArgumentException>(() => new AccountLedger([], new ClientServices(), new ClientServices(), 2));
        });

        test("Disposal releases only the owned container and never finalizes the ledger", () =>
        {
            var client = new ClientServices();
            var ledger = new AccountLedger([new Account("A", AssessmentScenario.Aed, 100m)],
                client, client, 2, [client]);
            Check(ledger.Process(new("P", 1, 1, client.Kind, "A", 3m)).Accepted);
            ledger.Dispose();
            ledger.Dispose();
            Check(!client.Disposed && ledger.Accruals.Count == 0 && ledger.Balance("A", 1) == 103m);
            Expect<ObjectDisposedException>(() => ledger.Process(new("X", 1, 1, client.Kind, "A", 1m)));
            Expect<ObjectDisposedException>(ledger.Finish);
        });

        test("A custom handler registered in one container does not leak into another", () =>
        {
            var client = new ClientServices();
            using var first = Create(5m, 0m, [client]);
            using var second = Create(5m, 0m);
            var input = new FinancialEvent("P", 1, 1, client.Kind, "A", 3m);
            Check(first.Process(input).Accepted);
            Check(!second.Process(input).Accepted && second.Entries.Count == 0);
        });
    }

    static AccountLedger Create(decimal fee, decimal rate, IEnumerable<IEventHandler>? handlers = null) =>
        new([new Account("A", AssessmentScenario.Aed, 0m)],
            new FixedOverdraftFeePolicy(new Dictionary<string, decimal> { ["AED"] = fee }),
            new DailyInterestPolicy(rate), 2, handlers);

    static void Check(bool condition)
    {
        if (!condition)
        {
            throw new Exception("Container composition contract violated.");
        }
    }

    static void Expect<T>(Action body) where T : Exception
    {
        try
        {
            body();
        }
        catch (T)
        {
            return;
        }

        throw new Exception($"Expected {typeof(T).Name}.");
    }

    sealed class ClientServices : IEventHandler, IOverdraftFeePolicy, IInterestPolicy, IDisposable
    {
        public EventKind Kind => new("Client.CompositionCredit");

        public bool Disposed
        {
            get; private set;
        }

        public void Handle(FinancialEvent input, OperationContext context) =>
            context.Post(input, input.Amount, EntryKind.Credit);

        public decimal GetFee(Account account, int assessmentDay) => 5m;

        public decimal Calculate(Account account, int day, decimal closingBalance) => 0m;

        public void Dispose() => Disposed = true;
    }
}
