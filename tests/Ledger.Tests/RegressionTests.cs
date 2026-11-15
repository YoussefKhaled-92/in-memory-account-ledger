using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ledger.Core;
using Ledger.Domain;
using Xunit;

public class RegressionTests
{
    [Fact(DisplayName = "Every scenario boundary preserves the pre-refactor complete history")]
    public void Complete_history_matches_baseline()
    {
        string expected = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "scenario-baseline.json"));
        Assert.Equal(expected.Replace("\r\n", "\n").Trim(), BehaviorTrace.Capture().Replace("\r\n", "\n").Trim());
    }

    static AccountLedger Through(int count)
    {
        var ledger = AssessmentScenario.Create();

        foreach (var input in AssessmentScenario.Events.Take(count))
        {
            ledger.Process(input);
        }

        return ledger;
    }

    static FinancialEvent Credit(string id, decimal amount, int day = 1) => new(id, day, day, EventKind.Credit, "ACC-001", amount);

    static FinancialEvent Hold(string id, decimal amount, string reference = "H", int day = 1) => new(id, day, day, EventKind.Authorization, "ACC-001", amount, reference);

    [Fact(DisplayName = "Criterion 1: raw Day 2 principal is -370 at E8")]
    public void Criterion_1_raw_Day_2_principal_is_370_at_E8()
    {
        var ledger = Through(8);
        decimal principal = ledger.Entries.Where(entry => entry.AccountId == "ACC-001" && entry.ValueDay <= 2
            && entry.Kind != EntryKind.OverdraftFee).Sum(entry => entry.Amount);
        Assert.Equal(-370m, principal);
        Assert.Equal(-395m, ledger.Balance("ACC-001", 2));
    }

    [Fact(DisplayName = "Criterion 2 refused: E7 affects Day 2 and Day 4 immediately, Day 5 on close")]
    public void Criterion_2_refused_E7_affects_Day_2_and_Day_4_immediately_Day_5_on_close()
    {
        var ledger = Through(7);

        Assert.Equal("2,4", string.Join(",", ledger.Fees.Select(fee => fee.Day)));
        ledger.Process(AssessmentScenario.Events[7]);
        ledger.Process(AssessmentScenario.Events[8]);
        Assert.Equal("2,4,5", string.Join(",", ledger.Fees.Select(fee => fee.Day)));
        Assert.Equal(75m, ledger.Fees.Sum(fee => fee.Amount));
    }

    [Fact(DisplayName = "Day 3 stays positive after the Day 2 fee and is not charged")]
    public void Day_3_stays_positive_after_the_Day_2_fee_and_is_not_charged()
    {
        var ledger = Through(8);

        Assert.Equal(5m, ledger.Balance("ACC-001", 3));
        Assert.DoesNotContain(ledger.Fees, fee => fee.Day == 3);
    }

    [Fact(DisplayName = "Criterion 3: Auth-A settles and releases unused 15")]
    public void Criterion_3_Auth_A_settles_and_releases_unused_15()
    {
        var ledger = Through(5);

        Assert.True(ledger.Results.Last().Accepted);
        Assert.Equal(465m, ledger.Balance("ACC-001", 4));
        Assert.Equal(465m, ledger.Available("ACC-001"));
        Assert.Equal(AuthorizationStatus.Settled, ledger.CurrentAuthorizations("ACC-001").Single().Status);
    }

    [Fact(DisplayName = "Criterion 4: unknown authorization cannot debit funds")]
    public void Criterion_4_unknown_authorization_cannot_debit_funds()
    {
        var ledger = Through(5);
        int before = ledger.Entries.Count;

        Assert.False(ledger.Process(AssessmentScenario.Events[5]).Accepted);
        Assert.Equal(before, ledger.Entries.Count);
        Assert.Equal(465m, ledger.Balance("ACC-001", 4));
    }

    [Fact(DisplayName = "Criterion 5: approved hold changes available only")]
    public void Criterion_5_approved_hold_changes_available_only()
    {
        var ledger = Through(3);

        Assert.Equal(250m, ledger.Balance("ACC-001", 2));
        Assert.Equal(50m, ledger.Available("ACC-001"));
    }

    [Fact(DisplayName = "Auth-B is rejected at its original decision point and never retried after E9")]
    public void Auth_B_is_rejected_at_its_original_decision_point_and_never_retried_after_E9()
    {
        var ledger = AssessmentScenario.Replay();

        Assert.Equal(AuthorizationStatus.Rejected, ledger.CurrentAuthorizations("ACC-001").Single(a => a.AuthorizationId == "Auth-B").Status);
        Assert.False(ledger.Results.Single(r => r.Input.Id == "E8").Accepted);
        Assert.Equal(390.93m, ledger.Available("ACC-001"));
    }

    [Fact(DisplayName = "Criterion 6 refused: reversing E7 retains 75 of assessed fees")]
    public void Criterion_6_refused_reversing_E7_retains_75_of_assessed_fees()
    {
        var ledger = Through(9);

        Assert.Equal(390m, ledger.Balance("ACC-001", 6));
        Assert.Equal(75m, ledger.Fees.Sum(f => f.Amount));
        Assert.Equal(1, ledger.Entries.Count(e => e.Kind == EntryKind.Reversal));
    }

    [Fact(DisplayName = "Criterion 7 refused: BHD minor units conserved across unequal rounded parts")]
    public void Criterion_7_refused_BHD_minor_units_conserved_across_unequal_rounded_parts()
    {
        var parts = InstalmentAllocator.Split(10m, 3, AssessmentScenario.Bhd);

        Assert.Equal("3.334,3.333,3.333", string.Join(",", parts.Select(p => p.ToString("F3"))));
        Assert.Equal(10m, parts.Sum());
    }

    [Fact(DisplayName = "Criterion 8: capitalization is exactly the sum of daily rounded accruals")]
    public void Criterion_8_capitalization_is_exactly_the_sum_of_daily_rounded_accruals()
    {
        var ledger = AssessmentScenario.Replay();

        foreach (Account account in ledger.Accounts)
        {
            Assert.Equal(ledger.Accruals.Where(a => a.AccountId == account.Id).Sum(a => a.Amount),
                ledger.Entries.Single(e => e.AccountId == account.Id && e.Kind == EntryKind.Interest).Amount);
        }

        Assert.Equal(0.93m, ledger.Entries.Single(e => e.AccountId == "ACC-001" && e.Kind == EntryKind.Interest).Amount);
        Assert.Equal(0.008m, ledger.Entries.Single(e => e.AccountId == "ACC-002" && e.Kind == EntryKind.Interest).Amount);
    }

    [Fact(DisplayName = "Final AED daily balances agree with independent hand calculation")]
    public void Final_AED_daily_balances_agree_with_independent_hand_calculation()
    {
        var ledger = AssessmentScenario.Replay();

        decimal[] expected = { 250m, 225m, 625m, 415m, 390m, 390.93m };

        for (int day = 1; day <= 6; day++)
        {
            Assert.Equal(expected[day - 1], ledger.Balance("ACC-001", day));
        }
    }

    [Fact(DisplayName = "Final daily AED accruals are restated, positive only, excluding capitalization")]
    public void Final_daily_AED_accruals_are_restated_positive_only_excluding_capitalization()
    {
        var ledger = AssessmentScenario.Replay();

        Assert.Equal("0.10,0.09,0.25,0.17,0.16,0.16", string.Join(",", ledger.Accruals
            .Where(a => a.AccountId == "ACC-001").Select(a => a.Amount.ToString("F2"))));
    }

    [Fact(DisplayName = "E10 remains last, its booked day does not rewind recorded time")]
    public void E10_remains_last_its_booked_day_does_not_rewind_recorded_time()
    {
        var ledger = AssessmentScenario.Replay();

        Assert.Equal("E1,E2,E3,E4,E5,E6,E7,E8,E9,E10", string.Join(",", ledger.Results.Select(r => r.Input.Id)));
        Assert.Equal(5, ledger.Results.Last().Input.BookedDay);
        Assert.Equal(6, ledger.Results.Last().RecordedDay);
        Assert.Equal(10m, ledger.Balance("ACC-002", 5));
        Assert.Equal(10.008m, ledger.Balance("ACC-002", 6));
    }

    [Fact(DisplayName = "Backdating preserves every previously appended entry and decision")]
    public void Backdating_preserves_every_previously_appended_entry_and_decision()
    {
        var ledger = Through(6);
        LedgerEntry[] before = ledger.Entries.ToArray();

        AuthorizationChange[] decisions = ledger.AuthorizationHistory.ToArray();

        foreach (var input in AssessmentScenario.Events.Skip(6))
        {
            ledger.Process(input);
        }

        ledger.Finish();
        Assert.True(before.SequenceEqual(ledger.Entries.Take(before.Length)));
        Assert.True(decisions.SequenceEqual(ledger.AuthorizationHistory.Take(decisions.Length)));
    }

    [Fact(DisplayName = "Public collection views cannot mutate ledger state")]
    public void Public_collection_views_cannot_mutate_ledger_state()
    {
        var ledger = Through(1);

        Assert.True(((IList<LedgerEntry>)ledger.Entries).IsReadOnly);

        var oldView = ledger.Entries;

        ledger.Process(AssessmentScenario.Events[1]);
        Assert.Single(oldView);
        Assert.Equal(2, ledger.Entries.Count);
    }

    [Fact(DisplayName = "Identical input retry is idempotent")]
    public void Identical_input_retry_is_idempotent()
    {
        var ledger = Through(1);
        var first = ledger.Results.Single();

        Assert.Equal(first, ledger.Process(AssessmentScenario.Events[0]));
        Assert.Single(ledger.Entries);
        Assert.Single(ledger.Results);
    }

    [Fact(DisplayName = "Conflicting input ID is refused before financial mutation")]
    public void Conflicting_input_ID_is_refused_before_financial_mutation()
    {
        var ledger = Through(1);

        Assert.Throws<LedgerRuleException>(() => ledger.Process(Credit("E1", 1m)));
        Assert.Equal(1200m, ledger.Balance("ACC-001", 1));
    }

    [Fact(DisplayName = "Repeated declined authorization returns original rejection without extra history")]
    public void Repeated_declined_authorization_returns_original_rejection_without_extra_history()
    {
        var ledger = AssessmentScenario.Create();
        var input = Hold("H1", 1m);

        Assert.False(ledger.Process(input).Accepted);
        Assert.False(ledger.Process(input).Accepted);
        Assert.Single(ledger.AuthorizationHistory);
    }

    [Fact(DisplayName = "Exact available boundary approves; one minor unit above rejects")]
    public void Exact_available_boundary_approves_one_minor_unit_above_rejects()
    {
        var ledger = AssessmentScenario.Create();

        ledger.Process(Credit("C", 100m));
        Assert.True(ledger.Process(Hold("H1", 100m)).Accepted);
        Assert.False(ledger.Process(Hold("H2", 0.01m, "H2")).Accepted);
        Assert.Equal(0m, ledger.Available("ACC-001"));
    }

    [Fact(DisplayName = "Settlement above hold rejects without closing hold or debiting")]
    public void Settlement_above_hold_rejects_without_closing_hold_or_debiting()
    {
        var ledger = Through(3);

        Assert.False(ledger.Process(new("S", 2, 2, EventKind.Settlement, "ACC-001", 201m, "Auth-A")).Accepted);
        Assert.Equal(250m, ledger.Balance("ACC-001", 2));
        Assert.Equal(AuthorizationStatus.Active, ledger.CurrentAuthorizations("ACC-001").Single().Status);
    }

    [Fact(DisplayName = "Repeated settlement with new ID cannot debit twice")]
    public void Repeated_settlement_with_new_ID_cannot_debit_twice()
    {
        var ledger = Through(5);

        Assert.False(ledger.Process(new("S2", 4, 4, EventKind.Settlement, "ACC-001", 185m, "Auth-A")).Accepted);
        Assert.Equal(465m, ledger.Balance("ACC-001", 4));
    }

    [Fact(DisplayName = "Authorization identity is account scoped")]
    public void Authorization_identity_is_account_scoped()
    {
        var ledger = Through(3);

        Assert.False(ledger.Process(new("S", 2, 2, EventKind.Settlement, "ACC-002", 1m, "Auth-A")).Accepted);
        Assert.Equal(0m, ledger.Balance("ACC-002", 2));
    }

    [Fact(DisplayName = "Explicit release ends a hold and leaves ledger unchanged")]
    public void Explicit_release_ends_a_hold_and_leaves_ledger_unchanged()
    {
        var ledger = Through(3);

        Assert.True(ledger.Process(new("R", 2, 2, EventKind.Release, "ACC-001", Reference: "Auth-A")).Accepted);
        Assert.Equal(250m, ledger.Available("ACC-001"));
        Assert.Equal(AuthorizationStatus.Released, ledger.CurrentAuthorizations("ACC-001").Single().Status);
        Assert.False(ledger.Process(new("S", 2, 2, EventKind.Settlement, "ACC-001", 1m, "Auth-A")).Accepted);
    }

    [Fact(DisplayName = "Settlement affordability is not retrospectively re-decided")]
    public void Settlement_affordability_is_not_retrospectively_re_decided()
    {
        var ledger = Through(3);

        ledger.Process(new("BACK", 3, 1, EventKind.Debit, "ACC-001", 500m));
        Assert.True(ledger.Process(new("S", 3, 3, EventKind.Settlement, "ACC-001", 185m, "Auth-A")).Accepted);
    }

    [Fact(DisplayName = "Duplicate reversal is rejected with no duplicate compensating entry")]
    public void Duplicate_reversal_is_rejected_with_no_duplicate_compensating_entry()
    {
        var ledger = Through(9);

        Assert.False(ledger.Process(new("R2", 6, 2, EventKind.Reversal, "ACC-001", Reference: "E7")).Accepted);
        Assert.Equal(1, ledger.Entries.Count(e => e.Kind == EntryKind.Reversal));
    }

    [Fact(DisplayName = "Unknown reversal and reversal at different value date are rejected")]
    public void Unknown_reversal_and_reversal_at_different_value_date_are_rejected()
    {
        var ledger = Through(7);

        Assert.False(ledger.Process(new("RX", 5, 2, EventKind.Reversal, "ACC-001", Reference: "missing")).Accepted);
        Assert.False(ledger.Process(new("RY", 5, 3, EventKind.Reversal, "ACC-001", Reference: "E7")).Accepted);
    }

    [Fact(DisplayName = "Reversing an instalment credit compensates every part exactly")]
    public void Reversing_an_instalment_credit_compensates_every_part_exactly()
    {
        var ledger = Through(10);

        Assert.True(ledger.Process(new("R10", 6, 5, EventKind.Reversal, "ACC-002", Reference: "E10")).Accepted);
        Assert.Equal(0m, ledger.Balance("ACC-002", 5));
        Assert.Equal(3, ledger.Entries.Count(e => e.SourceId == "R10"));
    }

    [Fact(DisplayName = "Finalization and fee assessment are idempotent")]
    public void Finalization_and_fee_assessment_are_idempotent()
    {
        var ledger = AssessmentScenario.Replay();
        int entries = ledger.Entries.Count;

        ledger.Finish();
        Assert.Equal(entries, ledger.Entries.Count);
        Assert.Equal(3, ledger.Fees.Count);
        Assert.Equal(12, ledger.Accruals.Count);
        Assert.Equal(3, ledger.Fees.Select(f => (f.AccountId, f.Day)).Distinct().Count());
    }

    [Fact(DisplayName = "New events after finalization are refused")]
    public void New_events_after_finalization_are_refused()
    {
        var ledger = AssessmentScenario.Replay();

        Assert.Throws<LedgerRuleException>(() => ledger.Process(Credit("LATE", 1m, 6)));
    }

    [Fact(DisplayName = "Fee amount is configurable and participates in downstream daily basis")]
    public void Fee_amount_is_configurable_and_participates_in_downstream_daily_basis()
    {
        var ledger = new AccountLedger(new[] { new Account("ACC-001", AssessmentScenario.Aed, 0m) },
            new FixedOverdraftFeePolicy(new Dictionary<string, decimal> { ["AED"] = 30m }), new DailyInterestPolicy(0m), 2);
        ledger.Process(new("D", 1, 1, EventKind.Debit, "ACC-001", 1m));
        ledger.Finish();
        Assert.Equal(-61m, ledger.Balance("ACC-001", 2));
        Assert.Equal(60m, ledger.Fees.Sum(f => f.Amount));
    }

    [Fact(DisplayName = "Interest rate is configurable, positive balances only")]
    public void Interest_rate_is_configurable_positive_balances_only()
    {
        var policy = new DailyInterestPolicy(0.001m);
        var account = new Account("A", AssessmentScenario.Aed, 0m);

        Assert.Equal(1m, policy.Calculate(account, 1, 1000m));
        Assert.Equal(0m, policy.Calculate(account, 1, -1000m));
        Assert.Equal(0m, policy.Calculate(account, 1, 0m));
    }

    [Fact(DisplayName = "Rounding is AwayFromZero for both currency precisions")]
    public void Rounding_is_AwayFromZero_for_both_currency_precisions()
    {
        Assert.Equal(1.01m, AssessmentScenario.Aed.Round(1.005m));
        Assert.Equal(-1.01m, AssessmentScenario.Aed.Round(-1.005m));
        Assert.Equal(1.001m, AssessmentScenario.Bhd.Round(1.0005m));
    }

    [Fact(DisplayName = "Allocator property: conserve total across currency, amount, and part counts")]
    public void Allocator_property_conserve_total_across_currency_amount_and_part_counts()
    {
        foreach (Currency currency in new[] { AssessmentScenario.Aed, AssessmentScenario.Bhd })
        {
            for (int units = 1; units <= 50; units++)
            {
                for (int count = 1; count <= 7; count++)
                {
                    decimal amount = units / (currency.DecimalPlaces == 2 ? 100m : 1000m);
                    var parts = InstalmentAllocator.Split(amount, count, currency);

                    Assert.Equal(amount, parts.Sum());
                    Assert.True(parts.All(p => p == currency.Round(p) && p >= 0m));
                    Assert.True(parts.Max() - parts.Min() <= (currency.DecimalPlaces == 2 ? 0.01m : 0.001m));
                }
            }
        }
    }

    [Fact(DisplayName = "Invalid amount and instalment count reject without principal entries")]
    public void Invalid_amount_and_instalment_count_reject_without_principal_entries()
    {
        var ledger = AssessmentScenario.Create();

        Assert.False(ledger.Process(Credit("ZERO", 0m)).Accepted);
        Assert.False(ledger.Process(Credit("NEG", -1m)).Accepted);
        Assert.False(ledger.Process(Credit("TINY", 0.001m)).Accepted);
        Assert.False(ledger.Process(new("PARTS", 1, 1, EventKind.Credit, "ACC-001", 1m, Instalments: 0)).Accepted);
        Assert.Empty(ledger.Entries);
    }

    [Fact(DisplayName = "Unknown account rejects and records an error")]
    public void Unknown_account_rejects_and_records_an_error()
    {
        var ledger = AssessmentScenario.Create();

        Assert.False(ledger.Process(new("X", 1, 1, EventKind.Credit, "missing", 1m)).Accepted);
        Assert.Single(ledger.Results);
    }

    [Fact(DisplayName = "Unsupported BHD overdraft policy fails explicitly, no invented FX fee")]
    public void Unsupported_BHD_overdraft_policy_fails_explicitly_no_invented_FX_fee()
    {
        var ledger = AssessmentScenario.Create();

        ledger.Process(new("D", 1, 1, EventKind.Debit, "ACC-002", 1m));
        Assert.Throws<LedgerRuleException>(ledger.Finish);
        Assert.Empty(ledger.Fees);
    }

    [Fact(DisplayName = "Invalid dates are refused before input replay")]
    public void Invalid_dates_are_refused_before_input_replay()
    {
        var ledger = AssessmentScenario.Create();

        Assert.Throws<LedgerRuleException>(() => ledger.Process(Credit("D0", 1m, 0)));
        Assert.Throws<LedgerRuleException>(() => ledger.Process(Credit("D7", 1m, 7)));
        Assert.Empty(ledger.Results);
    }

    [Fact(DisplayName = "Reserved system source IDs cannot collide with external input")]
    public void Reserved_system_source_IDs_cannot_collide_with_external_input()
    {
        var ledger = AssessmentScenario.Create();

        Assert.Throws<LedgerRuleException>(() => ledger.Process(Credit("FEE:ACC-001:1", 1m)));
        Assert.Throws<LedgerRuleException>(() => ledger.Process(Credit("INTEREST:ACC-001", 1m)));
        Assert.Empty(ledger.Results);
    }

    [Fact(DisplayName = "Invalid later-account interest policy cannot partly capitalize an earlier account")]
    public void Invalid_later_account_interest_policy_cannot_partly_capitalize_an_earlier_account()
    {
        var ledger = new AccountLedger(new[] {
        new Account("A", AssessmentScenario.Aed, 100m),
        new Account("B", AssessmentScenario.Bhd, 100m)
    }, new FixedOverdraftFeePolicy(new Dictionary<string, decimal> { ["AED"] = 25m }),
            new InvalidBhdInterestPolicy(), 1);
        Assert.Throws<LedgerRuleException>(ledger.Finish);
        Assert.Empty(ledger.Accruals);
        Assert.Empty(ledger.Entries);
    }
}
sealed class InvalidBhdInterestPolicy : IInterestPolicy
{
    public decimal Calculate(Account account, int day, decimal closingBalance)
    {
        return account.Currency.Code == "BHD" ? -1m : 1m;
    }
}
