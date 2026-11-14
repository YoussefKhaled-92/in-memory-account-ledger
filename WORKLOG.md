# Worklog

Times use Africa/Cairo (UTC+02:00). Entries describe actual work, not reconstructed working sessions. AI assistance is used for implementation and documentation.

- 2026-11-14 13:07 +02:00 - Received the complete assessment text and screenshots. Confirmed the PDF limit is 2-4 pages and 25 MB. Inspected installed .NET 10 SDK and existing Git identity. No repository has been published.
- 2026-11-14 13:10 +02:00 - Initialized the local repository and separated core, replay, and executable tests. Selected a dependency-free test harness so the suite needs no third-party NuGet packages. Chose retained assessed fees, monotonic processing time, and restated interest; these require explicit documentation and tests.
- 2026-11-14 13:11 +02:00 - Built the immutable monetary/lifecycle journal, configurable fee and interest policies, minor-unit allocation, and exact supplied event fixture. Core build passed with zero warnings.
- 2026-11-14 13:15 +02:00 - Ran the initial 37-test suite successfully and the complete console replay. Independently checked the three fee days and final accrual amounts. Ran the annotated expiry test: 37 passed, one expected failure, exit 1. During review, replaced per-account immediate capitalization with preparation of all accruals before any capitalization, and reserved system source-ID prefixes.
