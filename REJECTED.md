# Refused criteria and abandoned approaches

## Every acceptance criterion

1. **Accepted with a precise view qualifier.** Principal-only Day 2 balance at the end-of-Day-5 input checkpoint is `1200 - 950 - 620 = -370`. Actual fee-inclusive balance is -395. The phrase “before any fee is assessed” is a counterfactual calculation, not the observed state after the fee loop has run.
2. **REFUSED: exactly one fee on Day 2.** E7 restates several closes. Day 2 gets a 25 fee. Day 3 becomes `30 - 25 = 5`, so no fee. Day 4 becomes `-155 - 25 = -180`, then receives its own 25 fee. Day 5 is -205 before its own fee and closes at -230. Total: three assessments, on Days 2, 4 and 5. Day 5's assessment is recorded at the transition to Day 6, before E9. “Once per day” is not “once per event”.
3. **Accepted.** Auth-A was approved with 250 available and a 200 hold. The 185 settlement is within that active hold and accepted. Later backdating does not undo the historical approval/settlement.
4. **Accepted.** Auth-Z is unknown; E6 records rejection and no monetary entry. Cross-account or terminal authorizations likewise cannot be settled.
5. **Accepted as a conditional rule.** A successful authorization reduces available funds only. In this actual stream Auth-B is not approved: E8 sees -205 available. The code tests the positive branch independently with a funded account.
6. **REFUSED as a universal claim.** An opposite principal posting is not deletion of causality. E9 offsets E7, but earlier fee assessments and the rejected Auth-B decision remain. Under our disclosed retention policy the 75 is not refunded: Day 5's balance is 390 rather than the pre-E7 465. Append-only itself does not prohibit refunds; a different explicit product policy could append refund credits and restore *net* money while preserving history. The task does not establish that policy, so “all balances and fees return” is unjustified.
7. **REFUSED: three amounts of 3.334.** They sum to 10.002, manufacturing 0.002 BHD. Use 3.334, 3.333, 3.333; minor-unit allocation conserves 10.000 exactly.
8. **REFUSED: discard an interest remainder.** It contradicts exact reconciliation. Daily rounded accruals are authoritative. AED daily sum is 0.93, while rounding the raw aggregate 0.918 would give 0.92. Capitalize 0.93 and no mismatch arises. No amount is silently discarded.

## Approaches abandoned during this build/review

- **Automatic compensating fee refunds**, discussed before implementation: rejected in favor of retaining actual assessments until a distinct refund instruction exists. Neither convention is inferred as a universal bank standard. The decision is explicit in AMBIGUITIES.md; this was a design alternative, not code falsely claimed to have been implemented.
- **One immediate capitalization per account while still calculating later accounts**: the first implementation followed this sequence. Review replaced it with preparing and validating every account's daily accruals before appending any capitalization. Otherwise a malformed custom interest policy for a later account could leave a partially capitalized batch.
- **Dependency-free executable test harness (superseded):** selected at initial setup to avoid test packages. Replaced on 15 November 2026 at the owner's request with conventional xUnit, Microsoft.NET.Test.Sdk and the VSTest adapter. Standard discovery, filtering and TRX export now work. The required expiry failure now runs alongside the 52 passing cases in the same project; no Skip or exclusion. The earlier separate project/solution was unnecessary for the assessment and removed at the owner's request.
- **Sorting the supplied inputs by booked/value date**: rejected during fixture construction because E10 must stay last and approval decisions depend on information available at arrival.
- **Rounding one aggregate interest total**: rejected while checking the independent numbers; daily rounding and aggregate rounding differ by 0.01 in this very fixture.
- **Identical rounded BHD parts**: rejected at allocator design; they cannot conserve this total. We deliberately expose a largest-part difference of one minor unit instead.

Alternatives above distinguish design choices from code actually written and replaced. The timestamped worklog and real commit order are the evidence of work performed; no artificial multi-day history is manufactured.

## One failing test against our design

`tests/Ledger.Tests/Suites/AuthorizationExpiryTests.cs` contains the inline-annotated abandoned-authorization expiry test. Run:

```sh
dotnet test -c Release
```

The full suite reports 52 passed and one failed, with no skips. This test expects no active hold after the window; actual count is one. This is a genuine red assertion and the process exits 1. It reveals indefinite funds encumbrance if a merchant disappears. It does not prove the specified supplied replay incorrect: the assessment explicitly leaves a possible hold unsettled, and does not mandate an expiry duration. A production implementation needs a network/product deadline, an append-only Expired transition, idempotent release and a policy for late presentment. The supported explicit Release path does not make automatic expiry exist.
