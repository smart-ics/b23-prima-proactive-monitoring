---
Title: PrIMA Stage 4 — Historical Reliability Analytics, Reporting & v1.0 Quality Gate Implementation Plan
Code: PRIMA-STAGE-4
Artifact: IMPLEMENTATION-PLAN
Version: 1.0
LastUpdated: 2026-11-22
Status: COMPLETED
Execution Approval: APPROVED
---

# 1. Objective

Implement Stage 4 ("Historical Reliability Analytics, Reporting & v1.0 Quality Gate") of the PrIMA Development Roadmap (`docs/PRIMA-DEVELOPMENT-ROADMAP.md`), establishing retrospective period-close reliability reporting, historical issue ledger auditing (`S-07`), cross-screen operational deep navigation (`S-01` → `S-07` and `S-07` → `S-02`), database indexing optimization, and a consolidated full-stack automated quality gate enforcing every domain invariant (`BR-PRIMA-001` through `BR-PRIMA-010`) and frontend engineering standard, delivering the **Feature-Complete PrIMA v1.0 Release Candidate** milestone.

Planning Mode: FEATURE-PLANNING

Referenced artifacts:
- ISSUE: `docs/issues/IS004-PRIMA-STAGE-4-ISSUE.md`
- FEATURE: `docs/PRIMA-DEVELOPMENT-ROADMAP.md` (Stage 4, lines 176–212), `docs/PRIMA-USE-CASE-DISCOVERY.md` (`H-1`, `H-2`), `docs/PRIMA-SCREEN-INVENTORY.md` (`S-07`, `S-01`, `S-02`), `docs/PRIMA-UI-UX-GUIDELINE.md`
- ARCHITECTURE: `docs/issues/IS004-PRIMA-STAGE-4-ARCHITECTURE.md` (and baseline `docs/PRIMA-ARCHITECTURE.md`)
- FEASIBILITY-ASSESSMENT: `docs/issues/IS004-PRIMA-STAGE-4-FEASIBILITY-ASSESSMENT.md` (`Status: READY-FOR-PLANNING`)

Architecture Applicability: ARCHITECTURE-REQUIRED  
The target architecture artifact `docs/issues/IS004-PRIMA-STAGE-4-ARCHITECTURE.md` authoritatively defines the technical realization, CQRS Read Model aggregation and filtering logic (`DEC-001`, `DEC-002`, `DEC-004`, `DEC-007`), SQL Server covering and filtered indexes (`DEC-006`), frontend `S-07` reporting view and pure CSS proportion breakdown architecture (`DEC-003`, `DEC-007`), cross-feature deep navigation contracts (`DEC-005`), and full-stack v1.0 quality gate verification suites (`DEC-008`).

---

# 2. Planning Scope

The scope of this implementation plan encompasses the complete delivery of Stage 4 across database indexing, backend CQRS Minimal APIs, contract synchronization, frontend reporting features, cross-screen navigation linkages, and full-stack automated quality gate verification:

1. **Database Schema & Indexing Optimization (`004_HistoryAndAnalyticsIndexes.sql`):**
   - Covering index `IX_InspectionRuns_ExecutionTimestampUtc` on `dbo.InspectionRuns(ExecutionTimestampUtc DESC) INCLUDE (Outcome)` for sub-second period-close pass rate queries.
   - Covering index `IX_Issues_CreatedAtUtc` on `dbo.Issues(CreatedAtUtc DESC) INCLUDE (Scope, CurrentSeverity, BaseSeverity, Status)` for issue creation volume and Scope/Severity distributions.
   - Filtered indexes on `dbo.Issues`: `IX_Issues_ResolvedAtUtc`, `IX_Issues_AcknowledgedAtUtc`, `IX_Issues_ReopenedAtUtc`, and `IX_Issues_DismissedAtUtc` (`WHERE ... IS NOT NULL`) for high-performance MTTA, MTTR, and status-aware historical ledger filtering.

2. **Backend History Minimal API Endpoints & CQRS Projections (`Features/History/*`):**
   - `GET /api/history/reliability-kpi`: Parameterized aggregation query accepting `fromUtc` and `toUtc`, computing the 6 period-close KPI metrics (`Total Issues`, `Resolution Rate %`, `Avg. Time to Acknowledge (h)`, `Avg. Time to Resolve (h)`, `Detector Pass Rate %`, and `Reopened Issues` count) and Scope/Severity distribution counts using Dapper.
   - `GET /api/history/issues`: Parameterized historical ledger query supporting `status` (`All`, `Resolved`, `Dismissed`, `Reopened`, and active statuses), `scope`, `detectorId`, `severity`, `search`, and date range with clamped limits (`default: 500`, `max: 1000`) and status-aware lifecycle date filtering.
   - Endpoint mapping in `HistoryEndpoints.cs` and registration in `Program.cs`.

3. **API Contract Synchronization & Frontend Shared Date Utilities:**
   - OpenAPI documentation and contract synchronization via `SwaggerDocumentTests.cs` and `npm run generate:api` producing strongly typed TypeScript definitions in `src/frontend/src/shared/types/api.generated.ts`.
   - Regional calendar boundary utility `getWibDateRange(preset, customRange?)` in `src/frontend/src/shared/utils/dateUtils.ts` translating `Asia/Jakarta` (`WIB`, UTC+7) business calendar boundaries to exact UTC ISO strings.

4. **Frontend Historical Reporting Slice (`src/frontend/src/features/history/`):**
   - TanStack Query composables `useReliabilityKpiQuery` and `useHistoricalIssuesQuery`.
   - `DateRangePresetBar.vue`: Presets (`Today`, `This Week`, `This Month`, `Last Month`, `Custom`) with inline date picker.
   - `ReliabilityKpiGrid.vue`: 6 KPI metric tiles with monospace typography (`font-mono tabular-nums`), null value formatting (`—`), and pass rate color coding.
   - `ScopeSeverityBreakdown.vue`: 2-column breakdown tables with pure CSS horizontal proportion bars (zero external chart libraries per `PRIMA-UI-UX-GUIDELINE.md §10`).
   - `HistoryIssueFilters.vue`: Multi-attribute filter controls (Status, Scope, Detector, Severity, Search).
   - `HistoryIssueTable.vue`: Virtualized historical issue table (zero pagination) routing to read-only `S-02`.
   - `HistoryView.vue` (`S-07`): Complete read-only screen orchestrator synchronizing filters, presets, and router query state.

5. **Cross-Feature UI Navigation & Application Shell Integration:**
   - `AppLayout.vue`: Activate `History & Reports` sidebar route (`/history`), update milestone version badges to `v1.0-rc`.
   - `SummaryCountersBar.vue` (`S-01`): Convert disabled `Resolved Today` tile into an active router link to `/history?preset=Today&status=Resolved`.
   - `WorkbenchHeader.vue` & `IssueDetailView.vue` (`S-02`): Inspect `route.query.from === 'history'` to dynamically render `← Back to History` and return via `router.back()`, preserving S-07 date and filter states.

6. **Full-Stack v1.0 Quality Gate Infrastructure & Verification:**
   - Dedicated backend test suite `DomainBusinessRulesQualityGateTests.cs` verifying `BR-PRIMA-001` through `BR-PRIMA-010` with zero skipped tests.
   - Automated frontend static compliance audit script `src/frontend/scripts/audit-frontend-compliance.mjs` verifying zero pagination, zero external charts, zero native alerts/confirms, and zero cross-slice imports.
   - Development demo data seeding expansion in `DevelopmentDemoDataSeeder.cs` and end-to-end integration smoke test `EndToEndStage4ReliabilityReportingSmokeTests.cs`.

**Explicitly Out of Scope (Deferred to Stage 5):**
- Stage 5: Pilot SIMRS detector suite across hospital modules (`BR-PRIMA-007`), hospital on-premise production deployment hardening, hospital UAT, and hypercare rollout.

---

# 3. Dependencies

## External Prerequisites
- .NET 8 SDK installed on host.
- Node.js (v20+ or v22 LTS) and npm installed on host.
- Microsoft SQL Server 2022 (local instance accessible via connection string configured in `appsettings.Development.json`).
- Stage 1, Stage 2, and Stage 3 platforms verified and operational in codebase.

## Slice Dependency Rules
- `Depends On` declares implementation prerequisites between slices.
- Dependencies reference Slice IDs only.
- Dependency satisfaction requires the referenced slice to have implementation status `IMPLEMENTED`.
- Dependency satisfaction does not require review status `GO`.
- Dependencies represent real implementation prerequisites (e.g. required code, types, tables).
- Slices with no mutual dependencies can and should be implemented concurrently.

---

# 4. Progress Summary

| Phase | Implementation Status | Review Status | Progress |
|---|---|---|---|
| P1 - Database Schema & High-Performance Indexing | IMPLEMENTED | GO | 1/1 |
| P2 - Backend Minimal APIs & CQRS Projections | IMPLEMENTED | GO | 2/2 |
| P3 - API Contract Synchronization & Shared Date Utilities | IMPLEMENTED | GO | 2/2 |
| P4 - Frontend Historical Reporting Feature Slice (S-07) | IMPLEMENTED | GO | 3/3 |
| P5 - Cross-Feature UI Navigation & Application Shell Integration | IMPLEMENTED | GO | 2/2 |
| P6 - Full-Stack v1.0 Quality Gate Verification & Smoke Testing | IMPLEMENTED | GO | 3/3 |

---

# 5. Phases

## P1 - Database Schema & High-Performance Indexing

Implementation Status: IMPLEMENTED  
Review Status: GO

### P1-S01

Title: DbUp Migration 004 - History & Analytics Covering & Filtered Indexes

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Create DbUp migration script `004_HistoryAndAnalyticsIndexes.sql` under `src/backend/PrIMA.Api/Migrations/Scripts/` introducing targeted covering and filtered indexes on `dbo.InspectionRuns` and `dbo.Issues` to optimize period-close KPI aggregations and historical ledger queries under high-volume telemetry (`DEC-006`, `RISK-001`).

Depends On: None  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. Migration script `src/backend/PrIMA.Api/Migrations/Scripts/004_HistoryAndAnalyticsIndexes.sql` created with idempotent `IF NOT EXISTS` guards:
   - Covering index `IX_InspectionRuns_ExecutionTimestampUtc` on `dbo.InspectionRuns(ExecutionTimestampUtc DESC) INCLUDE (Outcome)` for period-close detector pass rate queries.
   - Covering index `IX_Issues_CreatedAtUtc` on `dbo.Issues(CreatedAtUtc DESC) INCLUDE (Scope, CurrentSeverity, BaseSeverity, Status)` for issue creation volume and Scope/Severity distributions.
   - Filtered index `IX_Issues_ResolvedAtUtc` on `dbo.Issues(ResolvedAtUtc DESC) INCLUDE (CreatedAtUtc, ReopenedAtUtc, CurrentSeverity, Scope, DetectorId, Status) WHERE ResolvedAtUtc IS NOT NULL` for MTTR and closed-issue resolution rate queries.
   - Filtered index `IX_Issues_AcknowledgedAtUtc` on `dbo.Issues(AcknowledgedAtUtc DESC) INCLUDE (CreatedAtUtc) WHERE AcknowledgedAtUtc IS NOT NULL` for MTTA queries.
   - Filtered index `IX_Issues_ReopenedAtUtc` on `dbo.Issues(ReopenedAtUtc DESC) INCLUDE (CreatedAtUtc, Status) WHERE ReopenedAtUtc IS NOT NULL` for reopened issue auditing.
   - Filtered index `IX_Issues_DismissedAtUtc` on `dbo.Issues(DismissedAtUtc DESC) INCLUDE (CreatedAtUtc, Status) WHERE DismissedAtUtc IS NOT NULL` for resolution rate denominator queries.
2. Migration runs automatically via `DatabaseMigrator` at backend startup without syntax errors or schema conflicts.
3. Project compiles cleanly via `dotnet build src/backend/PrIMA.Api/PrIMA.Api.csproj`.

Notes:
- **Changed Files**: `src/backend/PrIMA.Api/Migrations/Scripts/004_HistoryAndAnalyticsIndexes.sql` (created; automatically included as embedded resource via `PrIMA.Api.csproj` wildcard `Migrations\Scripts\*.sql`).
- **Implementation Notes**: Implemented all 6 idempotent index definitions with `IF NOT EXISTS` guards (`IX_InspectionRuns_ExecutionTimestampUtc`, `IX_Issues_CreatedAtUtc`, `IX_Issues_ResolvedAtUtc`, `IX_Issues_AcknowledgedAtUtc`, `IX_Issues_ReopenedAtUtc`, `IX_Issues_DismissedAtUtc`) matching `IS004-PRIMA-STAGE-4-ARCHITECTURE.md` §8.1. Executed via `MigrationRunner.Run` during backend startup (`AuthEndpointsTests` / `PrIMAApiFactory`), verified index metadata in `dbo.SchemaVersions` and `sys.indexes`, verified idempotent re-execution via `sqlcmd`, and passed all 295 backend tests (`PrIMA.Api.Tests`).

---

## P2 - Backend Minimal APIs & CQRS Projections

Implementation Status: IMPLEMENTED  
Review Status: GO

### P2-S02

Title: Reliability KPI Aggregation Endpoint (Features/History/GetReliabilityKpi)

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Implement the vertical slice `Features/History/GetReliabilityKpi/` under `src/backend/PrIMA.Api/Features/History/` computing the 6 period-close KPI metrics (`Total Issues`, `Resolution Rate %`, `Avg. Time to Acknowledge`, `Avg. Time to Resolve`, `Detector Pass Rate %`, `Reopened Issues`) and Scope/Severity distributions using optimized Dapper queries (`DEC-001`, `DEC-002`).

Depends On: P1-S01  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. Files created under `src/backend/PrIMA.Api/Features/History/GetReliabilityKpi/`:
   - `GetReliabilityKpiQuery.cs`: Record defining `DateTime FromUtc` and `DateTime ToUtc`.
   - `GetReliabilityKpiValidator.cs`: FluentValidation rule asserting `FromUtc <= ToUtc`.
   - `GetReliabilityKpiResponse.cs`: DTO encapsulating:
     - `int TotalIssuesCreated`
     - `double ResolutionRatePercentage`
     - `double? AvgTimeToAcknowledgeHours`
     - `double? AvgTimeToResolveHours`
     - `double DetectorPassRatePercentage`
     - `int ReopenedIssuesCount`
     - `IReadOnlyList<BreakdownItemDto> ScopeBreakdown`
     - `IReadOnlyList<BreakdownItemDto> SeverityBreakdown`
     - `BreakdownItemDto` record (`string Category, int Count, double Percentage`).
   - `GetReliabilityKpiHandler.cs`: Executes optimized Dapper queries against `dbo.Issues` and `dbo.InspectionRuns`:
     - Total issues created: `COUNT(CreatedAtUtc BETWEEN @FromUtc AND @ToUtc)`.
     - Resolution Rate % (`DEC-001`): `ResolvedInPeriod / (ResolvedInPeriod + DismissedInPeriod) * 100` (returns `0.0` if denominator is 0, rounded to 1 decimal).
     - MTTA in decimal hours (`DEC-002`): `AVG(CAST(DATEDIFF_BIG(second, CreatedAtUtc, AcknowledgedAtUtc) AS FLOAT)) / 3600.0` for issues acknowledged in period (returns `null` if 0 count).
     - MTTR in decimal hours (`DEC-002`): `AVG(CAST(DATEDIFF_BIG(second, COALESCE(ReopenedAtUtc, CreatedAtUtc), ResolvedAtUtc) AS FLOAT)) / 3600.0` for issues resolved in period (returns `null` if 0 count).
     - Reopened issues count in period: `COUNT(ReopenedAtUtc BETWEEN @FromUtc AND @ToUtc)`.
     - Detector pass rate %: `PassedRuns / TotalRuns * 100` across inspection runs in period (returns `100.0` if `TotalRuns == 0`, rounded to 1 decimal).
     - Scope and Severity distributions for issues created in period, calculating counts and percentages.
   - `GetReliabilityKpiEndpoint.cs`: Maps `GET /api/history/reliability-kpi` with `[Authorize]`, FluentValidation check, Swagger documentation (`WithTags("History")`), and RFC 7807 problem details.
2. Endpoint registered in route collection via `HistoryEndpoints.cs` and mapped in `Program.cs`.
3. Integration tests created in `src/backend/PrIMA.Api.Tests/Integration/HistoryKpiEndpointTests.cs` verifying calculation formulas, boundary conditions, zero-issue handling, null formatting, and invalid date validation.
4. Project compiles cleanly via `dotnet build src/backend/PrIMA.Api/PrIMA.Api.csproj`.

Notes:
- **Changed Files**: `src/backend/PrIMA.Api/Features/History/GetReliabilityKpi/GetReliabilityKpiQuery.cs`, `src/backend/PrIMA.Api/Features/History/GetReliabilityKpi/GetReliabilityKpiValidator.cs`, `src/backend/PrIMA.Api/Features/History/GetReliabilityKpi/GetReliabilityKpiResponse.cs`, `src/backend/PrIMA.Api/Features/History/GetReliabilityKpi/GetReliabilityKpiHandler.cs`, `src/backend/PrIMA.Api/Features/History/GetReliabilityKpi/GetReliabilityKpiEndpoint.cs`, `src/backend/PrIMA.Api/Features/History/HistoryEndpoints.cs`, `src/backend/PrIMA.Api/Program.cs`, and `src/backend/PrIMA.Api.Tests/Integration/HistoryKpiEndpointTests.cs`.
- **Implementation Notes**: Implemented `GET /api/history/reliability-kpi` vertical slice under `Features/History/GetReliabilityKpi/` with UTC timestamp normalization, FluentValidation (`FromUtc <= ToUtc`) returning RFC 7807 `422 Unprocessable Entity`, `BadHttpRequestExceptionHandler` in `Program.cs` ensuring missing/malformed date query params return RFC 7807 `400 Bad Request` instead of `500` in Development, JWT `.RequireAuthorization()`, OpenAPI schemas `ReliabilityKpiResponse` and `BreakdownItemDto` tagged `"History"`, and index-seeking Dapper `QueryMultipleAsync` queries with explicit `DbType.DateTime2` parameters (preventing SQL Server `DATETIME` `.999` rounding on WIB boundaries) computing Total Issues Created, Closed-Issues Resolution Rate % (`DEC-001`), MTTA & Reopening-Turnaround MTTR (`DEC-002`), Reopened Issues count, Detector Pass Rate % (`100.0` when `TotalRuns == 0`), and Scope/Severity breakdowns. `dotnet build src/backend/PrIMA.Api/PrIMA.Api.csproj` succeeded (0 warnings, 0 errors), `HistoryKpiEndpointTests` passed 9/9 tests, and full `PrIMA.Api.Tests` suite passed 312/312 tests.

---

### P2-S03

Title: Historical Issue Ledger Endpoint (Features/History/GetHistoricalIssues)

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Implement the vertical slice `Features/History/GetHistoricalIssues/` under `src/backend/PrIMA.Api/Features/History/` providing a filterable historical issue ledger with status-aware lifecycle date filtering (`DEC-004`) and clamped result limits (`DEC-007`).

Depends On: P1-S01  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. Files created under `src/backend/PrIMA.Api/Features/History/GetHistoricalIssues/`:
   - `GetHistoricalIssuesQuery.cs`: Record defining `DateTime FromUtc, DateTime ToUtc, string? Status, string? Scope, Guid? DetectorId, string? Severity, string? Search, int? Limit`.
   - `GetHistoricalIssuesValidator.cs`: FluentValidation rule asserting `FromUtc <= ToUtc`.
   - `GetHistoricalIssuesResponse.cs`: DTO encapsulating `IReadOnlyList<HistoricalIssueItemDto> Items, int TotalReturned, int ClampedLimit`.
   - `HistoricalIssueItemDto.cs`: Record containing `Guid Id, string CorrelationKey, string Title, Guid DetectorId, string DetectorName, string Scope, string CurrentSeverity, byte SeverityRank, string Status, int EscalationLevel, int OccurrenceCount, string? CurrentAssignee, DateTime CreatedAtUtc, DateTime? ResolvedAtUtc, DateTime? DismissedAtUtc, DateTime? ReopenedAtUtc, DateTime? AcknowledgedAtUtc, DateTime LastOccurrenceAtUtc`.
   - `GetHistoricalIssuesHandler.cs`:
     - Clamps `limit` between 1 and 1000 (`Math.Clamp(query.Limit ?? 500, 1, 1000)`) (`DEC-007`).
     - Constructs dynamic parameterized SQL with status-aware lifecycle date filtering (`DEC-004`):
       - If `Status == "Resolved"`: `ResolvedAtUtc >= @FromUtc AND ResolvedAtUtc <= @ToUtc`.
       - If `Status == "Dismissed"`: `DismissedAtUtc >= @FromUtc AND DismissedAtUtc <= @ToUtc`.
       - If `Status == "Reopened"`: `ReopenedAtUtc >= @FromUtc AND ReopenedAtUtc <= @ToUtc`.
       - Else (`All` or active statuses): `CreatedAtUtc >= @FromUtc AND CreatedAtUtc <= @ToUtc`.
     - Supports optional filters: `Status`, `Scope`, `DetectorId`, `CurrentSeverity`, and text `Search` matching `Title`, `DetectorName`, or `CorrelationKey`.
     - Orders rows by `LastOccurrenceAtUtc DESC` with SQL `OFFSET 0 ROWS FETCH NEXT @Limit ROWS ONLY`.
   - `GetHistoricalIssuesEndpoint.cs`: Maps `GET /api/history/issues` with `[Authorize]`, FluentValidation check, Swagger documentation (`WithTags("History")`), and RFC 7807 problem details.
2. Endpoint registered in route collection via `HistoryEndpoints.cs` and mapped in `Program.cs`.
3. Integration tests created in `src/backend/PrIMA.Api.Tests/Integration/HistoricalIssuesEndpointTests.cs` verifying status-aware date filtering, limit clamping, multi-filter search, and empty result sets.
4. Project compiles cleanly via `dotnet build src/backend/PrIMA.Api/PrIMA.Api.csproj`.

Notes:
- **Changed Files**: `src/backend/PrIMA.Api/Features/History/GetHistoricalIssues/GetHistoricalIssuesQuery.cs`, `src/backend/PrIMA.Api/Features/History/GetHistoricalIssues/GetHistoricalIssuesValidator.cs`, `src/backend/PrIMA.Api/Features/History/GetHistoricalIssues/HistoricalIssueItemDto.cs`, `src/backend/PrIMA.Api/Features/History/GetHistoricalIssues/GetHistoricalIssuesResponse.cs`, `src/backend/PrIMA.Api/Features/History/GetHistoricalIssues/GetHistoricalIssuesHandler.cs`, `src/backend/PrIMA.Api/Features/History/GetHistoricalIssues/GetHistoricalIssuesEndpoint.cs`, `src/backend/PrIMA.Api/Features/History/HistoryEndpoints.cs`, `src/backend/PrIMA.Api/Program.cs`, and `src/backend/PrIMA.Api.Tests/Integration/HistoricalIssuesEndpointTests.cs`.
- **Implementation Notes**: Implemented `GET /api/history/issues` CQRS vertical slice with UTC timestamp normalization, FluentValidation (`FromUtc <= ToUtc`) returning RFC 7807 `422 Unprocessable Entity`, JWT `.RequireAuthorization()`, OpenAPI schema types `HistoricalIssuesResponse` and `HistoricalIssueItemDto` tagged `"History"`, clamped limits (`Math.Clamp(query.Limit ?? 500, 1, 1000)` per `DEC-007`), status-aware lifecycle date filtering (`ResolvedAtUtc`, `DismissedAtUtc`, `ReopenedAtUtc`, `CreatedAtUtc` per `DEC-004`), multi-attribute filters (`Status`, `Scope`, `DetectorId`, `Severity`, escaped `LIKE` `Search` across `Title`, `DetectorName`, `CorrelationKey`), and ordering `LastOccurrenceAtUtc DESC OFFSET 0 ROWS FETCH NEXT @Limit ROWS ONLY`. Remediated `NO-GO` Iteration 0 findings (`F-P2S03-01` & `F-P2S03-02`): bound `FromUtc` and `ToUtc` explicitly with `DbType.DateTime2` in `GetHistoricalIssuesHandler.cs` (preventing SQL Server `DATETIME` `.999` rounding on WIB boundaries), consolidated `NormalizeToUtc(DateTime)` into `internal static` on `GetHistoricalIssuesValidator` (removed duplicate from handler), added `else if (connection.State != ConnectionState.Open) { connection.Open(); }` synchronous fallback in `GetHistoricalIssuesHandler.cs`, updated `SeedIssueAsync` in `HistoricalIssuesEndpointTests.cs` to bind all seeded `DateTime` columns as `DbType.DateTime2`, and added integration test `GetHistoricalIssues_WibMillisecondBoundaryPrecision_IncludesExact999ZAndExcludesNextDayMidnightWib` covering `.999Z` inclusion vs next-day `00:00:00.000 WIB` exclusion across `All`, `Resolved`, `Dismissed`, and `Reopened`. Verified `dotnet build src/backend/PrIMA.Api/PrIMA.Api.csproj` (0 warnings, 0 errors) and `dotnet test src/backend/PrIMA.Api.Tests/PrIMA.Api.Tests.csproj --filter FullyQualifiedName~HistoricalIssuesEndpointTests` (9/9 passed, 0 failed, 0 skipped).

---

## P3 - API Contract Synchronization & Shared Date Utilities

Implementation Status: IMPLEMENTED  
Review Status: GO

### P3-S04

Title: Swagger Contract Generation & TypeScript API Type Generation

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Update `SwaggerDocumentTests.cs` to assert Stage 4 history endpoints and schemas, regenerate `swagger.json`, expand `scripts/generate-api.js` with history type aliases, and execute contract synchronization to produce strongly typed definitions in `src/frontend/src/shared/types/api.generated.ts`.

Depends On: P2-S02, P2-S03  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. `src/backend/PrIMA.Api.Tests/Integration/SwaggerDocumentTests.cs` updated:
   - Expected paths include `/api/history/reliability-kpi` and `/api/history/issues`.
   - Expected schemas include `ReliabilityKpiResponse`, `BreakdownItemDto`, `HistoricalIssuesResponse`, and `HistoricalIssueItemDto`.
2. Running `dotnet test --filter FullyQualifiedName~SwaggerDocumentTests` passes and refreshes `src/backend/PrIMA.Api/swagger.json`.
3. `src/frontend/scripts/generate-api.js` updated with convenience type aliases for history DTOs:
   - `ReliabilityKpiResponse`, `BreakdownItemDto`, `HistoricalIssuesResponse`, `HistoricalIssueItemDto`.
4. Executing `npm run generate:api` in `src/frontend` regenerates `src/frontend/src/shared/types/api.generated.ts` containing the history contracts without manual edits.
5. Project compiles cleanly via `dotnet build` and `npm run build`.

Notes:
- **Changed Files**: `src/backend/PrIMA.Api.Tests/Integration/SwaggerDocumentTests.cs`, `src/backend/PrIMA.Api/swagger.json`, `src/frontend/scripts/generate-api.js`, `src/frontend/src/shared/types/api.generated.ts`, and `docs/issues/IS004-PRIMA-STAGE-4-IMPLEMENTATION-PLAN.md`.
- **Implementation Notes**: Updated `SwaggerDocumentTests.cs` to assert Stage 4 endpoints (`/api/history/reliability-kpi`, `/api/history/issues`) and schemas (`ReliabilityKpiResponse`, `BreakdownItemDto`, `HistoricalIssuesResponse`, `HistoricalIssueItemDto`), and added `IS004-PRIMA-STAGE-4-IMPLEMENTATION-PLAN.md` to `FindRepoRoot()`. Executed `dotnet test src/backend/PrIMA.Api.Tests/PrIMA.Api.Tests.csproj --filter FullyQualifiedName~SwaggerDocumentTests` (1/1 passed in 834 ms), refreshing `src/backend/PrIMA.Api/swagger.json`. Added convenience type aliases (`ReliabilityKpiResponse`, `BreakdownItemDto`, `HistoricalIssuesResponse`, `HistoricalIssueItemDto`) to `src/frontend/scripts/generate-api.js` and ran `npm --prefix src/frontend run generate:api` (`openapi-typescript 7.13.0`), regenerating `src/frontend/src/shared/types/api.generated.ts` cleanly without manual edits. Verified `dotnet build src/backend/PrIMA.Api/PrIMA.Api.csproj` (0 warnings, 0 errors) and `npm --prefix src/frontend run build` (`vue-tsc -b && vite build`, exit code 0 in 7.70s).

---

### P3-S05

Title: Frontend WIB Operational Calendar & Date Range Utilities (dateUtils.ts)

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Implement `getWibDateRange(preset, customRange?)` in `src/frontend/src/shared/utils/dateUtils.ts` translating Indonesian hospital operational calendar boundaries (`Asia/Jakarta`, UTC+7) to exact UTC ISO strings (`DEC-003`, `RISK-002`).

Depends On: None  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. Types exported from `src/frontend/src/shared/utils/dateUtils.ts`:
   - `export type DatePreset = 'Today' | 'This Week' | 'This Month' | 'Last Month' | 'Custom';`
   - `export interface UtcDateRange { fromUtc: string; toUtc: string; }`
2. Function `getWibDateRange(preset: DatePreset, customRange?: [Date | null, Date | null]): UtcDateRange` implemented:
   - Resolves current date in `Asia/Jakarta` using `Intl.DateTimeFormat`.
   - `Today`: `00:00:00.000 WIB` to `23:59:59.999 WIB` converted to UTC ISO strings.
   - `This Week`: Monday of current week `00:00:00.000 WIB` to end of today `23:59:59.999 WIB` converted to UTC ISO strings.
   - `This Month`: First calendar day of month `00:00:00.000 WIB` to end of today `23:59:59.999 WIB` converted to UTC ISO strings.
   - `Last Month`: First day of previous month `00:00:00.000 WIB` to last day of previous month `23:59:59.999 WIB` converted to UTC ISO strings.
   - `Custom`: Converts user-provided date range to start-of-day and end-of-day UTC ISO strings.
3. Unit verification confirms accurate UTC ISO string conversions across month and week boundaries.
4. Project compiles cleanly via `npm run build`.

Notes: Implemented `DatePreset`, `UtcDateRange`, and `getWibDateRange` in `src/frontend/src/shared/utils/dateUtils.ts` conforming to Architecture §4.3.1 (`DEC-003`, `RISK-002`):
- Resolves current `Asia/Jakarta` (`WIB`, UTC+7) calendar year/month/day via `Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Jakarta' })`.
- Computes exact `00:00:00.000 WIB` to `23:59:59.999 WIB` UTC ISO boundaries for `Today`, `This Week` (Monday–Today), `This Month` (1st–Today), and `Last Month` (1st–last calendar day, including January year rollover and leap/non-leap February).
- `Custom` clones user-provided `Date` instances before setting start-of-day (`00:00:00.000`) and end-of-day (`23:59:59.999`) so reactive inputs are not mutated, falling back to `Today` WIB if either bound is missing or invalid.
- Preserved existing `formatWibDateTime` and `formatRelativeTime` exports unchanged.
- Added 7 unit tests in `src/frontend/src/shared/utils/dateUtils.test.mjs` executed via `node --test src/shared/utils/dateUtils.test.mjs` (7/7 passed in both local WIB and `TZ=UTC`).
- Verified clean TypeScript + Vite build via `npm run build` (`vue-tsc -b && vite build`, exit code 0).
Changed files:
- `src/frontend/src/shared/utils/dateUtils.ts`
- `src/frontend/src/shared/utils/dateUtils.test.mjs`
- `docs/issues/IS004-PRIMA-STAGE-4-IMPLEMENTATION-PLAN.md`

---

## P4 - Frontend Historical Reporting Feature Slice (S-07)

Implementation Status: IMPLEMENTED  
Review Status: GO

### P4-S06

Title: History TanStack Query Composables & Type Contracts

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Create the foundation for the history feature slice under `src/frontend/src/features/history/`, including view state types, query keys factory, and TanStack Query composables for KPI aggregates and the historical issue ledger.

Depends On: P3-S04, P3-S05  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. Files created under `src/frontend/src/features/history/`:
   - `types/history.types.ts`: View filter interface `HistoryFilterState`:
     - `preset: DatePreset`
     - `customRange: [Date | null, Date | null]`
     - `status: string`
     - `scope: string`
     - `detectorId: string | null`
     - `severity: string`
     - `search: string`
     - `limit: number`
   - `api/history.keys.ts`: TanStack Query key factory `historyKeys`:
     - `historyKeys.all`
     - `historyKeys.kpi(fromUtc, toUtc)`
     - `historyKeys.issues(filters)`
   - `api/useHistoryQueries.ts`:
     - `useReliabilityKpiQuery(fromUtc: Ref<string>, toUtc: Ref<string>)`: TanStack `useQuery` fetching `/api/history/reliability-kpi` with `staleTime: 30_000` and conditional `enabled` flag.
     - `useHistoricalIssuesQuery(filters: Ref<HistoryFilterState>, fromUtc: Ref<string>, toUtc: Ref<string>)`: TanStack `useQuery` fetching `/api/history/issues` with debounced search and active filter parameters.
2. Composable functions utilize typed Axios client and map responses to `ReliabilityKpiResponse` and `HistoricalIssuesResponse`.
3. Project compiles cleanly via `npm run build`.

Notes:
- **Changed Files**: `src/frontend/src/features/history/types/history.types.ts`, `src/frontend/src/features/history/api/history.keys.ts`, `src/frontend/src/features/history/api/useHistoryQueries.ts`, and `docs/issues/IS004-PRIMA-STAGE-4-IMPLEMENTATION-PLAN.md`.
- **Implementation Notes**: Created the isolated `src/frontend/src/features/history/` foundation with zero cross-slice imports (importing strictly from `@/shared/...` and relative slice modules). Implemented `HistoryFilterState` and helper `createDefaultHistoryFilterState` (`DEFAULT_HISTORY_LIMIT = 500`, `MAX_HISTORY_LIMIT = 1000`) in `types/history.types.ts` reusing `DatePreset` from `@/shared/utils/dateUtils` and generated DTO types from `@/shared/types/api.generated`. Implemented JSON-deterministic TanStack Query key factory `historyKeys` (`all`, `kpis`, `kpi(fromUtc, toUtc)`, `issuesList`, `issues(filters)`) in `api/history.keys.ts` with safe `Date` boundary serialization for `customRange`. Implemented `useReliabilityKpiQuery` (`/api/history/reliability-kpi`, `staleTime: 30_000`, `placeholderData: keepPreviousData`, conditional `enabled` via `isValidUtcDateRange`) and `useHistoricalIssuesQuery` (`/api/history/issues`, 300ms debounced `search` with `onScopeDispose` cleanup, active filter serialization omitting `'All'`/`null`/empty values, `staleTime: 30_000`, `placeholderData: keepPreviousData`, conditional `enabled`) using typed Axios client `http` in `api/useHistoryQueries.ts`. Verified cleanly via `npx vue-tsc -b --noEmit` (exit code 0), `npm run build` (`vue-tsc -b && vite build`, exit code 0 in 7.20s), `node --test src/frontend/src/shared/utils/dateUtils.test.mjs` (7/7 passed), and runtime composable/query-key verification assertions against TanStack `QueryClient` and mocked Axios adapter.

---

### P4-S07

Title: Historical Reliability KPI Grid & Pure CSS Proportion Breakdown Components

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Implement `DateRangePresetBar.vue`, `ReliabilityKpiGrid.vue`, and `ScopeSeverityBreakdown.vue` under `src/frontend/src/features/history/components/` rendering the 6 KPI tiles and pure CSS horizontal proportion bars without external charting libraries (`DEC-001`, `DEC-002`, `PRIMA-UI-UX-GUIDELINE.md §10`).

Depends On: P4-S06  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. `src/frontend/src/features/history/components/DateRangePresetBar.vue` created:
   - Preset button group: `Today`, `This Week`, `This Month`, `Last Month`, `Custom`.
   - PrimeVue `DatePicker` displayed inline when `Custom` preset is active.
   - Emits preset and date range changes to parent orchestrator.
2. `src/frontend/src/features/history/components/ReliabilityKpiGrid.vue` created:
   - 6 KPI metric tiles conforming to `PRIMA-SCREEN-INVENTORY.md §372-378`:
     1. Total Issues (with count)
     2. Resolution Rate % (formatted with `%`, color coded green if `>= 90%`, amber otherwise)
     3. Avg. Time to Acknowledge (MTTA in decimal hours, formatted as `X.X h` or `—` if null)
     4. Avg. Time to Resolve (MTTR in decimal hours, formatted as `X.X h` or `—` if null)
     5. Detector Pass Rate % (formatted with `%`, color coded green if `>= 98%`, rose otherwise)
     6. Reopened Issues (with count, badge indicator if `> 0`)
   - Monospace numbers (`font-mono tabular-nums`), tooltip descriptions, and loading skeleton state.
3. `src/frontend/src/features/history/components/ScopeSeverityBreakdown.vue` created:
   - 2-column layout: Left column = Scope Breakdown; Right column = Severity Breakdown.
   - Pure CSS horizontal proportion bars using native Tailwind CSS classes (zero external chart dependencies per `PRIMA-UI-UX-GUIDELINE.md §10`).
   - Monospace counts and percentages (`tabular-nums`).
   - Severity bar color mapping: Critical (`bg-rose-600`), High (`bg-orange-500`), Medium (`bg-amber-500`), Low (`bg-blue-500`).
   - Scope bar color mapping: DataIntegrity (`bg-indigo-500`), ProcessIntegrity (`bg-purple-500`), DataSync (`bg-cyan-500`), Infrastructure (`bg-emerald-500`).
4. Project compiles cleanly via `npm run build`.

Notes:
- **Changed Files**: `src/frontend/src/features/history/components/DateRangePresetBar.vue`, `src/frontend/src/features/history/components/ReliabilityKpiGrid.vue`, `src/frontend/src/features/history/components/ScopeSeverityBreakdown.vue`, and `docs/issues/IS004-PRIMA-STAGE-4-IMPLEMENTATION-PLAN.md`.
- **Implementation Notes**: Implemented the 3 S-07 presentation components under `src/frontend/src/features/history/components/` with zero cross-slice imports, zero external charting libraries, zero native `alert`/`confirm`, and zero `paginator`. `DateRangePresetBar.vue` renders the 5 operational presets (`Today`, `This Week`, `This Month`, `Last Month`, `Custom`) with an inline PrimeVue `DatePicker` (`selectionMode="range"`, `dateFormat="dd M yy"`, `:manualInput="false"`) when `Custom` is active and emits `update:preset`, `update:customRange`, and `change`. `ReliabilityKpiGrid.vue` renders the 6 KPI tiles (`Total Issues`, `Resolution Rate` green `>= 90%` / amber otherwise, `Avg. Time to Ack` `X.X h` / `—`, `Avg. Time to Resolve` `X.X h` / `—`, `Detector Pass Rate` green `>= 98%` / rose otherwise, and `Reopened Issues` with `Recurred` badge when `> 0`) using `font-mono tabular-nums`, `v-tooltip.bottom` descriptions, and PrimeVue `Skeleton` loading states. `ScopeSeverityBreakdown.vue` renders a 2-column layout (Scope left, Severity right) with pure CSS Tailwind horizontal proportion bars (`DataIntegrity` `bg-indigo-500`, `ProcessIntegrity` `bg-purple-500`, `DataSync` `bg-cyan-500`, `Infrastructure` `bg-emerald-500`; `Critical` `bg-rose-600`, `High` `bg-orange-500`, `Medium` `bg-amber-500`, `Low` `bg-blue-500`), `font-mono tabular-nums` counts/percentages, and PrimeVue `Skeleton` loading states. Verified cleanly via `npx vue-tsc -b --noEmit` (exit code 0) and `npm run build` (`vue-tsc -b && vite build`, exit code 0 in 6.91s) in `src/frontend`.

---

### P4-S08

Title: Historical Issue Audit Table & S-07 History & Reports View Orchestrator

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Implement `HistoryIssueFilters.vue`, `HistoryIssueTable.vue`, and `HistoryView.vue` under `src/frontend/src/features/history/`, delivering the complete `S-07` read-only screen with zero-pagination virtualized scrolling, URL query parameter initialization, and deep navigation to `S-02` (`DEC-004`, `DEC-005`, `DEC-007`).

Depends On: P4-S06, P4-S07  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. `src/frontend/src/features/history/components/HistoryIssueFilters.vue` created:
   - Status filter pills / dropdown (`All`, `Resolved`, `Dismissed`, `Reopened`, `Open`, `Acknowledged`, `Investigating`).
   - Scope dropdown, Detector dropdown (populated from detectors query), Severity dropdown.
   - Search text input with instant clear button.
   - Reset filters button.
2. `src/frontend/src/features/history/components/HistoryIssueTable.vue` created:
   - Virtualized scrolling (zero pagination) per `PRIMA-UI-UX-GUIDELINE.md §10`.
   - Columns: Correlation Key / Title, Scope, Severity, Status, Occurrences, Created At / Lifecycle date, MTTR duration, Action link.
   - Clicking an issue row routes to `/issues/:id?from=history` (`DEC-005`).
   - Empty state illustration and message when filters yield 0 results.
3. `src/frontend/src/features/history/views/HistoryView.vue` created (`S-07`):
   - Reads URL query parameters on mount (`preset`, `status`) to support deep links like `/history?preset=Today&status=Resolved`.
   - Coordinates `DateRangePresetBar`, `ReliabilityKpiGrid`, `ScopeSeverityBreakdown`, `HistoryIssueFilters`, and `HistoryIssueTable`.
   - Header with title "Historical Reliability & Reports", date range subtitle, and manual Refresh button.
   - Read-only screen layout with zero mutation actions.
4. Project compiles cleanly via `npm run build`.

Notes:
- **Changed Files**: `src/frontend/src/features/history/api/history.keys.ts`, `src/frontend/src/features/history/api/useHistoryQueries.ts`, `src/frontend/src/features/history/components/HistoryIssueFilters.vue`, `src/frontend/src/features/history/components/HistoryIssueTable.vue`, `src/frontend/src/features/history/views/HistoryView.vue`, and `docs/issues/IS004-PRIMA-STAGE-4-IMPLEMENTATION-PLAN.md`.
- **Implementation Notes**: Added `historyKeys.detectors()` and `useHistoryDetectorsQuery()` (`/detectors` via `@/shared/api/http` and `DetectorListItemDto`) so `S-07` populates the Detector filter dropdown with zero cross-slice imports. Implemented `HistoryIssueFilters.vue` with status filter pills (`All`, `Resolved`, `Dismissed`, `Reopened`, `Open`, `Acknowledged`, `Investigating`), Scope/Detector/Severity dropdowns, search input with instant clear button, and Reset Filters button. Implemented `HistoryIssueTable.vue` using PrimeVue `DataTable` with virtualized scrolling (`scrollable`, `scrollHeight="580px"`, `:virtualScrollerOptions="{ itemSize: 64 }"`, zero `paginator`), all 8 required columns (including WIB Created/Lifecycle dates and `DEC-002` MTTR duration), row/action click navigation to `/issues/:id?from=history` (`DEC-005`), clamped-limit notice (`DEC-007`), and empty state with reset action. Implemented `HistoryView.vue` (`S-07`) reading and syncing `preset` and `status` URL query parameters, coordinating all 5 child components, displaying the read-only header ("Historical Reliability & Reports", WIB period subtitle, manual Refresh button), and error banner. Verified zero forbidden imports/anti-patterns via `rg`, `npx vue-tsc -b --noEmit` (exit code 0), and `npm run build` (`vue-tsc -b && vite build`, exit code 0 in 7.61s) in `src/frontend`.

---

## P5 - Cross-Feature UI Navigation & Application Shell Integration

Implementation Status: IMPLEMENTED  
Review Status: GO

### P5-S09

Title: Sidebar Navigation Activation & v1.0-rc Milestone Badges (AppLayout.vue, Router)

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Register the `/history` route in Vue Router, activate the `History & Reports` sidebar navigation item in `AppLayout.vue`, and update application milestone badges to `v1.0-rc`.

Depends On: P4-S08  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. `src/frontend/src/app/router/index.ts` updated:
   - Route `/history` registered pointing to `HistoryView.vue` with `requiresAuth: true`.
2. `src/frontend/src/shared/layout/AppLayout.vue` updated:
   - `History & Reports` sidebar item activated: removed disabled style, removed "Coming in Stage 4" tooltip, set active route matching `/history`.
   - Application version badges updated from stage badge to `v1.0-rc` (Release Candidate) in header and footer / sidebar.
3. Clicking `History & Reports` in the sidebar smoothly navigates to `/history`.
4. Project compiles cleanly via `npm run build`.

Notes:
- **Changed Files**: `src/frontend/src/app/router/index.ts`, `src/frontend/src/app/layouts/AppLayout.vue`, and `docs/issues/IS004-PRIMA-STAGE-4-IMPLEMENTATION-PLAN.md`.
- **Implementation Notes**: Registered the `/history` child route (`name: 'history'`, lazy-loading `@/features/history/views/HistoryView.vue`, `meta: { requiresAuth: true, title: 'History & Reports - PrIMA' }`) in `src/frontend/src/app/router/index.ts`. Updated `src/frontend/src/app/layouts/AppLayout.vue` (the actual layout path imported by the router): activated the `History & Reports` sidebar navigation item as a `<RouterLink to="/history">` with `isHistoryRoute` (`route.path.startsWith('/history')`) active styling, removed the disabled styling, `"Coming in Stage 4"` tooltip, and `Stage 4` pill, and updated the header stage badge (`Stage 1` -> `v1.0-rc`) and footer version badge (`v1.0-stage1` -> `v1.0-rc`). Verified cleanly via `npx vue-tsc -b --noEmit` (exit code 0) and `npm run build` (`vue-tsc -b && vite build`, exit code 0 in 7.04s, emitting `HistoryView-BiAhHyXW.js`) in `src/frontend`.

---

### P5-S10

Title: S-01 Dashboard Deep Link (Resolved Today) & S-02 Contextual Origin Navigation (← Back to History)

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Wire cross-screen operational navigation: activate the `Resolved Today` tile on `S-01` to route to `/history?preset=Today&status=Resolved`, and extend `S-02` `WorkbenchHeader.vue` to inspect `route.query.from === 'history'` to render `← Back to History` and return via `router.back()` (`DEC-005`).

Depends On: P4-S08  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. `src/frontend/src/features/dashboard/components/SummaryCountersBar.vue` (`S-01`) updated:
   - Tile 5 (`Resolved Today`): removed lock icon and disabled tooltip.
   - Added click handler executing `router.push({ path: '/history', query: { preset: 'Today', status: 'Resolved' } })`.
   - Styled with cursor pointer and hover transition matching active summary tiles.
2. `src/frontend/src/features/issues/components/WorkbenchHeader.vue` and `views/IssueDetailView.vue` (`S-02`) updated:
   - Evaluates `route.query.from === 'history'`.
   - If true: renders button label `← Back to History` (instead of `Back to Dashboard`).
   - Clicking executes `router.back()` with fallback to `router.push('/history')` if history stack is empty.
   - Preserves S-07 filter and date range states upon return.
   - Closed issues (`Resolved`, `Dismissed`) rendered from history show read-only status in Workbench action bar without mutating action buttons.
3. Project compiles cleanly via `npm run build`.

Notes:
- **Changed Files**: `src/frontend/src/features/dashboard/components/SummaryCountersBar.vue`, `src/frontend/src/features/issues/components/WorkbenchHeader.vue`, `src/frontend/src/features/issues/views/IssueDetailView.vue`, and `docs/issues/IS004-PRIMA-STAGE-4-IMPLEMENTATION-PLAN.md`.
- **Implementation Notes**: Activated Tile 5 (`Resolved Today`) in `SummaryCountersBar.vue` (`S-01`) as a `<button>` with `cursor-pointer` and emerald hover transitions, removing the Stage 1 lock icon and disabled tooltip and routing via `router.push({ path: '/history', query: { preset: 'Today', status: 'Resolved' } })`. Updated `WorkbenchHeader.vue` and `IssueDetailView.vue` (`S-02`) to inspect `route.query.from === 'history'`, render `← Back to History` (instead of `Back to Dashboard`), and return via `router.back()` when `window.history.state?.back` is present with fallback to `router.push('/history')` (or `'/'`), preserving `S-07` URL query state (`preset`, `status`) and retaining read-only action bar rendering for `Resolved`/`Dismissed` issues. Verified zero cross-slice imports (`rg -n "features/history"` returned 0 matches in `dashboard`/`issues`), `npx vue-tsc -b --noEmit` (exit code 0), and `npm run build` (`vue-tsc -b && vite build`, exit code 0 in 6.98s) in `src/frontend`.

---

## P6 - Full-Stack v1.0 Quality Gate Verification & Smoke Testing

Implementation Status: IMPLEMENTED  
Review Status: GO

### P6-S11

Title: Backend Domain Business Rules Consolidated Quality Gate Test Suite

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Create a dedicated integration test suite `DomainBusinessRulesQualityGateTests.cs` in `PrIMA.Api.Tests` explicitly asserting compliance across all 10 domain business rules (`BR-PRIMA-001` through `BR-PRIMA-010`) with zero skipped tests (`DEC-008`).

Depends On: P2-S02, P2-S03  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. Test suite `src/backend/PrIMA.Api.Tests/Integration/DomainBusinessRulesQualityGateTests.cs` created with `[Collection("ApiTestCollection")]`.
2. Explicit test methods covering all 10 domain business rules without skips:
   - `BR-PRIMA-001`: Ingesting duplicate finding with matching `CorrelationKey` appends occurrence; does not duplicate `dbo.Issues`.
   - `BR-PRIMA-002`: Ingesting finding on `Resolved` or `Dismissed` issue transitions status to `Reopened`, marks prior governance records `IsSuperseded = 1`, resets assignee, and creates occurrence.
   - `BR-PRIMA-003`: Unacknowledged `Open` issues past SLA timeout elevate severity (+1 step); issues with `>= 5` occurrences elevate severity, capped at `Critical`.
   - `BR-PRIMA-004`: `ResolveIssue` fails with 409 Conflict if no `CleanPassed` verification record exists; succeeds once clean verification is recorded.
   - `BR-PRIMA-005`: Missed heartbeat creates `SILENT:{id}` issue; failed run creates `FAIL:{id}` issue.
   - `BR-PRIMA-006`: `DismissIssue` fails with 422 if rationale length `< 20` characters; succeeds with rationale `>= 20`.
   - `BR-PRIMA-007`: Webhook client enforces 15-second timeout; timeout records `ExecutionFailed` and returns 504 ProblemDetails.
   - `BR-PRIMA-008`: Authenticated officer name is deterministically recorded across `dbo.IssueActivityLogs`, remediation, verification, and dismissal.
   - `BR-PRIMA-009`: Sequential occurrences (#1, #2, #3) are immutable; telemetry evidence snapshot is preserved intact.
   - `BR-PRIMA-010`: Detector health transitions to `Silent` after `2.0 * ExpectedCadenceSeconds`; prolonged silence adds occurrences at cadence multiples.
3. All 10 test methods execute against the test database and pass with zero failures and zero skipped tests.
4. Project compiles cleanly via `dotnet test`.

Notes:
- **Changed Files**: `src/backend/PrIMA.Api.Tests/Integration/DomainBusinessRulesQualityGateTests.cs` (created).
- **Implementation Notes**: Created `[Collection("ApiTestCollection")]` integration test suite `DomainBusinessRulesQualityGateTests` with 10 explicit `[Fact]` methods covering `BR-PRIMA-001` through `BR-PRIMA-010` against the live `PrIMA_IntegrationTests` SQL Server database and `PrIMAApiFactory` endpoints/services without skips:
  - `BR_PRIMA_001`: Verifies duplicate finding ingestion with matching `CorrelationKey` (case-insensitive normalization) appends occurrences (#1, #2, #3) without creating duplicate `dbo.Issues` rows.
  - `BR_PRIMA_002`: Verifies recurring finding ingestion on both `Resolved` and `Dismissed` issues transitions status to `Reopened`, resets `CurrentAssignee`/`AcknowledgedAtUtc`/`ResolvedAtUtc`/`DismissedAtUtc`, marks prior `RemediationRecords`, `VerificationRecords`, and `DismissalRecords` as `IsSuperseded = 1`, creates new occurrence, and logs `Reopened`.
  - `BR_PRIMA_003`: Verifies `MonitoringSupervisorWorker` elevates unacknowledged `Open` issues past SLA timeout (+1 step per SLA window) and issues with `>= 5` occurrences (+1 step), capped at `Critical` (`SeverityRank = 4`).
  - `BR_PRIMA_004`: Verifies `POST /api/issues/{id}/resolve` returns `409 Conflict` when no verification, remediation-only, or failed verification (`CleanPassed = false`) exists, and succeeds (`204 NoContent`) once `CleanPassed = true` verification is recorded.
  - `BR_PRIMA_005`: Verifies missed cadence supervision creates `SILENT:{id}` (`Infrastructure` scope) and `ExecutionFailed` ingestion creates `FAIL:{id}` (`Infrastructure` scope).
  - `BR_PRIMA_006`: Verifies `POST /api/issues/{id}/dismiss` rejects rationale lengths `< 20` characters (19 chars and whitespace-padded short strings) with `422 Unprocessable Entity` and succeeds (`204 NoContent`) at exact 20-character boundary.
  - `BR_PRIMA_007`: Verifies `DetectorWebhookClient` enforces a 15-second `HttpClient.Timeout`, records `ExecutionFailed` in `dbo.InspectionRuns` (plus `Failing` detector health and `FAIL:{id}` issue), and `POST /api/detectors/{id}/trigger-run` returns `504 GatewayTimeout` `ProblemDetails`.
  - `BR_PRIMA_008`: Verifies authenticated officer `FullName` (`Siti Rahma`, `Ahmad Budi`, `EDP Lead`) is deterministically captured across `dbo.IssueActivityLogs`, `dbo.InvestigationNotes`, `dbo.RemediationRecords`, `dbo.VerificationRecords`, and `dbo.DismissalRecords`.
  - `BR_PRIMA_009`: Verifies sequential occurrences (#1, #2, #3) remain immutable in `dbo.Occurrences` and `GET /api/issues/{id}` with each historical `TelemetryEvidence` snapshot preserved intact.
  - `BR_PRIMA_010`: Verifies detector remains `Healthy` before `2.0 * ExpectedCadenceSeconds`, transitions to `Silent` after `2.0 * ExpectedCadenceSeconds`, and appends `SILENT:{id}` occurrences at `3x` and `4x` cadence multiples.
- **Verification**:
  - `dotnet test src/backend/PrIMA.Api.Tests/PrIMA.Api.Tests.csproj --filter "FullyQualifiedName~DomainBusinessRulesQualityGateTests" --verbosity normal`: `Passed! - Failed: 0, Passed: 10, Skipped: 0, Total: 10` (`0 Warning(s)`, `0 Error(s)`).
  - `dotnet test src/backend/PrIMA.slnx`: `Passed! - Failed: 0, Passed: 323, Skipped: 0, Total: 323` (`Duration: 18 s`).

---

### P6-S12

Title: Frontend Static Compliance Audit Script & CI Quality Gate

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Implement an automated static audit script `src/frontend/scripts/audit-frontend-compliance.mjs` verifying zero forbidden anti-patterns (`PRIMA-UI-UX-GUIDELINE.md §10`) and zero cross-slice imports across the frontend codebase (`DEC-008`).

Depends On: P4-S08, P5-S09, P5-S10  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. Script `src/frontend/scripts/audit-frontend-compliance.mjs` created and added to `src/frontend/package.json` under `"audit:compliance": "node scripts/audit-frontend-compliance.mjs"`.
2. Script recursively parses `src/frontend/src/` asserting:
   - Zero `paginator` attributes/props in Vue templates across `S-01`, `S-03`, `S-06`, and `S-07` views and components.
   - Zero imports of external charting libraries (`chart.js`, `echarts`, `apexcharts`, etc.).
   - Zero calls to native `window.alert()` or `window.confirm()`.
   - Zero cross-slice imports between `src/features/{auth,dashboard,issues,detectors,history}/`.
3. Script outputs a detailed summary and exits with code 0 on complete compliance; exits with code 1 and lists offending files and line numbers upon any violation.
4. Running `npm run audit:compliance` succeeds cleanly with code 0.

Notes:
- **Changed Files**: `src/frontend/scripts/audit-frontend-compliance.mjs`, `src/frontend/package.json`, `src/frontend/src/features/issues/api/issueDetail.keys.ts`, `src/frontend/src/features/issues/api/useIssueDetailQueries.ts`, `src/frontend/src/features/issues/components/WorkbenchHeader.vue`, `src/frontend/src/features/issues/views/IssueDetailView.vue`, and `docs/issues/IS004-PRIMA-STAGE-4-IMPLEMENTATION-PLAN.md`.
- **Implementation Notes**: Created `src/frontend/scripts/audit-frontend-compliance.mjs` (ESM Node script) and registered `"audit:compliance": "node scripts/audit-frontend-compliance.mjs"` in `src/frontend/package.json`. The script recursively scans `src/frontend/src/` (`.vue`, `.ts`, `.tsx`, `.js`, `.jsx`, `.mjs`) and asserts 4 quality gates: `[no-paginator]` on Vue `<template>` blocks in `dashboard`/`detectors`/`history` (`S-01`, `S-03`, `S-06`, `S-07`), `[no-external-charts]` against charting packages (`chart.js`, `echarts`, `apexcharts`, etc.), `[no-native-dialogs]` against `window.alert()`/`window.confirm()`, and `[no-cross-slice-imports]` across `src/features/{auth,dashboard,issues,detectors,history}/` (supporting both `@/features/...` aliases and relative imports, single-line and multi-line). Resolved 2 pre-existing cross-slice imports discovered by the audit in `S-02` (`IssueDetailView.vue` and `WorkbenchHeader.vue` importing `useDetectorDetailQuery` from `@/features/detectors/api/useDetectorQueries`) by adding `issueDetailKeys.detectorDetail` and `useIssueDetectorDetailQuery` inside `src/features/issues/api/`. Verified via `npm run audit:compliance` (76 files scanned, 0 violations, exit code 0) and `npm run build` (`vue-tsc -b && vite build`, exit code 0 in 7.70s).

---

### P6-S13

Title: Demo Data Seeding & End-to-End Historical Reporting Smoke Verification

Implementation Status: IMPLEMENTED  
Review Status: GO  

Objective: Expand `DevelopmentDemoDataSeeder.cs` with rich historical issues and inspection runs across multiple temporal cohorts, and create an end-to-end integration smoke test `EndToEndStage4ReliabilityReportingSmokeTests.cs` verifying the complete reporting flow.

Depends On: P5-S09, P5-S10, P6-S11, P6-S12  

Repository: b23-prima-proactive-monitoring  

Completion Criteria:
1. `src/backend/PrIMA.Api/Infrastructure/Demo/DevelopmentDemoDataSeeder.cs` expanded to seed:
   - Historical issues resolved today, resolved last week, dismissed, reopened, and open across multiple scopes and detectors.
   - Historical inspection runs across the past 30 days generating realistic pass rates (`Passed` and `ExecutionFailed`).
2. Integration smoke test `src/backend/PrIMA.Api.Tests/Integration/EndToEndStage4ReliabilityReportingSmokeTests.cs` created:
   - Authenticates test officer.
   - Executes `GET /api/history/reliability-kpi` with `Today` and `This Month` date ranges, asserting all 6 metrics, Scope breakdown, and Severity breakdown.
   - Executes `GET /api/history/issues?status=Resolved` with date range, asserting returned items match the resolved cohort.
   - Verifies limit clamping when requesting `limit=2000`.
3. Full solution verification:
   - `dotnet test` executes all unit and integration test suites with zero failures.
   - `npm run audit:compliance` exits with code 0.
   - `npm run build` succeeds cleanly with 0 TypeScript/Vue errors.

Notes:
- **Changed Files**: `src/backend/PrIMA.Api/Seeding/DevelopmentDemoDataSeeder.cs`, `src/backend/PrIMA.Api.Tests/Integration/EndToEndStage4ReliabilityReportingSmokeTests.cs` (created), and `docs/issues/IS004-PRIMA-STAGE-4-IMPLEMENTATION-PLAN.md`.
- **Implementation Notes**: Expanded `DevelopmentDemoDataSeeder.cs` (actual repo path `src/backend/PrIMA.Api/Seeding/DevelopmentDemoDataSeeder.cs`) with `SeedTodayInspectionRunsAsync` (deterministic `Passed` and `ExecutionFailed` runs within today's WIB operational window, plus 65 historical 30-day runs for `BillingDetectorId` preserving >90% 30-day pass rate) and `SeedHistoricalCohortIssuesAsync` (idempotent cohorts for resolved today, resolved last week, dismissed last week, reopened today and reopened last week with superseded prior governance records, and historical open across `DataIntegrity`, `ProcessIntegrity`, and `Infrastructure` scopes and `Low`/`Medium`/`High`/`Critical` severities). Created `[Collection("ApiTestCollection")]` integration smoke test `EndToEndStage4ReliabilityReportingSmokeTests` authenticating `ahmad.budi`, querying `GET /api/history/reliability-kpi` for WIB `Today` and `This Month` (asserting all 6 KPI metrics, `ScopeBreakdown`, and `SeverityBreakdown`), querying `GET /api/history/issues?status=Resolved` for both ranges, and verifying `limit=2000` clamping to `ClampedLimit = 1000`.
- **Verification**:
  - `dotnet test src/backend/PrIMA.slnx`: `Passed! - Failed: 0, Passed: 324, Skipped: 0, Total: 324` (`Duration: 18 s`).
  - `npm run audit:compliance` (in `src/frontend`): `PASSED: 0 compliance violations found.` (`Scanned files: 76`, exit code 0).
  - `npm run build` (in `src/frontend`): `vue-tsc -b && vite build` succeeded cleanly (`✓ 2135 modules transformed`, `✓ built in 8.30s`, exit code 0).

---

# 6. Change Log

- 2026-11-22: Initial creation of Stage 4 Implementation Plan covering DbUp 004 index migration, backend CQRS history Minimal APIs, contract synchronization, frontend `S-07` reporting slice, cross-screen navigation linkages, and full-stack v1.0 quality gate verification suites. Plan structure released for execution (`Execution Approval: APPROVED`).
