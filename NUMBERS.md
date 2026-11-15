# Numbers and why they exist

All monetary arithmetic uses `decimal`; no binary floating point. These are daily rates, not annual rates divided by a guessed day-count basis.

## Specified numbers (not discretionary choices)

- Six business days, numbered 1 through 6; two accounts; zero opening balances. Three or twelve days would be a different assessment. Day 0 and Day 7 are rejected.
- AED 25.00 fee, once per account/day. AED 12.50 violates the specified amount. The default lives in `AssessmentScenario.AedOverdraftFee` and is injected through a currency-keyed fee policy, not scattered through handlers.
- Daily rate 0.04% = 0.0004. A rate of 0.0002 halves customer entitlement and contradicts the task. `DailyInterestPolicy` accepts a replacement rate; policies can be replaced for effective-date or tier behavior.
- AED precision 2 and BHD precision 3, giving minor units 0.01 and 0.001 and scaling factors 100 and 1000. Half a minor unit cannot be stored under the specified precision. `Currency` is configurable; `10^precision` is computed rather than independently configured.
- The complete fixture is in `AssessmentScenario.Events`: 1200, 950, hold 200, credit 400, settlement 185, unknown settlement 180, backdated debit 620, hold 90, reversal of that debit, and BHD 10 in 3 instalments. Their dates, account IDs, event IDs, authorization IDs and arrival positions are supplied test data, not tunable product defaults. Halving any of these changes the requested experiment.
- A reversal derives 620 from the original posting rather than trusting a second caller-supplied amount. It is +620 against a -620 debit.

## Chosen numerical conventions

- Zero is the affordability/interest/overdraft boundary: available may equal zero, fees need a strictly negative close, interest needs a strictly positive basis. Moving this boundary changes an explicit rule.
- Midpoints round `AwayFromZero`, consistently at ingestion and daily accrual. This is a disclosed convention, not a task-mandated rounding mode. A half-cent/half-fils is resolved deterministically rather than lost or treated differently by sign.
- Instalments use integer minor units. 10000 BHD fils / 3 gives quotient 3333 and remainder 1. The first part receives that one extra unit: `3.334 + 3.333 + 3.333 = 10.000`. Giving every part 3.334 creates 0.002. Halving the remainder loses 0.0005, which is not representable.
- Daily amounts are rounded first. Capitalization is the exact sum of those stored daily amounts; there is no independently rounded aggregate and no balancing tolerance. Zero-valued accruals are retained in the final accrual report; zero-valued capitalization credits are omitted.
- Sequence starts at zero and is incremented before appending: first sequence 1. It is an audit order, not money or an arbitrary settlement priority. Single-writer only; not a globally distributed ID.
- `Instalments=1` is the ordinary posting default; zero or negative counts are invalid. Very small positive totals may produce zero-valued parts when split into more parts than minor units; those parts are retained to preserve the requested count and exact sum.
- Currency precision is validated against the .NET decimal rounding API's supported range 0-28. Resource and arithmetic limits of decimal/arrays are not advertised as banking limits.
- .NET target 10 and SDK minimum feature band 10.0.100 use the available runtime; `latestFeature` permits a newer installed .NET 10 SDK. This is tooling configuration, not a financial constant.

## Independent fee calculation

Before E7, principal closes for Days 1-5 are `250, 250, 650, 465, 465`; holds are not postings. Rejecting E6 means its 180 never leaves.

E7 changes the principal-only closes to `250, -370, 30, -155, -155`.

Assess chronologically, carrying earlier fees into later balances:

1. Day 1: 250, no fee.
2. Day 2: -370, charge 25 -> -395.
3. Day 3: 30 - 25 = 5, no fee.
4. Day 4: -155 - 25 = -180, charge 25 -> -205.
5. Day 5: -155 - 25 - 25 = -205, charge 25 -> -230.

Days 2 and 4 are already closed when E7 arrives on Day 5, so those two assessments append immediately. Day 5 closes immediately before E9 advances the clock to Day 6: its third assessment appends then, before the reversal. E8 therefore sees -205 available, not -230; it is rejected either way. No decision depends on pretending E8 sees final restated balances.

E9 contributes +620 from Day 2 onward. Existing 75 of fees remain. Before capitalization, final AED closes are `250, 225, 625, 415, 390, 390`. Day 6 is positive after its input and needs no new fee.

## Independent interest calculation

Use the final restated, fee-inclusive close before capitalization, for each day:

```text
AED day  basis     raw interest  rounded
1        250.00    0.10000       0.10
2        225.00    0.09000       0.09
3        625.00    0.25000       0.25
4        415.00    0.16600       0.17
5        390.00    0.15600       0.16
6        390.00    0.15600       0.16
Total                          0.93
```

Sum of raw amounts is 0.918, but rounding that sum to 0.92 would not equal the required sum of rounded daily amounts. We capitalize 0.93, not 0.92. Final AED balance: 390 + 0.93 = 390.93.

BHD basis is zero on Days 1-4, then 10.000 on Days 5-6. Each positive day earns `10 × 0.0004 = 0.004`; capitalization is 0.008 and final BHD is 10.008. E10 is received before the final input watermark despite having an earlier booked day.

## Verification-only constants

The allocator property test checks 50 minor-unit totals × 7 part counts × 2 currencies = 700 combinations. The range includes amounts below the part count and non-divisible totals; 25 or 100 totals would change test breadth, not financial behavior. Separate tests exercise the supplied larger amount.

Other fixtures are deliberate boundary probes: 100 funds / 100 hold reaches exactly zero; an extra 0.01 tests the next representable AED amount. 201 exceeds the real 200 hold by 1; 185 and residual 15 test terminal partial settlement. A 500 backdated debit forces insufficient funds after prior approval. A 30 fee proves configuration differs from the specified 25; two days starting at -1 produce -61. A 0.001 rate on 1000 gives exactly 1 and proves rate injection. Midpoints 1.005 and 1.0005 test the two currency scales; 0.001 AED rounds to zero and must be rejected. Counts and assertion totals are test evidence, not hidden business policy.

The xUnit migration preserves 52 passing regression cases (not 52 individual assertion calls; loops and multi-assertion cases remain intact), including all 700 allocator combinations. The same project also contains the required real failing expiry fact: expected active holds 0, actual 1. Thus the default suite executes 53 tests: 52 passed, one failed, zero skipped. Test package versions (Microsoft.NET.Test.Sdk 18.0.1, xUnit 2.9.3, VSTest adapter 3.1.5) are pinned tooling choices, not financial constants. No monetary values or tolerances changed.
