---
Title: PrIMA Stage 4 — Historical Reliability Analytics, Reporting & v1.0 Quality Gate Review Log
Code: PRIMA-STAGE-4
Artifact: REVIEW
ReviewIteration: 1
LastUpdated: 2026-11-22
---

# Review Summary

This document preserves formal `NO-GO` review findings, verifiable evidence, required corrections, resolution status, and remediation history for Implementation Plan `docs/issues/IS004-PRIMA-STAGE-4-IMPLEMENTATION-PLAN.md` (`Code: PRIMA-STAGE-4`).

| Slice ID | Current Decision | ReviewIteration | Open BLOCKER | Open MAJOR | Open MINOR |
|---|---|---|---|---|---|
| `P2-S03` | `GO` | `1` | `0` | `0` | `0` |

---

# Slice P2-S03 — Historical Issue Ledger Endpoint (`Features/History/GetHistoricalIssues`)

- **Review Status**: `GO`
- **ReviewIteration**: `1`
- **Implementation Plan**: `docs/issues/IS004-PRIMA-STAGE-4-IMPLEMENTATION-PLAN.md`
- **Target Architecture**: `docs/issues/IS004-PRIMA-STAGE-4-ARCHITECTURE.md` (`DEC-003`, `DEC-004`, `DEC-007`)

## Findings

| ID | Severity | Resolution Status | Summary |
|---|---|---|---|
| `F-P2S03-01` | `MAJOR` | `FIXED` | `GetHistoricalIssuesHandler` omits `DbType.DateTime2` on `@FromUtc`/`@ToUtc` Dapper parameters, causing SQL Server `DATETIME` `.999` millisecond rounding to include next-day midnight WIB records |
| `F-P2S03-02` | `MINOR` | `FIXED` | Duplicated `NormalizeToUtc` helper and missing synchronous `IDbConnection` open fallback |

### `F-P2S03-01` — Missing `DbType.DateTime2` on `@FromUtc` and `@ToUtc` Dapper Parameters (`MAJOR`)

- **ID**: `F-P2S03-01`
- **Severity**: `MAJOR`
- **Resolution Status**: `FIXED`
- **Description**:
  `GetHistoricalIssuesHandler.HandleAsync` adds `@FromUtc` and `@ToUtc` to `DynamicParameters` without specifying `DbType.DateTime2`. In Dapper (`2.1.35`) with `Microsoft.Data.SqlClient` (`5.2.2`), omitting `DbType` for `System.DateTime` causes `SqlParameter` to infer `SqlDbType.DateTime` (`DATETIME`), which has 1/300th-second precision and rounds `.999` milliseconds up to `.000` of the next second. Because `getWibDateRange` (`src/frontend/src/shared/utils/dateUtils.ts`, `DEC-003`) computes every preset's `toUtc` boundary at `23:59:59.999 WIB` (e.g., `2026-11-22T16:59:59.999Z`), SQL Server receives `@ToUtc` as `2026-11-22 17:00:00.000` (`DATETIME`) and evaluates `DATETIME2` column values at `00:00:00.000 WIB` of the next day (`2026-11-22T17:00:00.000Z`) as `<= @ToUtc`. This leaks next-day midnight WIB issues into `Today` / `This Week` / `This Month` / `Last Month` / `Custom` historical ledger queries, violates `DEC-003` / `RISK-002` boundary accuracy, and causes count/row divergence against `GetReliabilityKpiHandler` (`P2-S02`), which explicitly binds `DbType.DateTime2`.
- **Evidence**:
  1. `src/backend/PrIMA.Api/Features/History/GetHistoricalIssues/GetHistoricalIssuesHandler.cs` lines 64–66:
     `parameters.Add("FromUtc", fromUtc);` and `parameters.Add("ToUtc", toUtc);` (no `DbType.DateTime2`).
  2. `src/frontend/src/shared/utils/dateUtils.ts` line 85 (`wibEndOfDayUtc`): returns `23:59:59.999` WIB converted to UTC ISO (`...T16:59:59.999Z`).
  3. `src/backend/PrIMA.Api/Migrations/Scripts/001_InitialSchema.sql` lines 52–57: all `dbo.Issues` timestamp columns (`CreatedAtUtc`, `LastOccurrenceAtUtc`, `AcknowledgedAtUtc`, `ResolvedAtUtc`, `DismissedAtUtc`, `ReopenedAtUtc`) are `DATETIME2`.
  4. `src/backend/PrIMA.Api/Features/History/GetReliabilityKpi/GetReliabilityKpiHandler.cs` lines 91–92: explicitly binds `DbType.DateTime2` for `FromUtc` and `ToUtc`.
  5. Direct SQL Server verification against `PrIMA_IntegrationTests` using `Dapper 2.1.35` and `Microsoft.Data.SqlClient 5.2.2`:
     - `DynamicParameters.Add("ToUtc", new DateTime(2026, 11, 22, 16, 59, 59, 999, DateTimeKind.Utc))` transmitted `SQL BaseType=datetime` with value `2026-11-22T17:00:00.0000000`, and `CAST('2026-11-22T17:00:00.000' AS DATETIME2) <= @ToUtc` evaluated to `1`.
     - `DynamicParameters.Add("ToUtc", new DateTime(2026, 11, 22, 16, 59, 59, 999, DateTimeKind.Utc), DbType.DateTime2)` transmitted `SQL BaseType=datetime2` with value `2026-11-22T16:59:59.9990000`, and `CAST('2026-11-22T17:00:00.000' AS DATETIME2) <= @ToUtc` evaluated to `0`.
  6. `src/backend/PrIMA.Api.Tests/Integration/HistoricalIssuesEndpointTests.cs`: existing tests did not cover `.999Z` vs `.999Z + 1ms` (`17:00:00.000Z`) boundary exclusion, and `SeedIssueAsync` (lines 77–98) also omits `DbType.DateTime2`.
- **Required Correction**:
  1. Update `src/backend/PrIMA.Api/Features/History/GetHistoricalIssues/GetHistoricalIssuesHandler.cs` to bind `FromUtc` and `ToUtc` explicitly with `DbType.DateTime2`:
     - `parameters.Add("FromUtc", fromUtc, DbType.DateTime2);`
     - `parameters.Add("ToUtc", toUtc, DbType.DateTime2);`
  2. Update `SeedIssueAsync` in `src/backend/PrIMA.Api.Tests/Integration/HistoricalIssuesEndpointTests.cs` to pass `DbType.DateTime2` for all seeded `DateTime` columns, and add integration test verification confirming millisecond boundary precision on WIB boundaries (issues at exact `fromUtc` and `toUtc` ending in `.999Z` are included, while issues at `fromUtc.AddMilliseconds(-1)` and `toUtc.AddMilliseconds(1)` — `00:00:00.000` next day WIB — are excluded).
  3. Re-run `dotnet build src/backend/PrIMA.Api/PrIMA.Api.csproj` and `dotnet test --filter FullyQualifiedName~HistoricalIssuesEndpointTests` cleanly.
- **Remediation Evidence (Iteration 1)**:
  1. `src/backend/PrIMA.Api/Features/History/GetHistoricalIssues/GetHistoricalIssuesHandler.cs` lines 65–66 now explicitly bind `parameters.Add("FromUtc", fromUtc, DbType.DateTime2);` and `parameters.Add("ToUtc", toUtc, DbType.DateTime2);`.
  2. `src/backend/PrIMA.Api.Tests/Integration/HistoricalIssuesEndpointTests.cs` lines 93–98 now bind all 6 seeded `DateTime` columns (`CreatedAtUtc`, `LastOccurrenceAtUtc`, `AcknowledgedAtUtc`, `ResolvedAtUtc`, `DismissedAtUtc`, `ReopenedAtUtc`) with `DbType.DateTime2`.
  3. `GetHistoricalIssues_WibMillisecondBoundaryPrecision_IncludesExact999ZAndExcludesNextDayMidnightWib` (lines 289–367) verifies exact `fromUtc` / `.999Z` (`toUtc`) inclusion and `fromUtc.AddMilliseconds(-1)` / `toUtc.AddMilliseconds(1)` (`00:00:00.000` next day WIB) exclusion across `All`, `Resolved`, `Dismissed`, and `Reopened`, passing in live SQL Server integration execution (`9/9` passed).

### `F-P2S03-02` — Duplicated `NormalizeToUtc` Helper and Non-`DbConnection` Open Fallback (`MINOR`)

- **ID**: `F-P2S03-02`
- **Severity**: `MINOR`
- **Resolution Status**: `FIXED`
- **Description**:
  `NormalizeToUtc(DateTime)` is duplicated identically in `GetHistoricalIssuesValidator.cs` (lines 14–20) and `GetHistoricalIssuesHandler.cs` (lines 153–159). Additionally, `GetHistoricalIssuesHandler.cs` lines 120–123 only call `OpenAsync` when `connection is DbConnection`, omitting the synchronous `else if (connection.State != ConnectionState.Open) { connection.Open(); }` fallback used in `GetReliabilityKpiHandler.cs` and `GetDashboardFeedHandler.cs`.
- **Evidence**:
  - `src/backend/PrIMA.Api/Features/History/GetHistoricalIssues/GetHistoricalIssuesValidator.cs` lines 14–20.
  - `src/backend/PrIMA.Api/Features/History/GetHistoricalIssues/GetHistoricalIssuesHandler.cs` lines 120–123, 153–159.
- **Required Correction**:
  Non-blocking (recommended during remediation of `F-P2S03-01`): consolidate `NormalizeToUtc` into a single `internal static` helper (e.g., on `GetHistoricalIssuesValidator`) and add `else if (connection.State != ConnectionState.Open) { connection.Open(); }`.
- **Remediation Evidence (Iteration 1)**:
  - `src/backend/PrIMA.Api/Features/History/GetHistoricalIssues/GetHistoricalIssuesValidator.cs` lines 14–20 expose `internal static DateTime NormalizeToUtc(DateTime value)`, and `GetHistoricalIssuesHandler.cs` lines 32–33 invoke `GetHistoricalIssuesValidator.NormalizeToUtc(...)` with the duplicate helper removed.
  - `src/backend/PrIMA.Api/Features/History/GetHistoricalIssues/GetHistoricalIssuesHandler.cs` lines 124–127 include `else if (connection.State != ConnectionState.Open) { connection.Open(); }`.

---

## Re-Review History

### Iteration 0 — Initial Review (`NO-GO`)

- **Date**: 2026-11-22
- **Reviewer Decision**: `NO-GO`
- **Verification Executed**:
  - `dotnet build src/backend/PrIMA.Api/PrIMA.Api.csproj --no-restore`: passed (0 warnings, 0 errors).
  - `dotnet test src/backend/PrIMA.Api.Tests/PrIMA.Api.Tests.csproj --filter FullyQualifiedName~HistoricalIssuesEndpointTests`: passed (8/8 passed, 0 skipped, 0 failed).
  - Read-only empirical SQL Server + Dapper `DynamicParameters` verification confirmed `DATETIME` `.999` rounding bug on `toUtc`.
- **Summary**: Endpoint routing, DI coexistence with `P2-S02`, FluentValidation `422` RFC 7807 response, JWT authorization, limit clamping (`DEC-007`), status-aware SQL predicates (`DEC-004`), escaped `LIKE` search, and all 18 DTO columns conform to spec, **except** `GetHistoricalIssuesHandler` omits `DbType.DateTime2` on `@FromUtc`/`@ToUtc` (`F-P2S03-01`, `MAJOR`), causing `.999Z` WIB end-of-day queries to round up to next-day midnight UTC and leak next-day records.

### Iteration 1 — Re-Review (`GO`)

- **Date**: 2026-11-22
- **Reviewer Decision**: `GO`
- **Verification Executed**:
  - `dotnet build src/backend/PrIMA.Api/PrIMA.Api.csproj --no-restore`: passed (`0 Warning(s)`, `0 Error(s)`).
  - `dotnet test src/backend/PrIMA.Api.Tests/PrIMA.Api.Tests.csproj --filter FullyQualifiedName~HistoricalIssuesEndpointTests --verbosity normal`: passed (`9/9` passed, `0` failed, `0` skipped, including `GetHistoricalIssues_WibMillisecondBoundaryPrecision_IncludesExact999ZAndExcludesNextDayMidnightWib`).
  - Code inspection verified `GetHistoricalIssuesHandler.cs`, `GetHistoricalIssuesValidator.cs`, `GetHistoricalIssuesQuery.cs`, `GetHistoricalIssuesResponse.cs`, `HistoricalIssueItemDto.cs`, `GetHistoricalIssuesEndpoint.cs`, `HistoryEndpoints.cs`, `Program.cs`, and `HistoricalIssuesEndpointTests.cs`.
- **Findings Resolved**: `F-P2S03-01` (`MAJOR` -> `FIXED`), `F-P2S03-02` (`MINOR` -> `FIXED`).
- **Summary**: Both prior findings are completely remediated with zero regressions. Slice `P2-S03` satisfies all completion criteria, `P1-S01` dependency (`IMPLEMENTED`/`GO`), and architectural decisions (`DEC-003`, `DEC-004`, `DEC-007`), and is granted `GO`.
