# Requirement coverage

- In-memory core only: `src/Ledger.Core`; no web, database, file storage or UI dependencies. Console output is the mandated runner.
- Six-day window and exact accounts/openings: `AssessmentScenario.Create`, constructor/date validation tests.
- Exact E1-E10 arrival order: `AssessmentScenario.Events`, replay-order test including E10 recorded on Day 6.
- Value-date balances: `AccountLedger.Balance`; pre-reversal checkpoint and independent final daily assertion.
- Daily overdraft and once-per-account/day: `AssessFeesThrough`, chronological cascading calculation, duplicate/idempotent tests and configuration test.
- Daily positive interest, exact capitalization: `IInterestPolicy`, `Finish`, daily expected values and per-account reconciliation tests.
- AED/BHD precision and three instalments: `Currency.Round`, `InstalmentAllocator.Split`, midpoint and 700-case conservation tests.
- Append-only records: immutable records, private lists, sequence IDs; prefix preservation test and read-only snapshot test.
- Approval funds constraint and holds separated from money: `Authorize`, `Available`, exact-zero/extra-unit tests.
- Settlement validation: `RequireActive`, `Settle`; unknown, excess, duplicate, wrong-account and terminal tests.
- Reversal: `Reverse`; full original source compensation, original-date constraint, no duplicate reversals, preserved prior journal.
- Per-day balances, fees, authorization states and errors: `Ledger.Replay/Program.cs`, captured `docs/replay-output.txt`.
- Incorrect criteria: all eight individually adjudicated in REJECTED.md and tested. Refused: 2, 6, 7, 8; criterion 1 qualified as principal-only.
- Real intact Git history and timestamped work: actual repository commits plus WORKLOG.md; no squashing, backdating or fake failures.
- Required files: README.md, NUMBERS.md, AMBIGUITIES.md, REJECTED.md, WORKLOG.md, this map.
- One inline-annotated failing test against own model: executable `--known-failure`, actual nonzero result, preserved output.
- Part 2: four required sections in `docs/ARCHITECTURE.md` and rendered `docs/architecture-and-tradeoffs.pdf`; 2-4 pages and below 25 MB. No replay or rule restatement.
- Public GitHub link: publication requires the owner's confirmation after local review. Do not treat the proposed URL as a published repository until remote and signed-out access are verified.
