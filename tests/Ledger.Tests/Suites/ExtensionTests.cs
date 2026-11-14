using System;
using System.Collections.Generic;
using System.Linq;
using Ledger.Application;
using Ledger.Core;
using Ledger.Domain;

static class ExtensionTests
{
    public static void Register(Action<string, Action> test)
    {
        test("A client operation runs through the unchanged coordinator and retains retry/finality rules", () =>
        {
            var handler = new PromotionalCredit();
            var ledger = new AccountLedger([new Account("A", new Currency("AED", 2), 0m)],
                new FixedOverdraftFeePolicy(new Dictionary<string, decimal> { ["AED"] = 25m }),
                new DailyInterestPolicy(0m), 2, [handler]);
            var input = new FinancialEvent("CLIENT:1", 1, 1, handler.Kind, "A", 12.345m);
            var first = ledger.Process(input);

            Check(first.Accepted && ledger.Balance("A", 1) == 12.35m);
            Check(ledger.Process(input) == first && handler.Calls == 1);
            Check(ledger.Entries.Single().Sequence == 1 && first.Sequence == 2);
            ledger.Finish();
            Check(ledger.Process(input) == first && handler.Calls == 1);
            Expect<LedgerRuleException>(() => ledger.Process(input with { Id = "CLIENT:2" }));
            Check(handler.Calls == 1);
        });

        test("Registry rejects duplicate operation registrations before execution", () =>
            Expect<ArgumentException>(() => new EventHandlerRegistry([new PostCredit(), new PostCredit()])));
        test("Registry rejects blank operation keys", () =>
            Expect<ArgumentException>(() => new EventHandlerRegistry([new PromotionalCredit(new EventKind(" "))])));
        test("Unknown operation records a decline and leaves money and holds unchanged", () =>
        {
            var ledger = AssessmentScenario.Create();
            var result = ledger.Process(new("X", 1, 1, new EventKind("Unknown"), "ACC-001", 10m));

            Check(!result.Accepted && result.Error == "Unsupported event kind.");
            Check(ledger.Entries.Count == 0 && ledger.AuthorizationHistory.Count == 0);
            Check(result.Sequence == 1);
        });

        test("Day-boundary policy failure stays fatal before dispatch without recording an outcome", () =>
        {
            var ledger = AssessmentScenario.Create();

            ledger.Process(new("D", 1, 1, EventKind.Debit, "ACC-002", 1m));
            Expect<LedgerRuleException>(() => ledger.Process(new("C", 2, 2, EventKind.Credit, "ACC-002", 5m)));
            Check(ledger.RecordedDay == 2 && ledger.Results.Count == 1);
            Check(ledger.Entries.Count == 1 && ledger.Fees.Count == 0);
        });

        test("Post-success fee failure retains the accepted outcome as documented", () =>
        {
            var ledger = AssessmentScenario.Create();

            ledger.Process(new("ADVANCE", 2, 2, EventKind.Credit, "ACC-001", 1m));
            Expect<LedgerRuleException>(() => ledger.Process(new("BACK", 2, 1, EventKind.Debit, "ACC-002", 1m)));
            Check(ledger.Results.Last().Accepted && ledger.Results.Last().Input.Id == "BACK");
            Check(ledger.Entries.Last().Amount == -1m);

            var retry = ledger.Process(new("BACK", 2, 1, EventKind.Debit, "ACC-002", 1m));

            Check(retry.Accepted && ledger.Results.Count == 2);
        });

        test("Authorization domain forbids terminal settlement and excess capture", () =>
        {
            var latest = new AuthorizationChange(1, "H", "A", "H", 1, 1, 1, 10m, AuthorizationStatus.Active);

            new Authorization(latest).ValidateSettlement(10m);
            Expect<LedgerRuleException>(() => new Authorization(latest).ValidateSettlement(10.01m));

            foreach (var status in new[] { AuthorizationStatus.Settled, AuthorizationStatus.Released, AuthorizationStatus.Rejected })
            {
                Expect<LedgerRuleException>(() => new Authorization(latest with { Status = status }).RequireActive());
            }
        });
    }

    static void Check(bool condition)
    {
        if (!condition)
        {
            throw new Exception("Extension or workflow contract violated.");
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

    // Client-owned feature: registration is the only composition change. No production
    // dispatcher, kind declaration, journal, or fee/finality workflow was edited for it.
    sealed class PromotionalCredit(EventKind? kind = null) : IEventHandler
    {
        public EventKind Kind => kind ?? new EventKind("Client.PromotionalCredit");

        public int Calls
        {
            get; private set;
        }

        public void Handle(FinancialEvent input, OperationContext context)
        {
            decimal amount = PostingRules.PositiveAmount(input, context.Account);

            Calls++;
            context.Post(input, amount, EntryKind.Credit);
        }
    }
}
