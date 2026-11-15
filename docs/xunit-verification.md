# Unified xUnit suite verification

Updated at 2026-11-15T09:03:10+02:00 (Africa/Cairo), based on `3dcef20`. The owner requested the assessment's straightforward arrangement: one test project with 53 tests, 52 passing regressions and one genuinely failing design test. Removed the extra project/solution, trait, opt-in commands and separate build/output captures. All earlier commits remain intact.

## Run

```sh
dotnet build AccountLedger.slnx -c Release -m:1 -p:UseSharedCompilation=false
dotnet test -c Release --no-build --logger "trx;LogFileName=tests.trx"
# Expected: exit 1, 52 passed, one failed, zero skipped.
python scripts/verify-test-results.py tests/Ledger.Tests/TestResults/tests.trx
dotnet run --project src/Ledger.Replay -c Release --no-build > replay-output.txt
# Linux/macOS: compare the entire output byte-for-byte
cmp docs/replay-output.txt replay-output.txt
dotnet format AccountLedger.slnx --no-restore --verify-no-changes --diagnostics IDE0011 IDE0005
```

The failing fact is `AuthorizationExpiryTests.Abandoned_authorization_should_eventually_expire`, inline-annotated in `tests/Ledger.Tests/Suites/AuthorizationExpiryTests.cs`. Expected active holds: 0; actual: 1. It runs normally with every other test; no Skip, filter, separate project, or expected-failure assertion wrapper. The full test run is intentionally reported as failed. CI checks exit 1, all 53 executed, exactly 52 passed, exactly this one equality failure, and zero skips; any different outcome fails verification.

## Evidence

- Local Release build passed with zero warnings/errors; actual output: `build-output.txt`.
- Production source and complete-history fixtures unchanged from the preceding commit.
- Full local replay matches the pre-migration executable baseline byte-for-byte: SHA-256 `aa8aa9fcdcb749dfac34d11c136d78070bc26986eb28c37559cadbe0851fcd56` (3729 bytes). The unchanged committed baseline uses LF; Windows executable uses CRLF. CI uses Linux `cmp` without normalization.
- PDF regenerated as four pages; every rendered page visually inspected.
- Local test-host communication and formatter named-pipe access were blocked by environment permissions. Those attempts are not counted as successful test/format verification. GitHub Actions runs both checks and exports complete TRX and console output; actual remote results are recorded after publication.
