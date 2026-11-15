# In-memory account ledger

A small C#/.NET 10 ledger with Clean Architecture boundaries, immutable postings, immutable authorization transitions, value-dated queries, configurable financial policies, and an exact arrival-order replay. No HTTP service, database, persistence adapter, or UI. Core state exists only in memory.

Public submission: [GitHub repository](https://github.com/YoussefKhaled-92/in-memory-account-ledger). Anonymous access was verified after publication; the original six implementation/review commits were pushed intact. [GitHub verification](https://github.com/YoussefKhaled-92/in-memory-account-ledger/actions) builds and runs both the regression suite and the documented known-failure probe.

## Run

Install the .NET 10 SDK. Restore uses nuget.org for Microsoft's DI package and test-only Microsoft.NET.Test.Sdk 18.0.1, xUnit 2.9.3 and Visual Studio runner 3.1.5 (plus transitive dependencies). From the repository root:

```sh
dotnet build AccountLedger.slnx -c Release -m:1 -p:UseSharedCompilation=false
dotnet test -c Release
dotnet run --project src/Ledger.Replay -c Release
```

The conventional xUnit project exposes all 52 existing regression cases as individually discovered facts. Bare `dotnet test` uses `AccountLedger.slnx` and runs only this passing suite. The tests cover all eight acceptance criteria, complete history, state immutability, monetary conservation, retries, validation, policy configuration, and authorization/reversal boundaries. The allocator property check still explores 700 combinations. Numeric formatting uses invariant culture. VSTest supports discovery, filtering and TRX results; for example, add `--logger "trx;LogFileName=regression.trx"`.

The assessment also requires a genuinely failing test against the design:

```sh
dotnet test tests/Ledger.KnownFailures -c Release --filter Category=KnownFailure
```

This explicitly runs the separate `Ledger.KnownFailures` project: one inline-annotated xUnit fact asserting that an abandoned authorization should expire. The project is intentionally outside the default solution; `tests/AccountLedger.KnownFailures.slnx` builds it with its dependencies. Its `Category=KnownFailure` trait also supports filtering. It **fails, with exit code 1**: this model has no automatic expiry. It is never counted as a passing test or hidden behind a skip. Both the opt-in command and its actual output are checked by CI. CI checks both exit 1 and the TRX result: exactly one failed expiry assertion (expected 0 active holds, actual 1), with no skips. See `tests/Ledger.KnownFailures/AuthorizationExpiryTests.cs` and REJECTED.md.

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
- `src/Ledger.Core`: small `AccountLedger` compatibility facade and Microsoft DI composition root; business workflows are in Application.
- `src/Ledger.Replay`: specified scenario and console reporting; no calculations duplicated in the printer. Tests link the scenario fixture as source.
- `tests/Ledger.Tests`: 52 passing xUnit facts, original regression assertions, pre-refactor complete-history fixture and extension/composition tests.
- `tests/Ledger.KnownFailures`: separate opt-in, genuinely failing expiry fact; excluded from the standard solution.

Read [the architecture and extension guide](docs/CLEAN-ARCHITECTURE.md) for the dependency diagram, business boundaries, registration example and migration tradeoffs. New operation kinds register an `IEventHandler`; dispatch has no switch. `EventKind` is an open, string-backed value type, while the existing operation names stay unchanged. Domain types moved to `Ledger.Domain`; numeric enum casts/serialization and binary compatibility are not preserved.

For browsing, Domain types are grouped into `Entities`, `ValueObjects`, `Enums`, `Records`, `Interfaces`, `Policies`, `Rules`, `Services` and `Exceptions`. Application groups use cases under `Postings`, `Authorizations`, `Products` and `Workflow`, with contracts in `Interfaces`. The journal is in `Journals`, the facade in `Facades`, and scenario data in `Scenarios`. Imports are explicit in each file; project-wide global and implicit imports are disabled. `.editorconfig` and CI keep braces and formatting consistent.

Public collection queries return read-only snapshots. Records carry global sequence numbers so postings, input outcomes, and lifecycle changes can be correlated without rewriting history. The result sequence is an outcome marker, not an instruction to order business inputs by date.

Each `AccountLedger` owns a private DI provider. Journal, session, queries, handlers and workflows are singletons **within that ledger only**; separate ledgers never share internal mutable state. Constructor injection remains in Application, which has no DI package reference. Caller-supplied policy and additional handler instances retain their configuration and remain caller-owned; supply separate instances for stateful clients. Prefer `using var ledger = new AccountLedger(...)` to release the container. Disposal is idempotent and does not call `Finish()`; new processing/finalization then throws `ObjectDisposedException`, while existing in-memory reads remain available. This adds no concurrency or transaction guarantee.

`Release` is a small explicit lifecycle extension used by tests; no release is injected into the supplied scenario. It exists to distinguish cancellation from settlement and makes the production lifecycle discussion concrete.

## Decisions and submission

Read AMBIGUITIES.md before comparing numbers: retained fees and restated interest are disclosed product choices, not universal banking rules. NUMBERS.md proves the arithmetic and explains configuration. REJECTED.md refuses criteria 2, 6, 7 and 8 and qualifies criterion 1's before-fees view. REQUIREMENTS.md maps requirements to evidence. WORKLOG.md records actual work; Git commits preserve real milestones without squashing or backdating. AI assistance is disclosed.

Part 2 is `docs/architecture-and-tradeoffs.pdf`, with editable text in `docs/ARCHITECTURE.md`. It covers the four requested sections, without reproducing the input stream or rule text.

This is intentionally a single-writer assessment model. No concurrency guarantee, durable recovery, automatic authorization expiry, automatic fee refunds, effective-dated policy catalog, FX conversion, or complete double-entry general ledger is claimed. Unsupported BHD overdraft charging fails explicitly rather than inventing a conversion. Decimal overflow/resource exhaustion and failures of a custom policy are fatal configuration/runtime faults, not ordinary customer declines. Production transaction boundaries and recovery are discussed in Part 2.

## Verification evidence

Commands, timestamps, byte comparison and scope checks for the xUnit migration are in [verification](docs/xunit-verification.md). Actual captured build, regression and opt-in failure outputs are in `docs/build-output.txt`, `docs/test-output.txt` and `docs/known-failure-output.txt`. CI exports fresh TRX artifacts and compares the complete replay byte-for-byte with the unchanged baseline. The migration changes test infrastructure and documentation only; production source and financial decisions remain unchanged.
