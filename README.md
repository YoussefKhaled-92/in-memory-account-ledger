# In-memory account ledger

A small C#/.NET 10 ledger with Clean Architecture boundaries, immutable postings, immutable authorization transitions, value-dated queries, configurable financial policies, and an exact arrival-order replay. No HTTP service, database, persistence adapter, or UI. Core state exists only in memory.

Public submission: [GitHub repository](https://github.com/YoussefKhaled-92/in-memory-account-ledger). Anonymous access was verified after publication; the original six implementation/review commits were pushed intact. [GitHub verification](https://github.com/YoussefKhaled-92/in-memory-account-ledger/actions) builds and runs both the regression suite and the documented known-failure probe.

## Run

Install the .NET 10 SDK. No third-party packages are needed. From the repository root:

```sh
dotnet build AccountLedger.slnx -c Release -m:1 -p:UseSharedCompilation=false
dotnet run --project tests/Ledger.Tests -c Release
dotnet run --project src/Ledger.Replay -c Release
```

The test project is a dependency-free executable test harness, **not a VSTest/xUnit project**. Use `dotnet run`, not `dotnet test`. A failed assertion produces a nonzero exit code. The test runner covers all eight acceptance criteria, state immutability, monetary conservation, retries, validation, policy configuration, and authorization/reversal boundaries. Its allocator property check explores 700 combinations.

The assessment also requires a genuinely failing test against the design:

```sh
dotnet run --project tests/Ledger.Tests -c Release -- --known-failure
```

This runs the passing suite plus one inline-annotated assertion that an abandoned authorization should expire. It **fails, with exit code 1**: this model has no automatic expiry. It is never counted as a passing test or hidden behind a skip. Both the opt-in command and its actual output are checked by CI. See `tests/Ledger.Tests/Program.cs` and REJECTED.md.

## Read the output

The first block prints E1 through E10 in the supplied order, with both supplied booked day and monotonic recorded day. E10 remains last: booked on Day 5, received on recorded Day 6. Errors do not post money. The E8 checkpoint deliberately precedes the Day 5 close.

The daily report is the **final restated view**, after all inputs and finalization. Each account/day prints closing ledger balance, interest basis/accrual, fee count/amount and the recorded assessment day. Authorization states are the decisions actually known by that recorded day; they are not retroactively reapproved using final balances. Errors are grouped by recorded day. Thus a fee can appear against a now-positive restated historical close: it was validly assessed against an earlier negative view and retained under our explicit policy.

Day 6 closing includes capitalization; its interest basis excludes that credit to avoid self-interest. The last block reconciles capitalization and prints the three BHD parts. Example output is in `docs/replay-output.txt`; regenerate it with the replay command above.

Expected final AED closes: `250.00, 225.00, 625.00, 415.00, 390.00, 390.93`.
Expected final BHD closes: `0.000, 0.000, 0.000, 0.000, 10.000, 10.008`.
Assessed fees: AED 25 on Days 2, 4 and 5. Errors: E6 unknown authorization and E8 insufficient available funds. Auth-A is Settled; Auth-B is Rejected.

## Design map

- `src/Ledger.Domain`: individual business types, reservation/reversal invariants, currency precision and financial policies.
- `src/Ledger.Application`: distinct operation handlers, immutable handler registry, arrival-order orchestration, daily product use cases, scan-based projections and the journal port.
- `src/Ledger.Infrastructure.InMemory`: private append-only histories, shared sequence allocation and detached read-only snapshots.
- `src/Ledger.Core`: small `AccountLedger` compatibility facade and default composition root; business workflows are in Application.
- `src/Ledger.Replay`: specified scenario and console reporting; no calculations duplicated in the printer. Tests link the scenario fixture as source.
- `tests/Ledger.Tests`: original assertions, pre-refactor complete-history fixture, extension/workflow tests and the explicit failing design probe.

Read [the architecture and extension guide](docs/CLEAN-ARCHITECTURE.md) for the dependency diagram, business boundaries, registration example and migration tradeoffs. New operation kinds register an `IEventHandler`; dispatch has no switch. `EventKind` is an open, string-backed value type, while the existing operation names stay unchanged. Domain types moved to `Ledger.Domain`; numeric enum casts/serialization and binary compatibility are not preserved.

Public collection queries return read-only snapshots. Records carry global sequence numbers so postings, input outcomes, and lifecycle changes can be correlated without rewriting history. The result sequence is an outcome marker, not an instruction to order business inputs by date.

`Release` is a small explicit lifecycle extension used by tests; no release is injected into the supplied scenario. It exists to distinguish cancellation from settlement and makes the production lifecycle discussion concrete.

## Decisions and submission

Read AMBIGUITIES.md before comparing numbers: retained fees and restated interest are disclosed product choices, not universal banking rules. NUMBERS.md proves the arithmetic and explains configuration. REJECTED.md refuses criteria 2, 6, 7 and 8 and qualifies criterion 1's before-fees view. REQUIREMENTS.md maps requirements to evidence. WORKLOG.md records actual work; Git commits preserve real milestones without squashing or backdating. AI assistance is disclosed.

Part 2 is `docs/architecture-and-tradeoffs.pdf`, with editable text in `docs/ARCHITECTURE.md`. It covers the four requested sections, without reproducing the input stream or rule text.

This is intentionally a single-writer assessment model. No concurrency guarantee, durable recovery, automatic authorization expiry, automatic fee refunds, effective-dated policy catalog, FX conversion, or complete double-entry general ledger is claimed. Unsupported BHD overdraft charging fails explicitly rather than inventing a conversion. Decimal overflow/resource exhaustion and failures of a custom policy are fatal configuration/runtime faults, not ordinary customer declines. Production transaction boundaries and recovery are discussed in Part 2.
