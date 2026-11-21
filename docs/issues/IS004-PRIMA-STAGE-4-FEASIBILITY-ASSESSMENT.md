---
Title: PrIMA Stage 4 — Historical Reliability Analytics, Reporting & v1.0 Quality Gate
Code: PRIMA-STAGE-4
Artifact: FEASIBILITY-ASSESSMENT
Version: 1.0
LastUpdated: 2026-11-21
Status: READY-FOR-PLANNING
---

# 1. Request Summary

Assess the feasibility of `IS004-PRIMA-STAGE-4-ISSUE.md` (`ID: PRIMA-STAGE-4`, `Type: CHANGE-REQUEST`): implementing Stage 4 ("Historical Reliability Analytics, Reporting & v1.0 Quality Gate") of `docs/PRIMA-DEVELOPMENT-ROADMAP.md` to deliver the **Feature-Complete PrIMA v1.0 Release Candidate** milestone.

In scope for Stage 4:
1. **Historical Reliability KPI Reporting (`S-07`, Use Case `H-1`):**
   - High-performance parameterized aggregation backend query `Features/History/GetReliabilityKpi` computing the 6 KPI tiles for any selected date range: `Total Issues`, `Resolution Rate %`, `Avg. Time to Acknowledge (h)` (MTTA), `Avg. Time to Resolve (h)` (MTTR), `Detector Pass Rate %`, and `Reopened Issues` count.
   - Breakdown counts: Scope breakdown (`Data Integrity`, `Process Integrity`, `Data Synchronization`, `Infrastructure & Service`) and Severity breakdown (`Critical`, `High`, `Medium`, `Low`).
   - Pure CSS horizontal proportion bars on the frontend (zero external chart libraries, conforming strictly to `PRIMA-UI-UX-GUIDELINE.md §10`).
2. **Historical Issue Audit Exploration (`S-07`, Use Case `H-2`):**
   - Filterable historical issue query `Features/History/GetHistoricalIssues` supporting `All`, `Resolved`, `Dismissed`, `Reopened`, and active statuses across Scope, Detector, Severity, and UTC/WIB date boundaries, with clamped results (`default: 500`, `max: 1000`) and zero-pagination virtualized scrolling.
   - Deep navigation to `S-02` Issue Detail Workbench in read-only mode for closed issues (`Resolved`, `Dismissed`), with query param origin tracking (`?from=history`) rendering a contextual `← Back to History` button that preserves S-07 date and filter states via `router.back()`.
3. **Dashboard-to-History Deep Link (`S-01` → `S-07`):**
   - Convert the `Resolved Today` summary tile on `S-01` from a disabled placeholder into an active link routing directly to `/history?preset=Today&status=Resolved`.
4. **Database Indexing Optimization (`004_HistoryAndAnalyticsIndexes.sql`):**
   - Add targeted covering and filtered indexes on `dbo.InspectionRuns` and `dbo.Issues` to ensure zero table scans and sub-second execution on high-volume telemetry under 1,000+ simulated issues.
5. **Full-Stack v1.0 Quality Gate Verification:**
   - Dedicated backend regression test suite `DomainBusinessRulesQualityGateTests.cs` asserting end-to-end compliance across all 10 domain business rules (`BR-PRIMA-001` through `BR-PRIMA-010`) with zero skipped tests.
   - Full automated compliance check against `PRIMA-UI-UX-GUIDELINE.md §10` anti-patterns: zero pagination, zero external chart libraries, zero browser alerts/confirms, zero cross-slice imports (`src/features/{auth,dashboard,issues,detectors,history}/`), keyboard shortcuts (`Esc`, `Ctrl+Enter`), tablet drawer responsiveness (< 1024px), and silent network reconnect handling (`failureCount >= 2`).

Explicitly out of scope for Stage 4:
- **Stage 5:** Pilot SIMRS detector suite across real hospital modules (`BR-PRIMA-007`), on-premise production deployment hardening, hospital UAT, and hypercare rollout.

Referenced artifacts:
- `ISSUE`: `docs/issues/IS004-PRIMA-STAGE-4-ISSUE.md`
- `ROADMAP`: `docs/PRIMA-DEVELOPMENT-ROADMAP.md` (Stage 4, lines 176–212)
- `DOMAIN`: `docs/PRIMA-DOMAIN.md` (All 10 Business Rules `BR-PRIMA-001` through `BR-PRIMA-010`)
- `USE-CASES`: `docs/PRIMA-USE-CASE-DISCOVERY.md` (`H-1`, `H-2`)
- `SCREENS`: `docs/PRIMA-SCREEN-INVENTORY.md` (`S-07`, lines 362–389; `S-01`, `S-02`)
- `UI-UX GUIDELINE`: `docs/PRIMA-UI-UX-GUIDELINE.md` (§5, §6.9, §10 Anti-Patterns Checklist)
- `ARCHITECTURE`: `docs/PRIMA-ARCHITECTURE.md` (History queries, CQRS read model, index strategy)
- Completed Baseline Artifacts: Stages 1, 2, and 3 issues, feasibility assessments, architectures, implementation plans, and test packages.

## Objective

Assess Stage 4 feasibility against current codebase state (`src/backend` and `src/frontend`), system specifications, and performance requirements. Surface, grill, and resolve every architectural, domain, UX, and lifecycle question so downstream `ica-architect` Architecture Update, Planning, and implementation agents do not have to invent any decision.

---

# 2. Current State

## Existing Behavior

- **Stages 1, 2, and 3 are fully operational in the repository:**
  - `src/backend/PrIMA.Api/`: ASP.NET Core .NET 8 Minimal API with 295 passing unit and integration tests. Supports JWT authentication, telemetry ingestion with deduplication (`BR-PRIMA-001`) and reopening (`BR-PRIMA-002`), issue triage commands (`TakeIt`, `Assign`), append-only notes, remediation logging (`M-02`), manual verification (`M-03`), verification gate enforcement (`BR-PRIMA-004`), dismissal (`M-01`), autonomous silence detection (`BR-PRIMA-010`), detector execution failure synthesis (`BR-PRIMA-005`), dynamic severity escalation (`BR-PRIMA-003`), detector lifecycle management (`Active <-> Paused -> Retired`), and synchronous webhook verification re-runs (`D-2`).
  - `src/frontend/`: Vue 3.5+ with Tailwind CSS v4, PrimeVue v4 (Aura preset), Pinia auth store, TanStack Query polling, `S-00` Login, `S-01` Issue Dashboard with Alert Strip and 5 summary counters, `S-02` Issue Detail Workbench with context-sensitive Action Bar, `S-03` Detector Portfolio, `S-04` Detector Detail, `S-05` Detector Form, and `S-06` Inspection Run Log.
- **Historical analytics and reporting are currently unimplemented:**
  - There are no endpoints under `/api/history/*`: no `GetReliabilityKpi` aggregation endpoint and no `GetHistoricalIssues` filterable query endpoint.
  - The feature slice `src/frontend/src/features/history/` does not exist; route `/history` (`S-07`) is not registered in `src/app/router/index.ts`.
  - In `AppLayout.vue`, the sidebar navigation item `History & Reports` is disabled with tooltip `"Coming in Stage 4"`.
  - In `SummaryCountersBar.vue` (`S-01`), the `Resolved Today` tile has a lock icon and tooltip `"History view available in Stage 4"`, lacking deep-link navigation.
  - In `WorkbenchHeader.vue` (`S-02`), back navigation unconditionally uses label `"Back to Dashboard"`, with no awareness of navigation origin when opening closed issues from historical audit logs.
- **Database Indexing:**
  - `dbo.Issues` only has `UX_Issues_CorrelationKey` and `IX_Issues_Status_SeverityRank (Status, SeverityRank DESC, LastOccurrenceAtUtc DESC)`. It lacks dedicated indexes on `CreatedAtUtc`, `ResolvedAtUtc`, `AcknowledgedAtUtc`, and `ReopenedAtUtc`.
  - `dbo.InspectionRuns` only has `IX_InspectionRuns_DetectorId_Time (DetectorId, ExecutionTimestampUtc DESC)`. Querying runs across all detectors for period-close pass rate requires an index scan.
- **Automated Regression & Quality Gate:**
  - While individual feature tests exist, there is no single consolidated automated regression suite verifying all 10 domain business rules (`BR-PRIMA-001` through `BR-PRIMA-010`) together.
  - The UI/UX compliance audit verifying zero anti-patterns (`PRIMA-UI-UX-GUIDELINE.md §10`), zero cross-slice imports, and 1,000+ row virtual scroll stability has not yet been executed.

## Existing Constraints

- **Verified local development environment:**
  - OS: Windows (PowerShell shell).
  - .NET SDK: `net8.0` (`8.0.428`).
  - Node.js / npm: `v22.22.3` / `10.9.8`.
  - Database: Microsoft SQL Server 2022 Developer Edition running on `localhost` (`MSSQLSERVER` active, connection string in `appsettings.Development.json`).
- **Architectural & Technology Constraints:**
  - Backend: ASP.NET Core .NET 8 Minimal API, vertical slice architecture (`Features/History/*`), Dapper for aggregation queries, FluentValidation, RFC 7807 `ProblemDetails`, DbUp startup migrations, and OpenAPI (`/swagger/v1/swagger.json`).
  - Frontend: Vue 3.5+ (`<script setup lang="ts">`), Vite 6, Tailwind CSS v4, PrimeVue v4 (Aura preset), Pinia, TanStack Query v5, and `openapi-typescript` generating `src/shared/types/api.generated.ts`.
  - Feature-slice isolation: `src/features/{auth,dashboard,issues,detectors,history}/` with strictly zero cross-feature imports.
  - Operational timezone: `Asia/Jakarta` (`WIB`, UTC+7).
  - Currency formatting: Indonesian Rupiah (`id-ID`).
  - Virtualized scrolling: Virtual scroll with zero pagination on feeds and audit tables (`S-01`, `S-03`, `S-06`, `S-07`).
  - Strict non-charting constraint: `S-07` Scope & Severity breakdown must use pure CSS horizontal proportion bars; external charting libraries (`Chart.js`, `ECharts`) are strictly forbidden per `PRIMA-UI-UX-GUIDELINE.md §10`.

---

# 3. Gap Analysis

| ID | Severity | Status | Gap |
|---|---|---|---|
| GAP-001 | CRITICAL | CLOSED | Missing `Features/History/GetReliabilityKpi` backend vertical slice: no endpoint or Dapper query to compute the 6 period-close KPI metrics (`Total Issues`, `Resolution Rate %`, `Avg. Time to Acknowledge`, `Avg. Time to Resolve`, `Detector Pass Rate %`, `Reopened Issues`) and Scope/Severity breakdown counts. |
| GAP-002 | CRITICAL | CLOSED | Missing `Features/History/GetHistoricalIssues` backend vertical slice: no endpoint or query to filter the complete historical issue ledger across all statuses (`All`, `Resolved`, `Dismissed`, `Reopened`, active) with Scope, Detector, Severity, and date range parameters. |
| GAP-003 | CRITICAL | CLOSED | Missing frontend `src/features/history/` slice and `S-07 History & Reports` view: route `/history` is unregistered; date range picker with presets (`Today`, `This Week`, `This Month`, `Last Month`, `Custom`), 6-tile KPI grid, pure CSS 2-column breakdown bars, and virtualized historical issues table are missing. |
| GAP-004 | MAJOR | CLOSED | Missing S-01 deep-link and S-02 origin navigation integration: `Resolved Today` tile on `S-01` is disabled; `S-02` `WorkbenchHeader.vue` has no mechanism to detect origin (`from=history`) to display `← Back to History` and preserve history filter state on return. |
| GAP-005 | MAJOR | CLOSED | Suboptimal database indexing for historical queries: `dbo.Issues` and `dbo.InspectionRuns` lack dedicated indexes on temporal lifecycle columns (`CreatedAtUtc`, `ResolvedAtUtc`, `AcknowledgedAtUtc`, `ReopenedAtUtc`, `ExecutionTimestampUtc`), causing full table scans under high-volume simulated telemetry. |
| GAP-006 | MAJOR | CLOSED | Missing consolidated v1.0 Quality Gate automated regression test suite: no dedicated suite asserting all 10 domain business rules (`BR-PRIMA-001`–`BR-PRIMA-010`) with zero skipped assertions, and no automated verification of frontend anti-pattern and cross-slice import guardrails. |

---

# 4. Open Questions

| ID | Status | Question | Impact |
|---|---|---|---|
| OQ-001 | CLOSED | For S-07 Period-Close Reliability KPIs, how should "Resolution Rate %" and "Total Issues" be computed for the selected date range? | Standardizes KPI aggregation formulas, accounting for closed issues versus incoming issue volume. |
| OQ-002 | CLOSED | For MTTA (Avg Time to Acknowledge) and MTTR (Avg Time to Resolve), how should the eligible issue cohorts and durations be calculated? | Governs event-completion cohort filtering, reopening turnaround measurement, and null representation. |
| OQ-003 | CLOSED | How should date range boundaries and presets (Today, This Week, This Month, Last Month, Custom) be anchored between frontend and backend? | Defines temporal API parameters, timezone alignment with `Asia/Jakarta` (WIB), and index seek efficiency. |
| OQ-004 | CLOSED | In the S-07 Historical Issue Ledger query, which timestamp column should the date range (fromUtc/toUtc) evaluate? | Prevents operational blind spots where issues resolved today but created earlier are omitted from period-close audit. |
| OQ-005 | CLOSED | How should S-02 Issue Detail Workbench know whether to render "← Back to History" versus "← Back to Dashboard" and preserve S-07 filter context? | Governs client-side route query contracts and seamless audit exploration UX. |
| OQ-006 | CLOSED | For SQL Server aggregation performance across high-volume simulated telemetry (1,000+ rows), what indexing strategy should be adopted for Stage 4? | Determines DbUp migration schema additions, covering index columns, and query execution plans. |
| OQ-007 | CLOSED | For S-07 Historical Issue Ledger query payload and table rendering, what result volume constraint should be enforced? | Governs query limits, browser memory safety, and virtual scrolling behavior. |
| OQ-008 | CLOSED | For the "v1.0 Quality Gate" milestone exit criterion, how should the end-to-end regression verification across all 10 Business Rules (BR-PRIMA-001 through BR-PRIMA-010) and frontend anti-patterns be structured? | Establishes regression test suite organization, automated gate execution, and audit verification. |

---

# 5. Assumptions

| ID | Assumption |
|---|---|
| ASM-001 | Stage 4 completes the functional scope of the PrIMA v1.0 Release Candidate (`S-00` through `S-07` and `M-01` through `M-03`), directly reusing established Minimal API, Dapper, Vue 3, Tailwind CSS v4, and PrimeVue v4 architectures. |
| ASM-002 | All operational timestamps are stored in UTC (`DATETIME2`) in SQL Server and displayed in `Asia/Jakarta` (`WIB`, UTC+7) on the frontend via `formatWibDateTime`. |
| ASM-003 | The `S-07 History & Reports` screen is strictly read-only; no mutation buttons, modals, or lifecycle status modifications exist on this screen. |
| ASM-004 | Visualizing Scope and Severity breakdown on `S-07` is accomplished exclusively using pure Tailwind CSS horizontal proportion bars; installing external charting libraries (`Chart.js`, `ECharts`) is prohibited per `PRIMA-UI-UX-GUIDELINE.md §10`. |
| ASM-005 | The existing `001_InitialSchema.sql`, `002_SeedInitialUsers.sql`, and `003_IssueActivityLogs.sql` migrations remain unmodified; new database indexes are introduced through a new DbUp migration `004_HistoryAndAnalyticsIndexes.sql`. |
| ASM-006 | Virtualized scrolling (`VirtualScroller` or virtualized `DataTable`, zero pagination) is enforced across `S-01`, `S-03`, `S-06`, and `S-07` to ensure all operational anomalies and historical audit logs remain accessible without page boundaries. |
| ASM-007 | Pilot SIMRS detector suites and hospital on-premise production rollout remain strictly deferred to Stage 5. |

---

# 6. Risks

| ID | Risk | Impact | Mitigation |
|---|---|---|---|
| RISK-001 | Full table scans on `dbo.Issues` and `dbo.InspectionRuns` causing slow KPI dashboard response times under high-volume telemetry. | `GetReliabilityKpi` times out or degrades backend performance during period-close reporting. | Deploy DbUp migration `004_HistoryAndAnalyticsIndexes.sql` with covering index on `InspectionRuns(ExecutionTimestampUtc)` and filtered indexes on `Issues(ResolvedAtUtc)`, `Issues(AcknowledgedAtUtc)`, `Issues(ReopenedAtUtc)`, and `Issues(CreatedAtUtc)`. |
| RISK-002 | Timezone misalignment between hospital officer calendar (WIB, UTC+7) and UTC database records. | Issues created or resolved near midnight WIB appear under the wrong calendar date in "Today" or "This Month" reports. | Compute exact WIB calendar start/end boundaries on the frontend (e.g., Today = 00:00:00 to 23:59:59.999 WIB converted to UTC) and pass explicit ISO `fromUtc` and `toUtc` parameters to the API (DEC-003). |
| RISK-003 | "Resolved Today" deep-link omitting issues resolved today that were created on a prior date. | EDP officers cannot audit all issues resolved during their shift, causing audit discrepancy. | Enforce status-aware date filtering (DEC-004): when status is `Resolved`, query evaluates `ResolvedAtUtc BETWEEN @FromUtc AND @ToUtc`. |
| RISK-004 | Loss of history filter context and navigation confusion when reviewing closed issues from S-07. | Clicking "Back" on `S-02` resets the officer's date range and filters on `S-07` or navigates to Dashboard. | Pass `?from=history` query param when navigating from `S-07` to `S-02`, display `← Back to History` in header, and return via `router.back()` (DEC-005). |
| RISK-005 | Massive payload memory bloat and DOM freeze on S-07 when querying wide date ranges. | Browser tab crashes or freezes when loading thousands of historical issues. | Enforce server-side limit clamping (`default: 500`, `max: 1000`) and render rows using zero-pagination virtual scroll (DEC-007). |
| RISK-006 | Regression drift across earlier stage business rules during final v1.0 release candidate delivery. | Changes in Stage 4 inadvertently break deduplication, reopening, or verification gate invariants. | Add dedicated `DomainBusinessRulesQualityGateTests.cs` verifying all 10 domain business rules (`BR-PRIMA-001`–`BR-PRIMA-010`) with zero skipped assertions (DEC-008). |
| RISK-007 | Violation of frontend anti-pattern guidelines (e.g., unintended pagination, external charts, or cross-slice imports). | Compromised architectural consistency and audit failure before pilot rollout. | Run an automated frontend compliance script/test verifying zero forbidden anti-patterns and zero cross-slice imports (DEC-008). |

---

# 7. Recommendations

The key design alternatives evaluated during stakeholder grilling are summarized below:

## 1. Resolution Rate Calculation (`OQ-001`)
- **Option A: Closed Issues Basis (Selected - DEC-001):** `Resolution Rate % = Count(Resolved in period) / (Count(Resolved in period) + Count(Dismissed in period)) * 100`. Aligns with `docs/PRIMA-SCREEN-INVENTORY.md §375` ("% resolved of closed") and directly measures remediation efficacy vs dismissals.
- **Option B: Total Issues Inflow Basis:** `Count(Resolved in period) / TotalIssuesCreated * 100`. Disadvantage: Long-running investigations artificially depress the rate during periods of high detector activity.

## 2. MTTA & MTTR Measurement (`OQ-002`)
- **Option A: Event-Completion Cohort + Reopening Turnaround (Selected - DEC-002):** Evaluates issues completed in the period. MTTR measures turnaround duration from latest reopening (`COALESCE(ReopenedAtUtc, CreatedAtUtc) -> ResolvedAtUtc`). Accurately captures operational cycle speed.
- **Option B: Total Lifecycle Duration:** Strictly measures from original `CreatedAtUtc`. Disadvantage: Chronic issues skew MTTR even if resolved quickly in their latest recurrence.

## 3. Date Boundary & Timezone Anchoring (`OQ-003`)
- **Option A: Frontend WIB Boundary Calculation (Selected - DEC-003):** Frontend converts WIB calendar presets to explicit UTC ISO strings. Backend remains cleanly timezone-agnostic and executes optimal SQL index seeks.
- **Option B: Server-Side Timezone Evaluation:** Backend parses preset strings and resolves `TimeZoneInfo`. Disadvantage: Duplicates calendar logic on the server and limits custom range flexibility.

---

# 8. Gap Closure Decisions

| Decision ID | Target GAP / OQ | Decision | Rationale | Impact | Architecture Impact | Resolved By | Resolved Date |
|---|---|---|---|---|---|---|---|
| DEC-001 | GAP-001, OQ-001 | **Closed-Issues Basis for Resolution Rate & Creation Basis for Total Issues:** Total Issues = `COUNT(CreatedAtUtc BETWEEN @FromUtc AND @ToUtc)`. Resolution Rate % = `COUNT(Resolved in period) / (COUNT(Resolved in period) + COUNT(Dismissed in period)) * 100`. If 0 closed, return 0.0%. | Aligns with `PRIMA-SCREEN-INVENTORY.md §375`; accurately reflects remediation success against false positives and dismissals. | Clear, unambiguous operational KPI for management period-close reporting. | Implement aggregation logic in `GetReliabilityKpiHandler.cs`. | Drury / User | 2026-11-21 |
| DEC-002 | GAP-001, OQ-002 | **Event-Completion Cohort + Reopening Turnaround for MTTA/MTTR:** MTTA averages `DATEDIFF_BIG(second, CreatedAtUtc, AcknowledgedAtUtc)` for issues acknowledged in period. MTTR averages `DATEDIFF_BIG(second, COALESCE(ReopenedAtUtc, CreatedAtUtc), ResolvedAtUtc)` for issues resolved in period. Returned as decimal hours (or null if count=0, displayed as `—`). | Evaluates actual work completed in the period and reflects turnaround efficiency on recurred issues without distortion from initial creation date. | Reliable MTTA/MTTR metrics in `font-mono tabular-nums`. | Implement SQL calculation using `AVG(CAST(DATEDIFF_BIG(...) AS FLOAT)) / 3600.0` in `GetReliabilityKpiHandler`. | Drury / User | 2026-11-21 |
| DEC-003 | GAP-001, GAP-002, OQ-003 | **Frontend WIB-to-UTC Boundary Calculation:** API endpoints accept explicit ISO `fromUtc` and `toUtc` parameters. Frontend helper in `dateUtils.ts` computes exact `Asia/Jakarta` (WIB) calendar boundaries (00:00:00 to 23:59:59.999 WIB) and converts them to UTC. | Keeps backend SQL queries clean and fast with index seeks on UTC `DATETIME2` columns, while ensuring calendar alignment for Indonesian hospital operations. | Clean separation of concerns; backend remains timezone-agnostic. | Add `getWibDateRange(preset)` helper in `src/shared/utils/dateUtils.ts`. | Drury / User | 2026-11-21 |
| DEC-004 | GAP-002, OQ-004 | **Status-Aware Lifecycle Date Filtering:** In `GetHistoricalIssues`, if status is `Resolved`, filter by `ResolvedAtUtc BETWEEN @FromUtc AND @ToUtc`. If `Dismissed`, filter by `DismissedAtUtc`. If `Reopened`, filter by `ReopenedAtUtc`. If `All` or active statuses, filter by `CreatedAtUtc`. | Guarantees that clicking "Resolved Today" shows all issues resolved today, even if originally detected days earlier, preventing audit blind spots. | Complete audit visibility into shift closures. | Implement dynamic SQL filter clauses in `GetHistoricalIssuesHandler.cs`. | Drury / User | 2026-11-21 |
| DEC-005 | GAP-004, OQ-005 | **Query Param Origin Tracking for S-02 Back Navigation:** S-07 appends `?from=history` when routing to `S-02` (`/issues/:id?from=history`). `WorkbenchHeader.vue` inspects `route.query.from === 'history'` to dynamically display `← Back to History` (instead of `Back to Dashboard`) and returns via `router.back()` (or `router.push('/history')` as fallback), preserving S-07 filter and date state. | Eliminates navigation confusion and preserves officer context during period-close audits without requiring persistent Pinia stores. | Seamless exploration across S-07 and S-02. | Extend `WorkbenchHeader.vue` and `IssueDetailView.vue` with `origin` computed property. | Drury / User | 2026-11-21 |
| DEC-006 | GAP-005, OQ-006 | **DbUp 004 Covering & Filtered Indexes:** Introduce `004_HistoryAndAnalyticsIndexes.sql` adding: (1) `IX_InspectionRuns_ExecutionTimestampUtc` on `dbo.InspectionRuns(ExecutionTimestampUtc DESC) INCLUDE (Outcome)` for detector pass rate queries; (2) `IX_Issues_CreatedAtUtc` on `dbo.Issues(CreatedAtUtc DESC) INCLUDE (Scope, CurrentSeverity, BaseSeverity, Status)`; (3) Filtered indexes `IX_Issues_ResolvedAtUtc`, `IX_Issues_AcknowledgedAtUtc`, and `IX_Issues_ReopenedAtUtc` with `WHERE ... IS NOT NULL`. | Guarantees index seeks/covering index scans with zero table scans on KPI aggregates and historical ledger queries under high-volume simulated telemetry. | Sub-second query response times under 1,000+ rows. | Add Migration `004_HistoryAndAnalyticsIndexes.sql` executed at backend startup. | Drury / User | 2026-11-21 |
| DEC-007 | GAP-002, GAP-003, OQ-007 | **Clamped Result Limit (Default 500, Max 1000) & Virtualized Scrolling:** `GetHistoricalIssues` clamps `limit` between 1 and 1000 (default 500) to protect backend memory and network transfer, while `S-07` renders rows via zero-pagination virtualized scrolling (`VirtualScroller` / virtualized `DataTable`). | Prevents browser DOM exhaustion and network bloat while strictly adhering to the zero-pagination anti-pattern rule. | Smooth scrolling under large datasets. | Implement limit clamping in backend handler and virtual scrolling in `HistoryIssueTable.vue`. | Drury / User | 2026-11-21 |
| DEC-008 | GAP-006, OQ-008 | **Dedicated Quality Gate Test Suites:** Create `DomainBusinessRulesQualityGateTests.cs` in `PrIMA.Api.Tests` explicitly asserting all 10 domain business rules (`BR-PRIMA-001` through `BR-PRIMA-010`) with zero skipped tests. Add an automated frontend audit script/test verifying zero forbidden anti-patterns (no pagination, no external charts, no browser confirms/alerts) and zero cross-slice imports. | Provides bulletproof automated verification of the complete system before pilot deployment, ensuring no regression across all 10 invariants. | 100% confidence for Feature-Complete v1.0 Release Candidate milestone. | Add backend quality gate test suite and frontend static/unit audit test. | Drury / User | 2026-11-21 |

---

# 9. Architecture Applicability

**Decision:** `ARCHITECTURE-REQUIRED`

**Rationale:**
Stage 4 introduces formal technical target-state requirements across the full stack:
1. **New Backend Vertical Slices:** Addition of `Features/History/GetReliabilityKpi` and `Features/History/GetHistoricalIssues` with specialized Dapper CQRS aggregation queries.
2. **Database Schema & Indexing Evolution:** Addition of DbUp migration `004_HistoryAndAnalyticsIndexes.sql` with covering and filtered indexes optimizing aggregation performance without degrading ingestion write throughput.
3. **New Frontend Feature Slice:** Implementation of `src/features/history/` (`S-07`), including Date Range presets, 6-Tile KPI grid, 2-Column pure CSS proportion bars, and virtualized issue audit table.
4. **Cross-Feature UX Linkage:** Deep-link navigation contract between `S-01` (`Resolved Today`) and `S-07`, and origin-aware contextual back navigation between `S-07` and `S-02`.
5. **Quality Gate Infrastructure:** Formalization of the consolidated 10-business-rule automated regression harness and frontend anti-pattern static verification.

A formal Target Architecture Update (`ica-architect`) is required before implementation planning can proceed.

---

# 10. Planning Readiness

### Readiness Checklist

- [x] All 6 critical and major architectural gaps identified and resolved.
- [x] All 8 open questions grilled, analyzed, and closed with recorded stakeholder decisions (`DEC-001` through `DEC-008`).
- [x] "Total Issues" and "Resolution Rate %" formulas formally standardized on closed-issues basis (`DEC-001`).
- [x] MTTA and MTTR cohort boundaries and reopening turnaround metrics finalized (`DEC-002`).
- [x] Date range boundary computation and `Asia/Jakarta` (WIB) timezone anchoring established (`DEC-003`).
- [x] Status-aware lifecycle date filtering specified for historical queries (`DEC-004`).
- [x] Navigation origin contract (`?from=history`) and contextual `← Back to History` button defined (`DEC-005`).
- [x] DbUp covering and filtered indexes designed for high-volume SQL Server performance (`DEC-006`).
- [x] Clamped query limits (`default: 500`, `max: 1000`) and virtual scrolling specified (`DEC-007`).
- [x] Full-Stack v1.0 Quality Gate test suite structure and anti-pattern verification defined (`DEC-008`).
- [x] Architecture Applicability established as `ARCHITECTURE-REQUIRED`.

### Artifact Status

```text
Status: READY-FOR-PLANNING
```

The feasibility assessment is fully resolved and complete. All architectural and business questions are closed; downstream architecture and planning can proceed without inventing decisions.
