---
Title: PrIMA Stage 4 — Historical Reliability Analytics, Reporting & v1.0 Quality Gate Test Package
Code: PRIMA-STAGE-4
Artifact: TEST-PACKAGE
Version: 1.0
LastUpdated: 2026-11-22
Status: READY-FOR-TESTING
---

# 1. Overview & Purpose

This test package provides human-executable verification procedures for **PrIMA Stage 4: Historical Reliability Analytics, Reporting & v1.0 Quality Gate** (`Feature-Complete PrIMA v1.0 Release Candidate` milestone).

The test suite validates the integrated system as a whole against the live running application, covering:
1. **Period-Close Reliability Reporting (`S-07`, `H-1`):** Retrospective operational health summary for any selected date range, featuring the 6 period-close KPI metrics (`Total Issues`, `Resolution Rate %`, `Avg. Time to Acknowledge (MTTA)`, `Avg. Time to Resolve (MTTR)`, `Detector Pass Rate %`, and `Reopened Issues` count) and 2-column Scope/Severity proportion distributions.
2. **Operational Calendar & Regional Boundaries (`dateUtils.ts`, `DEC-003`):** Quick date preset bar (`Today`, `This Week`, `This Month`, `Last Month`, `Custom` + range picker) anchored strictly to Indonesian hospital business hours in `Asia/Jakarta` (`WIB`, UTC+7) translated to UTC ISO boundaries.
3. **Pure CSS Proportion Breakdown (`ScopeSeverityBreakdown.vue`, `PRIMA-UI-UX-GUIDELINE.md §10`):** Proportion distribution bars built exclusively with native Tailwind CSS styling without external charting libraries (`Chart.js`, `ECharts`).
4. **Historical Issue Ledger Auditing (`S-07`, `H-2`, `DEC-004`, `DEC-007`):** Parameterized historical ledger query with status-aware lifecycle date filtering (`ResolvedAtUtc`, `DismissedAtUtc`, `ReopenedAtUtc`, `CreatedAtUtc`), multi-attribute filtering (Status, Scope, Detector, Severity, Search), server-enforced clamped limits (default 500, max 1,000), and zero-pagination virtualized scrolling (`DataTable` virtual scroller).
5. **Cross-Feature Operational Deep Navigation (`S-01` → `S-07` and `S-07` → `S-02`, `DEC-005`):** Active `Resolved Today` tile on the Issue Dashboard routing to `/history?preset=Today&status=Resolved`, and origin-aware `?from=history` query tracking on `S-02` rendering `← Back to History` and restoring S-07 date and filter states via `router.back()`.
6. **Application Shell & Milestone Branding (`AppLayout.vue`):** Active `History & Reports` sidebar menu item (`/history`) and updated milestone badges displaying `v1.0-rc` (Release Candidate) across header and footer.
7. **Database Indexing Optimization (`004_HistoryAndAnalyticsIndexes.sql`, `DEC-006`):** Sub-second query performance guaranteed via covering index on `dbo.InspectionRuns` and filtered indexes on `dbo.Issues` (`ResolvedAtUtc`, `AcknowledgedAtUtc`, `ReopenedAtUtc`, `DismissedAtUtc`).
8. **Consolidated Domain Business Rules Quality Gate (`DEC-008`):** Verification across all 10 domain business rules (`BR-PRIMA-001` through `BR-PRIMA-010`) asserting deduplication, reopening, dynamic escalation, verification gates, synthetic infrastructure issues, dismissal rationales, webhook timeouts, officer attribution, and silence thresholds with zero skipped assertions.
9. **Frontend Static Compliance Audit (`audit:compliance`, `DEC-008`):** Static verification ensuring zero paginator props, zero external charting libraries, zero native alerts/confirms, and zero cross-slice feature imports.
10. **RFC 7807 Error Handling & Boundary Precision (`F-P2S03-01`, `DEC-003`):** Sub-millisecond boundary precision avoiding next-day midnight record leaks, and structured ProblemDetails responses for `400 Bad Request`, `422 Unprocessable Entity`, and `401 Unauthorized`.

---

# 2. Test Environment & Prerequisites

### 2.1 System Services
Ensure the following services are running before executing tests:
- **Database:** Microsoft SQL Server 2022 (Local instance accessible via connection string configured in `src/backend/PrIMA.Api/appsettings.Development.json`).
- **Backend API (`PrIMA.Api`):**
  - Run command: `dotnet run --project src/backend/PrIMA.Api/PrIMA.Api.csproj`
  - URL: `http://localhost:5005` (or configured port in `launchSettings.json`).
  - Swagger UI: `http://localhost:5005/swagger/index.html`.
- **Frontend SPA (`PrIMA.Client`):**
  - Run command: `npm --prefix src/frontend run dev`
  - URL: `http://localhost:5173` (Vite dev server with `/api` proxy pointing to backend).

### 2.2 Seeded Officer Accounts
The database migration automatically seeds the following operational accounts:

| Username | Password | Full Name | Role | Active |
|---|---|---|---|---|
| `ahmad.budi` | `Hospital@2026!` | Ahmad Budi | `EdpOfficer` | Yes |
| `siti.rahma` | `Hospital@2026!` | Siti Rahma | `SystemAnalyst` | Yes |
| `edp.lead` | `Hospital@2026!` | Joko Santoso | `Lead` | Yes |

### 2.3 Pre-seeded Stage 4 Demo Fixtures
When `SeedDemoData == true` (default in Development mode), `DevelopmentDemoDataSeeder.cs` idempotently seeds rich operational and historical cohorts across multiple timeframes:

#### Pre-seeded Stage 4 Historical Reporting Cohorts:
| Entity Reference / Key | Status | Scope | Module / Detector | Severity | Resolved / Lifecycle Date | Key Operational Characteristic |
|---|---|---|---|---|---|---|
| `LIS-2026-RESOLVED-TODAY` | `Resolved` | `ProcessIntegrity` | Laboratory Result Dispatch Monitor | `Medium` | Today (WIB) ~10m ago | Resolved today by `siti.rahma`; clean verification recorded. Target for `Resolved Today` deep link. |
| `RX-2026-RESOLVED-LW1` | `Resolved` | `DataIntegrity` | Prescription Dispense Stock Checker | `Medium` | 7 days ago | Resolved last week by `ahmad.budi`; contributes to `This Month` / `Last Month` MTTR. |
| `REPL-2026-RESOLVED-LW2` | `Resolved` | `Infrastructure` | EMR Database Replication Lag Monitor | `Critical` | 10 days ago | Critical infrastructure issue resolved last week by `ahmad.budi`. |
| `BILL-2026-DISMISSED-LW1` | `Dismissed` | `DataIntegrity` | Billing Journal Balance Checker | `Low` | 9 days ago | Dismissed last week by `siti.rahma` with valid justification (>= 20 chars). Contributes to resolution rate denominator (`DEC-001`). |
| `BILL-2026-REOPENED-TODAY` | `Reopened` | `DataIntegrity` | Billing Journal Balance Checker | `High` | Today (WIB) ~15m ago | Recurring journal imbalance reopened today; demonstrates `BR-PRIMA-002` superseding of prior records. |
| `ADM-2026-REOPENED-LW1` | `Reopened` | `ProcessIntegrity` | Patient Admission Discharge Sync | `Medium` | 8 days ago | Admission mismatch reopened last week; contributes to reopened issues counts. |
| `LIS-2026-HISTORICAL-OPEN` | `Open` | `ProcessIntegrity` | Laboratory Result Dispatch Monitor | `Low` | 5 days ago | Historical open issue awaiting EDP triage. |

#### Pre-seeded Stage 1–3 Baseline Operational Issues:
| Entity Reference / Key | Status | Severity | Detector Name | Key Characteristics |
|---|---|---|---|---|
| `INV-2026-RESOLVED-001` | `Resolved` | `Medium` | Billing Journal Balance Checker | Baseline resolved issue with clean verification. |
| `ADM-2026-DISMISSED-001` | `Dismissed` | `Low` | Patient Admission Discharge Sync | Baseline dismissed issue with valid rationale. |
| `BILL-2026-OPEN-001` | `Open` | `High` | Billing Journal Balance Checker | Primary active open issue. |
| `RX-202611-042` | `Acknowledged` | `Medium` | Prescription Stock Deductor | Active issue under investigation notes. |
| `LIS-QUEUE-4412` | `Investigating` | `Medium` | Laboratory Order Sync Prober | Remediation recorded; awaiting verification. |
| `EMR-DB-REPL02` | `Investigating` | `High` | EMR Database Replication Monitor | Remediation recorded; failed verification recorded. |
| `SEP-202611-9982` | `Investigating` | `Critical` | Patient Admission Discharge Sync | Remediation recorded; clean verification recorded. Ready to resolve. |
| `RX-ESCALATED-099` | `Open` | `Critical` | Prescription Stock Deductor | Unacknowledged issue escalated to Critical (`EscalationLevel = 2`). |
| `FAIL:b6a9c023-4d5e...` | `Open` | `High` | EMR Database Replication Monitor | Synthetic execution failure infrastructure issue (`BR-PRIMA-005`). |
| `SILENT:e2c04068-8314...` | `Open` | `High` | Patient Admission Discharge Sync | Synthetic silence threshold breach issue (`BR-PRIMA-010`). |
| `BED-LEGACY-001` | `Investigating` | `Medium` | Legacy Bed Tracking Bridge | Active issue under retired detector (`DEC-009`). |

#### Pre-seeded Inspection Runs Cohort:
- **Today's Runs:** 7 runs recorded today for `Billing Journal Balance Checker` and `Prescription Stock Deductor` (6 `Passed`, 1 `ExecutionFailed`).
- **30-Day Historical Runs:** 65 historical runs across the past 30 days for `Billing Journal Balance Checker` maintaining >90% clean pass rate.

### 2.4 Index Migration & Verification Tooling
To verify that DbUp migration `004_HistoryAndAnalyticsIndexes.sql` successfully deployed the covering and filtered indexes:
```sql
SELECT name, type_desc, is_unique, filter_definition
FROM sys.indexes
WHERE object_id = OBJECT_ID('dbo.Issues') OR object_id = OBJECT_ID('dbo.InspectionRuns')
ORDER BY name;
```

---

# 3. Test Execution Sequence

Testers should follow this recommended execution order:

```text
Section 4: Automated Quality Gate (TC-001 to TC-004)
   ↓
Section 5: Application Shell & Global Navigation Upgrades (TC-01-01 to TC-01-02)
   ↓
Section 6: Operational Calendar & Date Range Presets (TC-02-01 to TC-02-03)
   ↓
Section 7: Period-Close Reliability KPI Grid (TC-03-01 to TC-03-06)
   ↓
Section 8: Scope & Severity Distribution Analytics (TC-04-01 to TC-04-04)
   ↓
Section 9: Historical Issue Ledger Auditing & Multi-Attribute Filtering (TC-05-01 to TC-05-06)
   ↓
Section 10: Cross-Feature Deep Navigation & Context Preservation (TC-06-01 to TC-06-04)
   ↓
Section 11: Consolidated Domain Business Rules Quality Gate (TC-07-01 to TC-07-05)
   ↓
Section 12: Edge Cases, Boundary Conditions & RFC 7807 Error Handling (TC-08-01 to TC-08-04)
   ↓
Section 13: Complete End-to-End Shift-Close Operational Smoke Test (TC-09-01)
```

---

# 4. Automated Quality Gate

## TC-001 — Backend Automated Domain & Regression Test Suite

- **Objective:** Verify that the automated backend test suite compiles and executes cleanly, covering domain invariant rules (`BR-PRIMA-001` through `BR-PRIMA-010`), CQRS history queries, Dapper indexes, and API controllers.
- **Preconditions:** Host has .NET 8 SDK installed; local SQL Server running.
- **Steps:**
  1. Open terminal in the repository root directory.
  2. Execute:
     ```powershell
     dotnet test src/backend/PrIMA.Api.Tests/PrIMA.Api.Tests.csproj --no-build
     ```
- **Expected Result:**
  - Test runner executes all unit and integration test fixtures.
  - Test summary reports 0 compilation errors.
  - Dedicated suites `DomainBusinessRulesQualityGateTests` (10 tests), `HistoryKpiEndpointTests` (9 tests), `HistoricalIssuesEndpointTests` (9 tests), and `EndToEndStage4ReliabilityReportingSmokeTests` (1 test) pass 100% cleanly.

---

## TC-002 — Frontend Static Compliance Audit (`audit:compliance`, `DEC-008`)

- **Objective:** Verify that the frontend codebase strictly adheres to `PRIMA-UI-UX-GUIDELINE.md §10` anti-patterns: zero paginator props, zero external charting libraries, zero native alerts/confirms, and zero cross-slice imports.
- **Preconditions:** Node.js v20+ or v22 LTS installed.
- **Steps:**
  1. Open terminal in the repository root directory.
  2. Execute:
     ```powershell
     npm --prefix src/frontend run audit:compliance
     ```
- **Expected Result:**
  - Script output displays:
    ```text
    === PrIMA Frontend Static Compliance Audit (DEC-008) ===
    Scanned files: 76 under src/frontend/src/
    - [no-paginator]           S-01/S-03/S-06/S-07 zero pagination props : 0 violations
    - [no-external-charts]     Zero external charting library imports    : 0 violations
    - [no-native-dialogs]      Zero window.alert() / window.confirm()      : 0 violations
    - [no-cross-slice-imports] Zero cross-slice feature imports            : 0 violations

    PASSED: 0 compliance violations found.
    ```
  - Exit code is `0`.

---

## TC-003 — Frontend Production Build & TypeScript Typecheck

- **Objective:** Verify that the frontend application compiles cleanly into production assets without TypeScript compiler (`vue-tsc -b`) or Vite bundler errors.
- **Preconditions:** Node.js v20+ or v22 LTS installed.
- **Steps:**
  1. Open terminal in the repository root directory.
  2. Execute:
     ```powershell
     npm --prefix src/frontend run build
     ```
- **Expected Result:**
  - `vue-tsc -b` completes with 0 errors.
  - Vite bundles all production chunks into `src/frontend/dist/` successfully with 0 errors (including `HistoryView` chunk).
  - Exit code is `0`.

---

## TC-004 — OpenAPI Specification & Contract Synchronization Verification

- **Objective:** Verify that the OpenAPI Swagger endpoint exposes both Stage 4 history endpoints (`GET /api/history/reliability-kpi` and `GET /api/history/issues`) and strongly typed contracts are fully synchronized in `api.generated.ts`.
- **Preconditions:** Backend running at `http://localhost:5005`.
- **Steps:**
  1. Open browser and navigate to `http://localhost:5005/swagger/index.html`.
  2. Locate the **History** tag group in Swagger UI.
  3. Expand `GET /api/history/reliability-kpi`.
  4. Expand `GET /api/history/issues`.
  5. Inspect the response schemas.
- **Expected Result:**
  - **History** tag group exists and exposes:
    - `GET /api/history/reliability-kpi` with parameters `fromUtc` and `toUtc` (required `DateTime`), returning `200 OK` (`ReliabilityKpiResponse`).
    - `GET /api/history/issues` with parameters `fromUtc`, `toUtc`, `status`, `scope`, `detectorId`, `severity`, `search`, `limit`, returning `200 OK` (`HistoricalIssuesResponse`).
  - Schemas document RFC 7807 ProblemDetails for `400 Bad Request`, `422 Unprocessable Entity`, and `401 Unauthorized`.

---

# 5. Test Suite: Application Shell & Global Navigation Upgrades (`AppLayout.vue`)

## TC-01-01 — Global Navigation Sidebar "History & Reports" Menu Item & Active Router Styling

- **Objective:** Verify that the primary sidebar displays an active, clickable `History & Reports` navigation link that smoothly routes to `/history` without disabled tooltips.
- **Preconditions:** Authenticated as `ahmad.budi`.
- **Steps:**
  1. Open browser and navigate to `http://localhost:5173/`.
  2. Inspect the left navigation sidebar.
  3. Locate the `History & Reports` menu item (chart/history clock icon).
  4. Verify the item is not visually greyed out or showing a "Coming in Stage 4" tooltip.
  5. Click the `History & Reports` menu item.
- **Expected Result:**
  - Sidebar item is active and clickable.
  - Browser URL smoothly updates to `http://localhost:5173/history`.
  - The `History & Reports` sidebar menu item displays active state styling (blue/indigo highlight background and border indicator).
  - Page header displays: `"Historical Reliability & Reports"` with calendar range subtitle.

---

## TC-01-02 — Release Candidate Version Badges (`v1.0-rc`) in Shell Header and Footer

- **Objective:** Verify that the application shell reflects the `v1.0-rc` (Release Candidate) milestone version branding in both the header and the sidebar/footer.
- **Preconditions:** On `http://localhost:5173/history`.
- **Steps:**
  1. Inspect the top application header beside the PrIMA logo.
  2. Inspect the bottom-left sidebar footer area.
- **Expected Result:**
  - The top header renders a prominent milestone badge: `v1.0-rc` (replacing earlier stage tags).
  - The footer/sidebar renders: `PrIMA v1.0-rc (Release Candidate)`.

---

# 6. Test Suite: Operational Calendar & Date Range Presets (`DateRangePresetBar.vue`, `dateUtils.ts`, `DEC-003`)

## TC-02-01 — Operational Calendar Quick Presets (`Today`, `This Week`, `This Month`, `Last Month`) & Dynamic WIB Subtitle

- **Objective:** Verify that clicking date presets updates the active preset styling, re-computes UTC ISO boundaries respecting `Asia/Jakarta` (WIB, UTC+7), and updates the period description subtitle.
- **Preconditions:** Authenticated as `ahmad.budi`; on `http://localhost:5173/history`.
- **Steps:**
  1. Observe the top **Date Range Preset Bar** buttons: `Today`, `This Week`, `This Month`, `Last Month`, `Custom`.
  2. Verify that `This Month` is selected by default.
  3. Click the **Today** preset button.
  4. Observe the period subtitle below the page title.
  5. Click the **This Week** preset button.
  6. Click the **Last Month** preset button.
  7. Click the **This Month** preset button.
- **Expected Result:**
  - Clicking each preset button applies an active filled button style (`bg-blue-600 text-white`).
  - Period subtitle dynamically displays the local WIB calendar range (e.g., `"Period: 22 Nov 2026 00:00 WIB – 22 Nov 2026 23:59 WIB"` for Today).
  - Network panel confirms background queries trigger with exact UTC ISO bounds:
    - Today: `...T17:00:00.000Z` (previous UTC day) to `...T16:59:59.999Z` (current UTC day).
  - KPI tiles and historical table immediately refetch and update.

---

## TC-02-02 — Custom Date Range Selection via Inline DatePicker & Range Boundary Handling

- **Objective:** Verify that selecting the `Custom` preset reveals an inline date picker, allowing selection of arbitrary start and end dates with clean validation.
- **Preconditions:** On `http://localhost:5173/history`.
- **Steps:**
  1. In the preset bar, click the **Custom** button.
  2. Verify that an inline range `DatePicker` appears beside the button group.
  3. Click the date picker and select a 7-day range (e.g., from 1st to 7th of the current month).
  4. Observe the page subtitle and data queries.
- **Expected Result:**
  - The date picker input displays the chosen formatted date range (e.g., `01 Nov 2026 - 07 Nov 2026`).
  - Period subtitle updates to reflect the chosen custom dates.
  - Queries dispatch with `fromUtc` anchored to start-of-day WIB (`00:00:00.000`) and `toUtc` anchored to end-of-day WIB (`23:59:59.999`).
  - KPI grid and issue table refresh with records from that custom timeframe.

---

## TC-02-03 — Reactive Query Invalidation & Stale-Time Cache Behavior (30s Cache)

- **Objective:** Verify that query data is cached with a 30-second stale time, and clicking the header **Refresh** button forces a background cache invalidation and refetch.
- **Preconditions:** On `http://localhost:5173/history`.
- **Steps:**
  1. Toggle between `Today` and `This Month`.
  2. Observe network requests in browser Developer Tools.
  3. Quickly toggle back to `Today` within 5 seconds.
  4. Observe network requests.
  5. Click the manual **"Refresh"** button in the header toolbar.
- **Expected Result:**
  - Toggling back to `Today` within 30 seconds serves cached data immediately without refetching (`staleTime: 30_000`).
  - Clicking the **Refresh** button invalidates TanStack Query keys `['history', 'kpi', ...]` and `['history', 'issues', ...]`, spinning the refresh icon and refetching fresh data from the server.

---

# 7. Test Suite: Period-Close Reliability KPI Grid (`ReliabilityKpiGrid.vue`, `DEC-001`, `DEC-002`)

## TC-03-01 — Total Issues Created Metric & Monospace Typography

- **Objective:** Verify that Tile 1 accurately displays total issues created in the active period with monospace numbers (`font-mono tabular-nums`).
- **Preconditions:** Authenticated as `ahmad.budi`; on `http://localhost:5173/history`.
- **Steps:**
  1. Click **Today** in the preset bar.
  2. Inspect **Tile 1: Total Issues Created**.
  3. Click **This Month** in the preset bar.
  4. Inspect **Tile 1**.
- **Expected Result:**
  - Tile 1 displays label `"Total Issues Created"` with an information tooltip explaining `"All issues first detected within this operational calendar window"`.
  - Numeric count is formatted in bold monospace typography (`font-mono font-bold text-2xl tabular-nums`).
  - Count reflects the sum of issues created in that timeframe (e.g., `>= 1` for Today, `>= 10` for This Month).

---

## TC-03-02 — Closed-Issues Resolution Rate % (`DEC-001`) & Dynamic Color Coding

- **Objective:** Verify that Tile 2 computes the Resolution Rate based strictly on the closed-issues cohort (`Resolved / (Resolved + Dismissed) * 100`) per `DEC-001`, displays dynamic color coding, and returns `0.0%` if no issues were closed.
- **Preconditions:** On `http://localhost:5173/history`.
- **Steps:**
  1. Select **This Month** preset.
  2. Inspect **Tile 2: Resolution Rate**.
  3. Observe the percentage value and the color of the text/badge.
  4. Check the formula components: `ResolvedCount` and `DismissedCount` in the historical table.
- **Expected Result:**
  - If `Resolved = 3` and `Dismissed = 1`, Resolution Rate is calculated as `3 / (3 + 1) * 100 = 75.0%`.
  - Percentage is formatted to 1 decimal place with a `%` suffix (`font-mono tabular-nums`).
  - Dynamic color coding conforms to threshold:
    - `>= 90.0%`: Emerald / green styling.
    - `< 90.0%`: Amber / orange styling.
  - Subtitle or tooltip indicates: `"Resolved in period / (Resolved + Dismissed)"`.
  - If a period has 0 resolved and 0 dismissed issues, Tile 2 safely returns `0.0%` (not `NaN%` or `null`).

---

## TC-03-03 — Average Time to Acknowledge (MTTA) in Decimal Hours & Null Placeholder (`—`)

- **Objective:** Verify that Tile 3 displays MTTA in decimal hours (`X.X h`) for issues acknowledged in the period (`DEC-002`), and renders `—` when no issues were acknowledged.
- **Preconditions:** On `http://localhost:5173/history`.
- **Steps:**
  1. Select **This Month** preset.
  2. Inspect **Tile 3: Avg. Time to Acknowledge (MTTA)**.
  3. Switch to a future or custom date range where no issues were acknowledged.
  4. Inspect **Tile 3**.
- **Expected Result:**
  - For periods with acknowledged issues: MTTA displays a formatted decimal number followed by `"h"` (e.g., `0.5 h` or `1.2 h`).
  - Tooltip explains: `"Average duration from issue detection to EDP acknowledgement across issues acknowledged in this window"`.
  - For periods with 0 acknowledged issues: displays an em-dash (`"—"`) instead of `0.0 h` or empty space.

---

## TC-03-04 — Average Time to Resolve (MTTR) with Reopening Turnaround (`DEC-002`) & Null Placeholder (`—`)

- **Objective:** Verify that Tile 4 displays MTTR in decimal hours (`X.X h`) for issues resolved in the period, measuring duration from latest reopening for reopened issues (`DEC-002`), and renders `—` when no issues were resolved.
- **Preconditions:** On `http://localhost:5173/history`.
- **Steps:**
  1. Select **Today** preset (which contains seeded `LIS-2026-RESOLVED-TODAY`).
  2. Inspect **Tile 4: Avg. Time to Resolve (MTTR)**.
  3. Select **This Month** preset.
  4. Inspect **Tile 4**.
  5. Select an empty custom range.
- **Expected Result:**
  - Today MTTR shows the turnaround time for today's resolved issues (e.g., `2.8 h`).
  - For reopened issues resolved in the period, the duration measures from `ReopenedAtUtc` to `ResolvedAtUtc` rather than original creation date (`DEC-002`).
  - Monospace formatting (`font-mono tabular-nums`).
  - For periods with 0 resolved issues: renders `"—"`.

---

## TC-03-05 — System-Wide Detector Clean Pass Rate % & Reliability Threshold Badging

- **Objective:** Verify that Tile 5 displays the proportion of inspection runs with outcome `Passed` across all detectors in the period, displaying emerald for `>= 98.0%` and rose for `< 98.0%`, and defaulting to `100.0%` if 0 runs occurred.
- **Preconditions:** On `http://localhost:5173/history`.
- **Steps:**
  1. Select **Today** preset.
  2. Inspect **Tile 5: Detector Pass Rate**.
  3. Select **This Month** preset.
  4. Inspect **Tile 5**.
- **Expected Result:**
  - For Today (6 Passed, 1 Failed): Pass rate is calculated as `6 / 7 * 100 = 85.7%`.
  - Text color is rose (`text-rose-600`) because pass rate is `< 98.0%`.
  - For This Month: High clean pass rate from historical runs displays emerald (`text-emerald-600`) if `>= 98.0%`.
  - Tooltip explains: `"Proportion of clean inspection runs across all active detectors"`.
  - If a timeframe has 0 runs, returns `100.0%`.

---

## TC-03-06 — Reopened Issues Count & Recurrence Alert Badge

- **Objective:** Verify that Tile 6 displays the count of issues reopened in the active period and renders an amber alert badge when count `> 0`.
- **Preconditions:** On `http://localhost:5173/history`.
- **Steps:**
  1. Select **Today** preset (which contains seeded `BILL-2026-REOPENED-TODAY`).
  2. Inspect **Tile 6: Reopened Issues**.
  3. Select an empty date range with 0 reopened issues.
  4. Inspect **Tile 6**.
- **Expected Result:**
  - For Today: Count shows `1` (or count of today's reopened issues).
  - An amber recurrence badge is rendered beside the count: `"⚠ Recurred"` or amber pill indicator.
  - Tooltip notes: `"Issues that recurred after prior resolution or dismissal"`.
  - For periods with 0 reopened issues: displays `0` with neutral styling and no alert badge.

---

# 8. Test Suite: Scope & Severity Distribution Analytics (`ScopeSeverityBreakdown.vue`, `PRIMA-UI-UX-GUIDELINE.md §10`)

## TC-04-01 — Pure CSS Horizontal Proportion Bars Rendering (Zero External Charting Libraries)

- **Objective:** Verify that the 2-column breakdown section renders horizontal proportion bars built entirely with native Tailwind CSS styling without importing external charting libraries.
- **Preconditions:** Authenticated as `ahmad.budi`; on `http://localhost:5173/history`.
- **Steps:**
  1. Scroll down to the **Distribution Breakdown** section below the KPI tiles.
  2. Verify the 2-column layout:
     - Left Column: **Monitoring Scope Breakdown**
     - Right Column: **Issue Severity Breakdown**
  3. Inspect the DOM elements using browser Developer Tools (F12).
  4. Verify the proportion bars are native HTML `<div>` elements with Tailwind classes (`h-2 rounded-full transition-all`).
- **Expected Result:**
  - Layout conforms to desktop 2-column grid (`grid grid-cols-1 lg:grid-cols-2 gap-6`).
  - No `<canvas>`, `<svg>` chart objects, or external chart library wrappers (`Chart.js`, `ECharts`, `Highcharts`) exist in the DOM.
  - Bars animate smoothly to their percentage widths (`transition-all duration-500`).

---

## TC-04-02 — Monitoring Scope Breakdown Proportions, Color Coding & Monospace Figures

- **Objective:** Verify that the Scope breakdown displays all 4 monitoring scopes (`DataIntegrity`, `ProcessIntegrity`, `DataSync`, `Infrastructure`) with accurate counts, percentages, and distinct color codes.
- **Preconditions:** On `http://localhost:5173/history`; preset set to `This Month`.
- **Steps:**
  1. Inspect the left column (**Monitoring Scope Breakdown**).
  2. Check each scope row: Name, Horizontal Bar, Count, Percentage.
  3. Verify the color coding:
     - `DataIntegrity`: Indigo bar (`bg-indigo-500`).
     - `ProcessIntegrity`: Purple bar (`bg-purple-500`).
     - `DataSync`: Cyan bar (`bg-cyan-500`).
     - `Infrastructure`: Emerald bar (`bg-emerald-500`).
- **Expected Result:**
  - Each scope row displays category name, filled progress bar matching proportion, count in bold monospace (`font-mono tabular-nums`), and percentage to 1 decimal place (`font-mono tabular-nums text-slate-500`).
  - The sum of percentages across all scopes equals ~100.0%.

---

## TC-04-03 — Severity Breakdown Proportions, Color Coding & Distribution Counts

- **Objective:** Verify that the Severity breakdown displays all 4 canonical severity levels (`Critical`, `High`, `Medium`, `Low`) with accurate proportions and matching severity colors.
- **Preconditions:** On `http://localhost:5173/history`; preset set to `This Month`.
- **Steps:**
  1. Inspect the right column (**Issue Severity Breakdown**).
  2. Check each severity row: Name, Horizontal Bar, Count, Percentage.
  3. Verify the color coding:
     - `Critical`: Rose bar (`bg-rose-600`).
     - `High`: Orange bar (`bg-orange-500`).
     - `Medium`: Amber bar (`bg-amber-500`).
     - `Low`: Blue bar (`bg-blue-500`).
- **Expected Result:**
  - Severity bars reflect distribution counts for issues created in the active period.
  - Colors strictly follow the PrIMA severity color system.
  - Counts and percentages are aligned in monospace tabular format.

---

## TC-04-04 — Empty Period Handling (0 Issues Created State)

- **Objective:** Verify that when a selected period has 0 issues created, both breakdown panels gracefully display an empty state banner rather than broken bars or dividing by zero.
- **Preconditions:** On `http://localhost:5173/history`.
- **Steps:**
  1. Switch to the **Custom** preset.
  2. Select a date range far in the future (e.g., year 2030).
  3. Observe the breakdown panels.
- **Expected Result:**
  - Left panel displays: `"No issues created in this timeframe."`
  - Right panel displays: `"No issues created in this timeframe."`
  - Zero console errors or layout shifts occur.

---

# 9. Test Suite: Historical Issue Ledger Auditing & Multi-Attribute Filtering (`HistoryIssueFilters.vue`, `HistoryIssueTable.vue`, `DEC-004`, `DEC-007`)

## TC-05-01 — Status-Aware Lifecycle Date Filtering (`Resolved`, `Dismissed`, `Reopened`, `All`, Active) (`DEC-004`)

- **Objective:** Verify that filtering by Status dynamically switches the database query predicate to the corresponding lifecycle timestamp (`ResolvedAtUtc`, `DismissedAtUtc`, `ReopenedAtUtc`, or `CreatedAtUtc`) per `DEC-004`.
- **Preconditions:** Authenticated as `ahmad.budi`; on `http://localhost:5173/history`; preset `This Month`.
- **Steps:**
  1. In the **Status** filter pill strip, click **Resolved** (emerald pill).
  2. Inspect the table rows returned.
  3. In the Status filter pill strip, click **Dismissed** (zinc pill).
  4. Inspect the table rows returned.
  5. In the Status filter pill strip, click **Reopened** (amber pill).
  6. Inspect the table rows returned.
  7. Click **All** (slate pill).
- **Expected Result:**
  - Clicking **Resolved**: Table evaluates `ResolvedAtUtc BETWEEN @FromUtc AND @ToUtc`. Only resolved issues are returned (e.g., `LIS-2026-RESOLVED-TODAY`, `RX-2026-RESOLVED-LW1`, `REPL-2026-RESOLVED-LW2`, `INV-2026-RESOLVED-001`). Column displays `Resolved Date` and `MTTR Duration`.
  - Clicking **Dismissed**: Table evaluates `DismissedAtUtc BETWEEN @FromUtc AND @ToUtc`. Only dismissed issues are returned (e.g., `BILL-2026-DISMISSED-LW1`, `ADM-2026-DISMISSED-001`). Column displays `Dismissed Date`.
  - Clicking **Reopened**: Table evaluates `ReopenedAtUtc BETWEEN @FromUtc AND @ToUtc`. Only issues reopened in this window are returned (`BILL-2026-REOPENED-TODAY`, `ADM-2026-REOPENED-LW1`).
  - Clicking **All**: Table evaluates `CreatedAtUtc BETWEEN @FromUtc AND @ToUtc`. All issues created in the period are returned regardless of current status.

---

## TC-05-02 — Multi-Attribute Dimension Filtering (Scope, Detector, Severity)

- **Objective:** Verify that the historical issue table can be simultaneously filtered across Scope, Detector, and Severity dropdowns.
- **Preconditions:** On `http://localhost:5173/history`; Status set to `All`.
- **Steps:**
  1. In the **Scope** dropdown, select `DataIntegrity`.
  2. In the **Severity** dropdown, select `High`.
  3. In the **Detector** dropdown, select `Billing Journal Balance Checker`.
  4. Observe the filtered table rows.
- **Expected Result:**
  - Table filters down to issues matching all three criteria simultaneously (e.g., `BILL-2026-OPEN-001` or `BILL-2026-REOPENED-TODAY`).
  - Row count in table header updates to reflect the matching count (e.g., `"Showing N historical issues"`).

---

## TC-05-03 — Debounced Instant Search across Title, Detector Name, and Correlation Key

- **Objective:** Verify that typing in the search input debounces by 300ms, searches across Title, Detector Name, and Correlation Key, and includes a one-click clear button.
- **Preconditions:** On `http://localhost:5173/history`; dropdown filters cleared.
- **Steps:**
  1. In the Search input box, type `Replication`.
  2. Observe the debounced network dispatch and table results.
  3. In the Search input box, type `LW1` (matching correlation key substring).
  4. Observe the results.
  5. Click the `×` clear button inside the search input.
- **Expected Result:**
  - Typing `Replication` filters table to show EMR replication issues (e.g., `REPL-2026-RESOLVED-LW2` and `EMR-DB-REPL02`).
  - Typing `LW1` filters table to show `RX-2026-RESOLVED-LW1` and `BILL-2026-DISMISSED-LW1`.
  - Search triggers automatically without requiring an Enter keypress.
  - Clicking `×` instantly clears the search text and restores all historical rows.

---

## TC-05-04 — Zero-Pagination Virtualized Scrolling (Zero Paginator, Smooth 64px Row Virtualization)

- **Objective:** Verify that the historical table renders rows using high-performance virtualized scrolling (`scrollable`, `scrollHeight="580px"`, `itemSize: 64`) with zero pagination controls per `PRIMA-UI-UX-GUIDELINE.md §10`.
- **Preconditions:** On `http://localhost:5173/history`; preset set to `This Month`.
- **Steps:**
  1. Inspect the bottom of the historical issue table.
  2. Verify there are NO pagination buttons, page numbers (`1`, `2`, `3`), or page size dropdowns (`10 / page`).
  3. Grab the vertical scrollbar and rapidly scroll up and down.
  4. Inspect the DOM in Developer Tools.
- **Expected Result:**
  - No `p-paginator` or pagination UI exists anywhere in the view.
  - Scrolling is completely smooth and responsive without browser lag or flickering.
  - DOM contains only visible row elements plus buffer rows (verifying PrimeVue virtual scrolling).

---

## TC-05-05 — Server Result Clamping Protection (Default 500, Max 1000) & Notice Badge (`DEC-007`)

- **Objective:** Verify that the historical ledger query enforces server-side result clamping (default 500, max 1,000) to protect browser memory, displaying a notification badge when results reach the clamp limit.
- **Preconditions:** Backend running with seeded historical data.
- **Steps:**
  1. Inspect the historical table header count badge.
  2. Open Developer Tools Network tab, inspect the request payload for `GET /api/history/issues`.
  3. Check the response body properties `totalReturned` and `clampedLimit`.
- **Expected Result:**
  - Network response contains `totalReturned` and `clampedLimit: 500` (or `1000` if custom limit specified).
  - Server SQL executes `OFFSET 0 ROWS FETCH NEXT @Limit ROWS ONLY` (`DEC-007`).
  - If returned rows equal the clamp limit, a subtle info pill is displayed: `"Showing maximum 500 records. Refine date range or filters for specific audits."`

---

## TC-05-06 — Filter Reset Action & Empty State Handling

- **Objective:** Verify that when filters yield 0 results, the table renders a friendly empty state illustration with a `"Reset Filters"` action that restores defaults.
- **Preconditions:** On `http://localhost:5173/history`.
- **Steps:**
  1. In the search box, type `NonExistentQueryXYZ123`.
  2. Observe the table body.
  3. Click the **"Reset Filters"** button in the empty state banner (or filter toolbar).
- **Expected Result:**
  - Table displays an empty state banner: `"No historical issues match the selected filter criteria."`
  - A primary `"Reset Filters"` button is visible.
  - Clicking `"Reset Filters"` clears search text, restores Status to `All`, and resets Scope/Detector/Severity dropdowns to `All`, redisplaying all historical records.

---

# 10. Test Suite: Cross-Feature Deep Navigation & Context Preservation (`DEC-005`)

## TC-06-01 — Dashboard "Resolved Today" Tile (`S-01`) Deep Link to `/history?preset=Today&status=Resolved`

- **Objective:** Verify that clicking Tile 5 (`Resolved Today`) on the active Issue Dashboard (`S-01`) navigates directly to `S-07` pre-filtered to `preset=Today` and `status=Resolved`.
- **Preconditions:** Authenticated as `ahmad.budi`; navigate to `http://localhost:5173/` (Dashboard `S-01`).
- **Steps:**
  1. Locate **Tile 5: Resolved Today** on the top summary counter bar.
  2. Verify that the tile is active and has hover styling (no disabled lock icon).
  3. Click the **Resolved Today** tile.
- **Expected Result:**
  - Browser URL immediately updates to `http://localhost:5173/history?preset=Today&status=Resolved`.
  - The `S-07` History screen opens.
  - In the Date Preset Bar, **Today** is highlighted as active.
  - In the Status filter pills, **Resolved** is selected.
  - Table displays only issues resolved today (including `LIS-2026-RESOLVED-TODAY`).

---

## TC-06-02 — Historical Issue Table Row Click Navigation to Read-Only Detail Workbench (`S-02`) with `?from=history`

- **Objective:** Verify that clicking any issue row (or `"Audit →"` action) in the historical table navigates to that issue's detail workbench (`S-02`) with query parameter `?from=history`.
- **Preconditions:** On `http://localhost:5173/history`.
- **Steps:**
  1. Locate the row for `LIS-2026-RESOLVED-TODAY` (`d0a10001-0000-0000-0000-000000000001`).
  2. Click the row (or the `"Audit →"` link on the far right).
- **Expected Result:**
  - Browser navigates to `http://localhost:5173/issues/d0a10001-0000-0000-0000-000000000001?from=history`.
  - The Issue Detail Workbench (`S-02`) loads with full issue context, telemetry evidence, investigation notes, remediation record, and verification certificate.

---

## TC-06-03 — Dynamic Contextual Header `← Back to History` and State Preservation (`router.back()`)

- **Objective:** Verify that `WorkbenchHeader.vue` evaluates `route.query.from === 'history'` to dynamically display `← Back to History` (instead of `Back to Dashboard`), and clicking it executes `router.back()`, preserving S-07 date presets and active filters.
- **Preconditions:** On `http://localhost:5173/issues/d0a10001-0000-0000-0000-000000000001?from=history`.
- **Steps:**
  1. Inspect the top-left navigation button in the Workbench sticky header.
  2. Verify the button text is: `← Back to History`.
  3. Click the `← Back to History` button.
- **Expected Result:**
  - Button explicitly reads `← Back to History`.
  - Clicking the button executes `router.back()`.
  - Browser navigates back to `http://localhost:5173/history?preset=Today&status=Resolved`.
  - All previously active filters (`preset=Today`, `status=Resolved`, custom search, or scroll position) are completely preserved without resetting.

---

## TC-06-04 — Read-Only Governance Protection on Closed Issues in Workbench

- **Objective:** Verify that when viewing closed issues (`Resolved` or `Dismissed`) audited from history, the Action Bar in `S-02` renders read-only status indicators without mutating buttons (`Acknowledge`, `Remediate`, `Verify`, `Resolve`).
- **Preconditions:** On `http://localhost:5173/issues/d0a10001-0000-0000-0000-000000000001?from=history` (Resolved issue).
- **Steps:**
  1. Inspect the Action Bar in the top-right corner of the workbench.
  2. Inspect the forms below (Investigation Notes, Remediation, Verification).
- **Expected Result:**
  - Action Bar displays a static status chip: `"✓ Resolved"` (emerald) and `"Audited by Siti Rahma"`.
  - No mutation buttons (`Mark Resolved`, `Dismiss`, `Record Remediation`) are rendered.
  - Governance records are presented in read-only audit cards with officer attribution and timestamps intact.

---

# 11. Test Suite: Consolidated Domain Business Rules Quality Gate (`DEC-008`, `BR-PRIMA-001` through `BR-PRIMA-010`)

## TC-07-01 — Deduplication & Reopening Lifecycle Integrity (`BR-PRIMA-001`, `BR-PRIMA-002`, `BR-PRIMA-009`)

- **Objective:** Verify that anomaly ingestion strictly adheres to domain rules: duplicate finding appends occurrences (#1, #2, #3) to the existing issue without creating duplicates (`BR-PRIMA-001`), recurring finding on closed issue reopens it, supersedes prior governance records, and resets assignee (`BR-PRIMA-002`), and historical occurrences remain immutable (`BR-PRIMA-009`).
- **Preconditions:** Test suite `DomainBusinessRulesQualityGateTests` accessible; local backend running.
- **Steps:**
  1. In terminal, execute:
     ```powershell
     dotnet test src/backend/PrIMA.Api.Tests/PrIMA.Api.Tests.csproj --no-build --filter "FullyQualifiedName~BR_PRIMA_001|FullyQualifiedName~BR_PRIMA_002|FullyQualifiedName~BR_PRIMA_009"
     ```
  2. In browser, navigate to `/issues/d0a30001-0000-0000-0000-000000000001` (`BILL-2026-REOPENED-TODAY`).
  3. Inspect the Activity Log and Governance tabs.
- **Expected Result:**
  - Automated tests pass with 0 failures (`3 passed`).
  - In browser: `BILL-2026-REOPENED-TODAY` reflects status `Reopened`.
  - Prior remediation/verification records display an amber warning badge: `"⚠ Superseded by Reopening (BR-PRIMA-002)"`.
  - Occurrences accordion displays sequential immutable occurrences (#1, #2) with original telemetry snapshots preserved intact.

---

## TC-07-02 — Dynamic SLA & Recurrence Escalation Rules (`BR-PRIMA-003`)

- **Objective:** Verify that unacknowledged `Open` issues past SLA timeout step up by +1 severity level, and issues accumulating 5 or more occurrences step up by +1 severity level, strictly capped at `Critical` (`BR-PRIMA-003`).
- **Preconditions:** Automated test suite accessible; backend running.
- **Steps:**
  1. In terminal, execute:
     ```powershell
     dotnet test src/backend/PrIMA.Api.Tests/PrIMA.Api.Tests.csproj --no-build --filter "FullyQualifiedName~BR_PRIMA_003"
     ```
  2. In browser, navigate to `/issues/d0999999-9999-9999-9999-999999999901` (`RX-ESCALATED-099`).
  3. Inspect header badges and activity log.
- **Expected Result:**
  - Automated test passes with 0 failures (`1 passed`).
  - In browser: `RX-ESCALATED-099` displays Severity `Critical` and Escalation badge `↑ Escalated (L2)`.
  - Activity log documents stepped escalation without exceeding `Critical`.

---

## TC-07-03 — Mandatory Verification Gate Enforcement (`BR-PRIMA-004`, `BR-PRIMA-006`)

- **Objective:** Verify that an issue cannot be resolved without a recorded remediation and clean verification (`CleanPassed = true`), returning 409 Conflict (`BR-PRIMA-004`), and dismissal requires a rationale of at least 20 characters (`BR-PRIMA-006`).
- **Preconditions:** Backend running.
- **Steps:**
  1. In terminal, execute:
     ```powershell
     dotnet test src/backend/PrIMA.Api.Tests/PrIMA.Api.Tests.csproj --no-build --filter "FullyQualifiedName~BR_PRIMA_004|FullyQualifiedName~BR_PRIMA_006"
     ```
  2. In browser, navigate to `/issues/d0444444-4444-4444-4444-444444444401` (`LIS-QUEUE-4412`, remediation recorded, but verification missing).
  3. Verify the `Mark Resolved` button state.
  4. Click `Dismiss Issue` and type a short rationale (e.g., `"Too short"` - 9 chars).
  5. Click `Confirm Dismissal`.
- **Expected Result:**
  - Automated tests pass with 0 failures (`2 passed`).
  - In browser: `Mark Resolved` button is disabled with tooltip: `"Verification required before resolution (BR-PRIMA-004)"`.
  - Dismissal modal displays client-side validation error: `"Rationale must be at least 20 characters"`.
  - Direct API attempt to resolve without clean pass returns `409 Conflict`.
  - Direct API attempt to dismiss with <20 chars returns `422 Unprocessable Entity`.

---

## TC-07-04 — Autonomous Silence Detection, Infrastructure Synthesis & Webhook Ergonomics (`BR-PRIMA-005`, `BR-PRIMA-007`, `BR-PRIMA-010`)

- **Objective:** Verify that a missed heartbeat transitions detector health to `Silent` after `2.0 * Cadence` and creates `SILENT:{id}` (`BR-PRIMA-005`, `BR-PRIMA-010`), failed probe creates `FAIL:{id}`, and webhook client enforces a strict 15-second timeout returning 504 ProblemDetails (`BR-PRIMA-007`).
- **Preconditions:** Backend running.
- **Steps:**
  1. In terminal, execute:
     ```powershell
     dotnet test src/backend/PrIMA.Api.Tests/PrIMA.Api.Tests.csproj --no-build --filter "FullyQualifiedName~BR_PRIMA_005|FullyQualifiedName~BR_PRIMA_007|FullyQualifiedName~BR_PRIMA_010"
     ```
  2. In browser, navigate to `/detectors/e2c04068-8314-469b-8326-880bebbff71d` (`Patient Admission Discharge Sync`).
  3. Inspect detector health badge and linked issues.
- **Expected Result:**
  - Automated tests pass with 0 failures (`3 passed`).
  - In browser: Detector health badge displays amber `Silent`.
  - Linked issues section displays active infrastructure issue `SILENT:e2c04068-8314-469b-8326-880bebbff71d`.
  - Webhook timeout strictly aborts at 15 seconds, records `ExecutionFailed`, and flags detector `Failing`.

---

## TC-07-05 — Operational Attribution & Accountability Across Audit Logs (`BR-PRIMA-008`)

- **Objective:** Verify that all lifecycle mutations deterministically record the authenticated officer's full name across `dbo.IssueActivityLogs`, investigation notes, remediation, verification, and dismissal (`BR-PRIMA-008`).
- **Preconditions:** Authenticated as `siti.rahma`; on `http://localhost:5173/issues/d0a10001-0000-0000-0000-000000000001`.
- **Steps:**
  1. In terminal, execute:
     ```powershell
     dotnet test src/backend/PrIMA.Api.Tests/PrIMA.Api.Tests.csproj --no-build --filter "FullyQualifiedName~BR_PRIMA_008"
     ```
  2. In browser, inspect the **Activity & Audit Trail** accordion on `S-02`.
  3. Inspect the **Remediation Record** and **Verification Certificate**.
- **Expected Result:**
  - Automated test passes cleanly (`1 passed`).
  - In browser: Every activity log entry records the exact officer name (e.g., `Actor: Siti Rahma` or `Actor: Ahmad Budi`), role, and WIB timestamp.
  - No anonymous, null, or generic `User` entries exist in the audit log.

---

# 12. Test Suite: Edge Cases, Boundary Conditions & RFC 7807 Error Handling

## TC-08-01 — Invalid Date Range Validation (`FromUtc > ToUtc`) RFC 7807 `422 Unprocessable Entity`

- **Objective:** Verify that submitting a request where `fromUtc` is chronologically after `toUtc` returns an RFC 7807 ProblemDetails response with HTTP status `422 Unprocessable Entity`.
- **Preconditions:** Authenticated as `ahmad.budi`.
- **Steps:**
  1. In terminal or browser Developer Tools console, execute a fetch request with inverted date bounds:
     ```javascript
     const token = JSON.parse(localStorage.getItem('prima_auth') || '{}').token;
     fetch('/api/history/reliability-kpi?fromUtc=2026-11-25T00:00:00.000Z&toUtc=2026-11-20T00:00:00.000Z', {
       headers: { 'Authorization': `Bearer ${token}` }
     }).then(r => r.json()).then(console.log);
     ```
- **Expected Result:**
  - Response status is `422 Unprocessable Entity`.
  - Response payload conforms to RFC 7807 `ValidationProblemDetails`:
    - `title`: `"One or more validation errors occurred."`
    - `errors.FromUtc`: includes `"FromUtc must be less than or equal to ToUtc."`

---

## TC-08-02 — Missing or Malformed Query Parameters RFC 7807 `400 Bad Request`

- **Objective:** Verify that omitting required query parameters or providing non-date strings returns an RFC 7807 ProblemDetails response with HTTP status `400 Bad Request` instead of an unhandled server 500 error.
- **Preconditions:** Authenticated as `ahmad.budi`.
- **Steps:**
  1. In console, execute:
     ```javascript
     const token = JSON.parse(localStorage.getItem('prima_auth') || '{}').token;
     fetch('/api/history/reliability-kpi?fromUtc=not-a-date&toUtc=2026-11-22T00:00:00.000Z', {
       headers: { 'Authorization': `Bearer ${token}` }
     }).then(r => r.json()).then(console.log);
     ```
- **Expected Result:**
  - Response status is `400 Bad Request`.
  - Response conforms to RFC 7807 ProblemDetails with descriptive message indicating the parameter could not be parsed as a valid `DateTime`.

---

## TC-08-03 — Exact Millisecond Boundary Precision (`.999Z` vs `.000Z` Next Day WIB) (`DEC-003`, `F-P2S03-01`)

- **Objective:** Verify that queries with `toUtc` ending at `23:59:59.999 WIB` (`...T16:59:59.999Z`) use `DbType.DateTime2` parameters, correctly including records at the exact boundary and excluding records created at `00:00:00.000 WIB` of the next day (`F-P2S03-01`).
- **Preconditions:** Test suite `HistoricalIssuesEndpointTests` accessible; local backend running.
- **Steps:**
  1. In terminal, execute:
     ```powershell
     dotnet test src/backend/PrIMA.Api.Tests/PrIMA.Api.Tests.csproj --no-build --filter "FullyQualifiedName~GetHistoricalIssues_WibMillisecondBoundaryPrecision"
     ```
- **Expected Result:**
  - Test executes against SQL Server and passes cleanly (`1 passed`).
  - Verifies that `DATETIME2` parameters prevent SQL Server 1/300th-second rounding, ensuring zero records leak across WIB midnight boundaries.

---

## TC-08-04 — Unauthorized Access Protection (`401 Unauthorized` / Redirect to Login)

- **Objective:** Verify that unauthenticated requests to `/history` are blocked and redirected to `/login`, and direct API requests return HTTP `401 Unauthorized`.
- **Preconditions:** Browser in Incognito / Private window with no auth token.
- **Steps:**
  1. Open an Incognito browser window.
  2. Navigate directly to `http://localhost:5173/history`.
  3. In terminal, execute a curl or fetch without Authorization header:
     ```powershell
     curl -i http://localhost:5005/api/history/reliability-kpi?fromUtc=2026-11-01T00:00:00Z&toUtc=2026-11-22T00:00:00Z
     ```
- **Expected Result:**
  - Step 2: Vue Router navigation guard intercepts request and immediately redirects browser to `http://localhost:5173/login?redirect=%2Fhistory`.
  - Step 3: API returns `HTTP/1.1 401 Unauthorized`.

---

# 13. Test Suite: Complete End-to-End Shift-Close Operational Smoke Test

## TC-09-01 — Comprehensive Hospital EDP Period-Close Audit & Verification Flow

- **Objective:** Execute the complete end-to-end hospital operational shift-close workflow across the live running application, verifying cross-screen navigation, period-close KPI reporting, historical issue ledger auditing, and origin-preserving return.
- **Preconditions:** Both Backend API (`http://localhost:5005`) and Frontend Client (`http://localhost:5173`) are running. Authenticated as `ahmad.budi`.
- **Steps:**
  1. **Shift Dashboard Inspection (`S-01`):**
     - Navigate to `http://localhost:5173/`.
     - Observe the top summary counters bar. Note the count on **Resolved Today** (Tile 5).
  2. **Deep-Link to Period Close (`S-01` → `S-07`):**
     - Click the **Resolved Today** tile.
     - Verify navigation to `http://localhost:5173/history?preset=Today&status=Resolved`.
  3. **Verify Shift KPIs (`S-07`):**
     - Verify Tile 1 (Total Issues), Tile 2 (Resolution Rate %), Tile 3 (MTTA), Tile 4 (MTTR), Tile 5 (Pass Rate %), and Tile 6 (Reopened Issues).
     - Confirm all values render in clean monospace typography.
  4. **Verify Proportion Distributions:**
     - Scroll to Scope Breakdown and Severity Breakdown.
     - Confirm pure CSS horizontal bars render with appropriate colors and monospace figures.
  5. **Historical Ledger Audit:**
     - In the historical table, locate `LIS-2026-RESOLVED-TODAY`.
     - Verify status is `Resolved`, resolved date is today, and MTTR duration is displayed.
  6. **Deep Audit Navigation (`S-07` → `S-02`):**
     - Click the row for `LIS-2026-RESOLVED-TODAY`.
     - Verify navigation to `/issues/d0a10001-0000-0000-0000-000000000001?from=history`.
     - Verify header displays `← Back to History`.
     - Inspect remediation notes and clean verification certificate. Confirm read-only protection (no mutation buttons).
  7. **Context-Preserving Return (`S-02` → `S-07`):**
     - Click `← Back to History`.
     - Verify return to `http://localhost:5173/history?preset=Today&status=Resolved`.
     - Verify preset and status filters are intact.
  8. **Multi-Preset Exploration:**
     - Click **This Month** preset. Verify table loads broader cohort.
     - Click **Reopened** status filter pill. Locate `BILL-2026-REOPENED-TODAY`.
     - Test search filter: type `Stock`. Confirm table isolates `RX-2026-RESOLVED-LW1`.
  9. **Sidebar Navigation & Branding:**
     - Inspect header logo and footer: confirm `v1.0-rc` release candidate badge.
     - Click sidebar `Detectors`, then click back to `History & Reports`.
- **Expected Result:**
  - All 9 shift-close workflow stages execute seamlessly with zero uncaught exceptions, zero console errors, and zero layout defects.
  - Origin navigation smoothly transitions between screens while preserving active audit state.
  - Delivers complete retrospective reporting and historical audit capabilities for the PrIMA v1.0 Release Candidate.

---

# 14. Verification Summary & Next Workflow Stages

### 14.1 Acceptance Gate Criteria
The Stage 4 implementation achieves acceptance when:
- [ ] Automated Quality Gate passes 100% (TC-001: backend tests, TC-002: static compliance audit, TC-003: clean frontend production build, TC-004: OpenAPI contract synchronization).
- [ ] All manual test suites (Sections 5 through 13) are executed by human testers and recorded in `TEST-EXECUTION`.
- [ ] Every test case achieves `PASS`. Any observed defect is documented with full evidence and handed off to the **Issuer** via `ics-issue-creation` as a `BUG` ISSUE.

### 14.2 Next Workflow Stage & Owning Role
Upon successful execution and validation of this test package:
1. **Human Tester:** Executes the test suites defined in this package against the live running application, recording results, timestamps, and evidence via `TEST-EXECUTION` assisted by `ica-tester` (`ics-testing-assistance`).
2. **Tester (`ica-tester`):** Grants `TEST PASSED` gate once all test cases achieve `PASS` (or all defects are resolved through formal bug issues).
3. **SDLC Transition:** Following `TEST PASSED`, the feature marks the completion of the core development roadmaps (Stages 1 through 4: Feature-Complete PrIMA v1.0 Release Candidate) and unlocks **Stage 5: Pilot SIMRS Detector Suite, Hardening & Production Rollout** under the governance of the **Architect** (`ica-architect`).
