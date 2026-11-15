# xUnit migration verification

Verified locally at 2026-11-15T08:20:37+02:00 (Africa/Cairo), starting from main `efe3bdd`. Commands ran against the final test implementation. All pre-existing commits remain intact; publication adds a normal descendant commit.

- Release build: zero warnings/errors. Captured in `build-output.txt`.
- Bare `dotnet test -c Release --no-build`: 52 executed, 52 passed, zero failed/skipped. TRX names match all 52 original PASS labels one-for-one. Captured in `test-output.txt`.
- Explicit known-failure project: one executed, one failed, zero passed/skipped, exit 1. TRX verifies test identity and actual xUnit equality failure: expected 0, actual 1 active authorization. Captured in `known-failure-output.txt`; its successful build in `known-failure-build-output.txt`.
- Both solutions pass formatter verification for braces and explicit imports.
- Full E1-E10 replay matches the pre-migration executable baseline byte-for-byte on Windows. SHA-256: `aa8aa9fcdcb749dfac34d11c136d78070bc26986eb28c37559cadbe0851fcd56` (3729 bytes). The unchanged committed LF baseline has SHA-256 `4154cf7c0485611bb1f1a332c2aa12fd538adbfe6306b6479b17fb18dd5022ea` (3666 bytes). Windows console output uses CRLF; only this platform newline difference exists against the stored LF file. Linux CI uses `cmp` with no normalization against the committed baseline.
- `git diff --name-only -- src tests/Ledger.Tests/Fixtures` is empty. Domain, Application, Infrastructure, Core, Replay and both complete-history fixtures remain unchanged. All numeric expectations and 700 allocator combinations remain. Assertions use equivalent xUnit forms (True/False, Empty/Single and Equal); original extension/composition exception helpers retain derived-exception acceptance via ThrowsAny.
- Architecture PDF regenerated: four pages; all four rendered pages visually inspected, with no clipping or overlap.

## Reproduce

```sh
dotnet build AccountLedger.slnx -c Release -m:1 -p:UseSharedCompilation=false
dotnet test -c Release --no-build --logger "console;verbosity=normal" --logger "trx;LogFileName=regression.trx"
python scripts/verify-regression.py tests/Ledger.Tests/TestResults/regression.trx
dotnet build tests/AccountLedger.KnownFailures.slnx -c Release -m:1 -p:UseSharedCompilation=false
dotnet test tests/Ledger.KnownFailures -c Release --no-build --filter Category=KnownFailure --logger "trx;LogFileName=known-failure.trx" --results-directory TestResults/known-failure
# Previous command MUST exit 1. Then validate the real assertion:
python scripts/verify-known-failure.py TestResults/known-failure/known-failure.trx
dotnet format AccountLedger.slnx --no-restore --verify-no-changes --diagnostics IDE0011 IDE0005
dotnet format tests/AccountLedger.KnownFailures.slnx --no-restore --verify-no-changes --diagnostics IDE0011 IDE0005
dotnet run --project src/Ledger.Replay -c Release --no-build > replay-output.txt
# Linux/macOS: byte-for-byte comparison
cmp docs/replay-output.txt replay-output.txt
```

CI runs the same build/test/format/replay/failure checks and uploads TRX and replay outputs. Remote CI status is reported after publication; this local capture makes no advance claim about a remote run.
