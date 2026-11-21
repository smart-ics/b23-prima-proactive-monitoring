---
Title: PrIMA Stage 3 — Detector Portfolio, Automated Supervision & Webhook Verification Test Package
Code: PRIMA-STAGE-3
Artifact: TEST-PACKAGE
Version: 1.0
LastUpdated: 2026-11-21
Status: READY-FOR-TESTING
---

# 1. Overview & Purpose

This test package provides human-executable verification procedures for **PrIMA Stage 3: Detector Portfolio, Automated Supervision & Webhook Verification** (`Autonomous Proactive Supervision & Self-Monitoring` milestone).

The test suite validates the integrated system as a whole against the live running application, covering:
1. **Detector Portfolio Management (`S-03`):** 4-state Health Summary Bar (`Healthy`, `Failing`, `Silent`, `Paused`), multi-attribute filter strips (Scope, Admin Status, Health Status), debounced search, virtualized data grid with 30-day clean pass rate color badges, and responsive card views.
2. **Global Navigation & Sidebar Health Alert (`AppLayout.vue`):** Active `/detectors` navigation, animated amber/rose alert indicator dot and count pill on the sidebar when failing or silent detectors exist (`DEC-010`), and real-time 15-second background synchronization.
3. **Detector Detail Workbench (`S-04`):** Sticky header navigation, 4-column 30-day reliability metrics card, recent 10 inspection runs with inline diagnostic expansion, monospace failure blocks with one-click copy, and linked active issues feed.
4. **Detector Administrative Governance (`S-04`, `DEC-009`, `BR-PRIMA-008`):** Status-sensitive Action Bar operations (`Pause`, `Resume`, confirmation-gated `Retire`), retirement lockouts, and linked issue lifecycle autonomy upon retirement.
5. **Detector Registration & Configuration Management (`S-05`):** Create (`/detectors/new`) and Edit (`/detectors/:id/edit`) workflows, VeeValidate + Zod validation, immutable name lock in Edit mode (`BR-PRIMA-008`), Split Cadence Picker (Minutes/Hours), dynamic 2× silence threshold calculation helper banner (`BR-PRIMA-010`), and optional validated webhook URL.
6. **Historical Inspection Run Log (`S-06`):** Dedicated run log (`/detectors/:id/runs`), 7-day date range picker with quick preset buttons (24h, 7d, 30d), outcome and trigger filtering pills, non-disruptive row expansion, monospace diagnostics, and direct navigation to discovered issues.
7. **On-Demand Webhook Probe Execution (`S-04`, `E-7`, `DEC-001`, `DEC-002`):** Manual probe dispatch via Action Bar `Trigger Run`, execution duration tracking, synchronous outcome persistence, and graceful 502/504 error handling with transition to `Failing`.
8. **Automated Webhook Verification & Issue Gate Unlocking (`S-02`, `D-2`, `BR-PRIMA-004`):** Issue Workbench Action Bar verification promotion for webhook-enabled detectors, automated verification re-run dispatch (`POST /api/issues/{id}/verification/trigger-run`), Verification Gate unlocking on clean pass, and fallback to manual verification (`M-03`).
9. **Cross-Feature Dashboard Upgrades (`S-01`, `G-1`, `G-2`, `DEC-010`):** Conditional top Alert Strip (`⚠ N issues escalated · M issues reopened`), `Escalated` toggle filter pill, visual `↑ Escalated (L{N})` badges on active issue rows, and escalation history in issue audit logs.
10. **Autonomous Background Supervision Engine (`MonitoringSupervisorWorker`, `BR-PRIMA-010`, `BR-PRIMA-005`, `BR-PRIMA-003`):** 60-second periodic background loop executing Phase 1 (silence detection at 2× cadence, `SILENT:{DetectorId}` synthetic issue creation, cadence-multiplied occurrences) and Phase 2 (dynamic multi-step SLA timeout escalation and recurrence threshold escalation).
11. **Telemetry Ingestion Failure & Autonomous Recovery (`IngestTelemetryHandler`, `DEC-004`, `BR-PRIMA-005`):** Processing `ExecutionFailed` outcomes, synthesizing `FAIL:{DetectorId}` infrastructure issues, and resetting detector health to `Healthy` on clean telemetry while preserving EDP governance.

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

### 2.3 Pre-seeded Stage 3 Demo Fixtures
When `SeedDemoData == true` (default in Development mode), `DevelopmentDemoDataSeeder.cs` idempotently seeds 6 detectors and realistic issues covering all Stage 3 operational states:

#### Seeded Detectors Portfolio:
| Detector Name | Target Module | Scope | Admin Status | Health Status | Cadence | Webhook URL Configured | Deterministic ID | Key Characteristics |
|---|---|---|---|---|---|---|---|---|
| `Billing Journal Balance Checker` | Billing | `DataIntegrity` | `Active` | `Healthy` | 300s (5m) | Yes (`https://probes.internal.hospital/api/detectors/billing`) | `3fa85f64-5717-4562-b3fc-2c963f66afa6` | Active healthy detector with webhook, high pass rate (> 90%), ready for manual probe trigger. |
| `EMR Database Replication Monitor` | Database | `Infrastructure` | `Active` | `Failing` | 60s (1m) | Yes (`https://probes.internal.hospital/api/detectors/replication`) | `b6a9c023-4d5e-5f6a-7b8c-9d0e1f2a3b4c` | Active failing detector with recent `ExecutionFailed` run and linked `FAIL:{id}` issue. |
| `Patient Admission Discharge Sync` | Inpatient | `ProcessIntegrity` | `Active` | `Silent` | 600s (10m) | No | `e2c04068-8314-469b-8326-880bebbff71d` | Active silent detector (missed heartbeat by 40m vs 20m threshold) and linked `SILENT:{id}` issue. |
| `Laboratory Order Sync Prober` | Laboratory | `DataSync` | `Paused` | `Healthy` | 180s (3m) | Yes (`https://probes.internal.hospital/api/detectors/laboratory`) | `c7b0d134-5e6f-6a7b-8c9d-0e1f2a3b4c5d` | Administratively paused detector; ready for `Resume` transition. |
| `Legacy Bed Tracking Bridge` | Inpatient | `ProcessIntegrity` | `Retired` | `Healthy` | 300s (5m) | No | `f8c1e245-6f7a-7b8c-9d0e-1f2a3b4c5d6e` | Permanently decommissioned detector; has active linked issue (`DEC-009`); read-only form. |
| `Prescription Stock Deductor` | Pharmacy | `DataSync` | `Active` | `Healthy` | 300s (5m) | No | `a5f8b912-3c4d-4e5f-6a7b-8c9d0e1f2a3b` | Active healthy detector without webhook; host for escalated critical issue. |

#### Seeded Stage 3 Operational Issues:
| Entity Reference / Key | Status | Severity | Detector | Deterministic ID | Key Characteristics |
|---|---|---|---|---|---|
| `FAIL:b6a9c023-4d5e-5f6a-7b8c-9d0e1f2a3b4c` | `Open` | High | EMR Database Replication Monitor | `d0555555-5555-5555-5555-555555555591` | Synthetic infrastructure issue generated from execution failure (`BR-PRIMA-005`). |
| `SILENT:e2c04068-8314-469b-8326-880bebbff71d` | `Open` | High | Patient Admission Discharge Sync | `d0222222-2222-2222-2222-222222222291` | Synthetic infrastructure issue generated from silence heartbeat breach (`BR-PRIMA-010`). |
| `RX-ESCALATED-099` | `Open` | Critical | Prescription Stock Deductor | `d0999999-9999-9999-9999-999999999901` | Unacknowledged issue escalated to `Critical` (`EscalationLevel = 2`) with audit logs. |
| `BED-LEGACY-001` | `Investigating` | Medium | Legacy Bed Tracking Bridge | `d0888888-8888-8888-8888-888888888801` | Active issue under retired detector; demonstrates issue lifecycle autonomy (`DEC-009`). |
| `LIS-QUEUE-4412` | `Investigating` | Medium | Laboratory Order Sync Prober | `d0444444-4444-4444-4444-444444444401` | Investigating issue with recorded remediation; assigned to webhook-enabled detector. |

### 2.4 Telemetry & Webhook Simulation Tooling
- **Telemetry Ingestion Endpoint:** `POST http://localhost:5005/api/ingestion/telemetry` (API Key: `prima-dev-key-alpha-99`).
- **Telemetry Simulator CLI:** `node src/tools/telemetry-simulator/simulate.mjs [scenario] [options]`.
- **Local Webhook Mock Server (Optional for Live Probe Verification):**
  A lightweight HTTP mock server can be started on port 8088 to respond to webhook probes:
  ```powershell
  # Using Node.js one-liner in a spare terminal:
  node -e "const http = require('http'); http.createServer((req, res) => { res.writeHead(200, {'Content-Type': 'application/json'}); res.end(JSON.stringify({ outcome: 'Passed', durationMs: 124, findings: [] })); }).listen(8088, () => console.log('Mock webhook listening on http://localhost:8088'));"
  ```

---

# 3. Test Execution Sequence

Testers should follow this recommended execution order:

```text
Section 4: Automated Quality Gate (TC-001 to TC-003)
   ↓
Section 5: Sidebar Navigation & Detector Portfolio Overview (TC-01-01 to TC-01-05)
   ↓
Section 6: Detector Detail Workbench & Action Bar Governance (TC-02-01 to TC-02-06)
   ↓
Section 7: Detector Registration & Edit Configuration (TC-03-01 to TC-03-06)
   ↓
Section 8: Historical Inspection Run Log & Telemetry Diagnostics (TC-04-01 to TC-04-04)
   ↓
Section 9: On-Demand Webhook Probe Execution (TC-05-01 to TC-05-03)
   ↓
Section 10: Automated Webhook Verification & Issue Gate Unlocking (TC-06-01 to TC-06-05)
   ↓
Section 11: Cross-Feature Dashboard Upgrades & Dynamic Escalation (TC-07-01 to TC-07-04)
   ↓
Section 12: Autonomous Background Supervision & Self-Healing Loop (TC-08-01 to TC-08-05)
   ↓
Section 13: Complete End-to-End Autonomous Supervision Smoke Test (TC-09-01)
```

---

# 4. Automated Quality Gate

## TC-001 — Backend Automated Regression & Domain Test Suite

- **Objective:** Verify that all 295 automated backend tests pass cleanly, covering domain aggregate invariants (`Detector.cs`, `Issue.cs`), background supervisor worker (`MonitoringSupervisorWorker`), webhook client (`DetectorWebhookClient`), ingestion failure and recovery, Minimal API endpoints, and idempotency.
- **Preconditions:** Host has .NET 8 SDK installed; local SQL Server running.
- **Steps:**
  1. Open terminal in the repository root directory.
  2. Execute:
     ```powershell
     dotnet test src/backend/PrIMA.slnx
     ```
- **Expected Result:**
  - All 295 test cases execute and pass cleanly (`Failed: 0, Passed: 295, Skipped: 0`).
  - Duration is approximately 15–25 seconds.
  - Test output concludes with: `Passed! - Failed: 0, Passed: 295, Skipped: 0, Total: 295`.

---

## TC-002 — Frontend Production Build & TypeScript Typecheck

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
  - Vite bundles all production chunks into `src/frontend/dist/` successfully with 0 errors.

---

## TC-003 — OpenAPI Specification Integrity & Contract Verification

- **Objective:** Verify that the OpenAPI Swagger endpoint exposes all 8 Stage 3 detector portfolio endpoints, the automated verification trigger endpoint, and that TypeScript contracts are fully synchronized.
- **Preconditions:** Backend running at `http://localhost:5005`.
- **Steps:**
  1. Open browser and navigate to `http://localhost:5005/swagger/index.html`.
  2. Inspect the **Detectors** tag group.
  3. Inspect the **Issues** tag group.
- **Expected Result:**
  - The **Detectors** tag group exposes:
    - `GET /api/detectors`
    - `GET /api/detectors/summary`
    - `GET /api/detectors/{id}`
    - `POST /api/detectors`
    - `PUT /api/detectors/{id}`
    - `POST /api/detectors/{id}/toggle-status`
    - `POST /api/detectors/{id}/trigger-run`
    - `GET /api/detectors/{id}/runs`
  - The **Issues** tag group exposes:
    - `POST /api/issues/{id}/verification/trigger-run`
  - Responses include documented 200, 201, 204, 400, 401, 404, 409, 422, 502, and 504 schemas.

---

# 5. Test Suite: Sidebar Navigation & Detector Portfolio Overview (`S-03`)

## TC-01-01 — Sidebar Navigation & Live Amber/Rose Alert Indicator Dot (`DEC-010`)

- **Objective:** Verify that the sidebar navigation displays an active `Detectors` link with an animated alert indicator dot when unhealthy detectors exist in the system.
- **Preconditions:** Authenticated as `ahmad.budi`; system seeded with demo data (which contains 1 Failing and 1 Silent detector).
- **Steps:**
  1. Open browser and navigate to `http://localhost:5173/`.
  2. Look at the primary left navigation sidebar.
  3. Observe the `Detectors` menu item (radar/shield icon).
  4. Click the `Detectors` menu item.
- **Expected Result:**
  - The `Detectors` menu item is active and clickable (no longer disabled or displaying a "Coming in Stage 3" tooltip).
  - An animated amber/rose alert indicator dot with a numeric count pill (`2`) is visibly rendered beside the `Detectors` label, reflecting `FailingCount (1) + SilentCount (1) = 2`.
  - Browser URL navigates to `http://localhost:5173/detectors`.
  - Page header displays: `"Detector Portfolio"` with subtitle `"Supervise probe execution health, cadences, and reliability metrics"`.

---

## TC-01-02 — 4-State Health Summary Bar Interactive Toggle Filtering

- **Objective:** Verify that the 4-state Health Summary Bar accurately displays system-wide detector counts and functions as interactive toggle filter chips.
- **Preconditions:** On `http://localhost:5173/detectors`.
- **Steps:**
  1. Observe the 4 Health Summary Bar cards at the top of the portfolio screen.
  2. Click the **Failing** count chip (rose badge).
  3. Observe the detector table rows.
  4. Click the **Failing** count chip again.
  5. Click the **Silent** count chip (amber badge).
  6. Click the **Paused** count chip (slate badge).
  7. Click the **Healthy** count chip (emerald badge).
- **Expected Result:**
  - Initial summary displays: **Healthy: 3**, **Failing: 1**, **Silent: 1**, **Paused: 1**, Total: 6.
  - Clicking **Failing** activates an active ring outline around the card; the table immediately filters to show only `EMR Database Replication Monitor` (1 row).
  - Clicking **Failing** again deactivates the filter; all 6 detectors are redisplayed.
  - Clicking **Silent** filters the table to show only `Patient Admission Discharge Sync` (1 row).
  - Clicking **Paused** filters the table to show only `Laboratory Order Sync Prober` (1 row).
  - Clicking **Healthy** filters the table to show only the 3 healthy detectors.

---

## TC-01-03 — Multi-Attribute Scope and Status Filtering with Debounced Instant Search

- **Objective:** Verify that the filter bar correctly filters detectors across Monitoring Scope, Admin Status, Health Status, and instant search queries.
- **Preconditions:** On `http://localhost:5173/detectors`; summary bar filters cleared.
- **Steps:**
  1. In the **Scope** dropdown, select `Infrastructure`.
  2. In the **Admin Status** filter pills, click `Active`.
  3. In the **Search** input box, type `Replication`.
  4. Clear the search input box and select `All` for Scope and Admin Status.
  5. In the Search input box, type `NonExistentProbe`.
- **Expected Result:**
  - Selecting `Infrastructure`, `Active`, and searching `Replication` filters the table down to exactly `EMR Database Replication Monitor`.
  - Clearing filters returns the list to all 6 detectors.
  - Searching `NonExistentProbe` displays an empty state banner: `"No detectors match the selected filter criteria."` with a `"Reset Filters"` button.
  - Clicking `"Reset Filters"` restores all filters to default and displays all 6 detectors.

---

## TC-01-04 — Virtualized Table Columns, Formatting & Color-Coded Pass Rates (`DEC-008`)

- **Objective:** Verify that the detector portfolio data table displays all required columns with formatted cadences, Asia/Jakarta (WIB) timestamps, active issue counts, and color-coded 30-day clean pass rate badges.
- **Preconditions:** On `http://localhost:5173/detectors`.
- **Steps:**
  1. Inspect the columns of the detector table: Status, Name & Target Module, Cadence, Last Heartbeat, 30-Day Pass Rate, Active Issues, Actions.
  2. Inspect the **Cadence** column values.
  3. Inspect the **30-Day Pass Rate** column values and colors:
     - `Billing Journal Balance Checker`
     - `EMR Database Replication Monitor`
  4. Inspect the **Active Issues** column values.
- **Expected Result:**
  - Status badges accurately display composite health and admin status:
    - Failing: rose badge (`Failing`)
    - Silent: amber badge (`Silent`)
    - Paused: slate badge (`Paused`)
    - Active Healthy: emerald badge (`Healthy`)
    - Retired: zinc badge (`Retired`)
  - Cadence is human-formatted (e.g., `Every 5m`, `Every 1m`, `Every 10m`, `Every 3m`).
  - Pass Rate badges comply with `DEC-008` thresholds:
    - `>= 95.0%`: Emerald badge.
    - `85.0%–94.9%`: Amber badge.
    - `< 85.0%`: Rose badge (e.g., `EMR Database Replication Monitor`).
  - Active Issues displays a count badge linking to linked issues (e.g., `1 active issue` for Failing/Silent detectors).

---

## TC-01-05 — Table Row Click & Card View Navigation to Detector Detail (`S-04`)

- **Objective:** Verify that clicking a detector table row (or clicking `View →` or a card in card view) smoothly navigates to that detector's detail workbench (`S-04`).
- **Preconditions:** On `http://localhost:5173/detectors`.
- **Steps:**
  1. Click anywhere on the row for `Billing Journal Balance Checker`.
  2. Observe the browser navigation.
  3. Return to `/detectors` via browser back or breadcrumb.
  4. Toggle the view mode switch to **Card View** (grid icon in header).
  5. Click the `EMR Database Replication Monitor` card.
- **Expected Result:**
  - Step 1 navigates to `http://localhost:5173/detectors/3fa85f64-5717-4562-b3fc-2c963f66afa6`.
  - The detector detail workbench (`S-04`) loads with detector name, metadata, reliability cards, and recent runs.
  - Step 4 switches the UI layout into responsive cards.
  - Step 5 navigates to `http://localhost:5173/detectors/b6a9c023-4d5e-5f6a-7b8c-9d0e1f2a3b4c`.

---

# 6. Test Suite: Detector Detail Workbench & Action Bar Governance (`S-04`)

## TC-02-01 — Detail Workbench Sticky Header & 4-Column Reliability Metrics Card

- **Objective:** Verify that the detector detail screen displays a sticky context header, back navigation button, detector scope/module badges, and the 4-column 30-day reliability metrics card.
- **Preconditions:** Authenticated as `ahmad.budi`; navigate to `http://localhost:5173/detectors/3fa85f64-5717-4562-b3fc-2c963f66afa6` (`Billing Journal Balance Checker`).
- **Steps:**
  1. Observe the sticky top header.
  2. Click the chevron-left back button.
  3. Navigate forward back to the detail screen.
  4. Inspect the 4-column **30-Day Reliability Card**.
- **Expected Result:**
  - Header displays: `Billing Journal Balance Checker`, module tag `Billing`, scope tag `DataIntegrity`, health badge `Healthy`, admin badge `Active`, and WIB operational clock.
  - Back button navigates cleanly back to `/detectors`.
  - The 4 reliability columns display:
    1. **Last Successful Run:** Asia/Jakarta (WIB) timestamp and relative time (e.g., `Just now` or `X minutes ago`).
    2. **Last Failure:** Timestamp or `"None in last 30 days"`.
    3. **Total Runs (30d):** Numeric total (>= 30 runs).
    4. **Clean Pass Rate:** Formatted percentage (e.g., `93.8%` or `100.0%`) with color-coded chip.

---

## TC-02-02 — Recent 10 Inspection Runs Display & Polling-Stable Inline Expansion

- **Objective:** Verify that the Recent Runs table shows the 10 most recent runs with expand/collapse capability that remains stable across 15-second background query refreshes.
- **Preconditions:** On `http://localhost:5173/detectors/3fa85f64-5717-4562-b3fc-2c963f66afa6`.
- **Steps:**
  1. Scroll down to the **Recent Inspection Runs (Top 10)** section.
  2. Verify that up to 10 run rows are displayed with Timestamp, Trigger Source, Outcome, Duration, and Findings.
  3. Click the chevron expand button on row #1 to expand its execution details.
  4. Wait for 20 seconds while observing the open expanded row (allowing at least one 15-second background query refetch to occur).
- **Expected Result:**
  - Row expands smoothly to reveal execution details (run ID, duration, trigger source, and payload summary).
  - During and after the 15-second background query refetch, the expanded row remains open and does NOT collapse or flicker (verifying reactive `Set<string>` ID tracking).

---

## TC-02-03 — Monospace Diagnostic Failure Blocks & One-Click Clipboard Copy

- **Objective:** Verify that failed inspection runs render an error trace block in dark monospace font with a functional copy button.
- **Preconditions:** Authenticated as `ahmad.budi`; navigate to `http://localhost:5173/detectors/b6a9c023-4d5e-5f6a-7b8c-9d0e1f2a3b4c` (`EMR Database Replication Monitor`).
- **Steps:**
  1. Scroll to the **Recent Inspection Runs** section.
  2. Locate the row with the rose `ExecutionFailed` badge.
  3. Click the expand button on that row.
  4. Inspect the diagnostic failure block.
  5. Click the `"Copy Trace"` button.
  6. Paste the clipboard contents into a text editor (e.g., Notepad).
- **Expected Result:**
  - Expanded row displays a dark slate/black code block formatted with monospace font.
  - Failure reason is clearly visible (e.g., replication latency timeout or SQL connectivity error).
  - Clicking `"Copy Trace"` toggles the button text to `"Copied!"` with a green checkmark icon.
  - Pasted text exactly matches the failure reason / trace string displayed in the UI.

---

## TC-02-04 — Linked Active Issues Feed & Direct Navigation to Workbench (`S-02`)

- **Objective:** Verify that the detector detail screen displays all active unclosed issues linked to this detector and allows one-click navigation directly to the Issue Detail Workbench (`S-02`).
- **Preconditions:** On `http://localhost:5173/detectors/b6a9c023-4d5e-5f6a-7b8c-9d0e1f2a3b4c` (`EMR Database Replication Monitor`).
- **Steps:**
  1. Scroll to the **Linked Active Issues** section.
  2. Observe the issues listed.
  3. Locate the row for `FAIL:b6a9c023-4d5e-5f6a-7b8c-9d0e1f2a3b4c` (Execution Failure).
  4. Click the issue link or `"Investigate →"` button.
- **Expected Result:**
  - The section lists the active issue with Severity badge (`High`), Title (`Execution Failure: EMR Database Replication Monitor`), Status (`Open`), and Occurrences count.
  - Clicking the issue immediately navigates to `http://localhost:5173/issues/d0555555-5555-5555-5555-555555555591`.
  - The Issue Detail Workbench (`S-02`) opens with full context for the infrastructure failure.

---

## TC-02-05 — Status-Sensitive Action Bar State Rendering

- **Objective:** Verify that the Action Bar dynamically updates its available actions according to detector administrative state and webhook configuration.
- **Preconditions:** Authenticated as `ahmad.budi`.
- **Steps:**
  1. Navigate to `http://localhost:5173/detectors/3fa85f64-5717-4562-b3fc-2c963f66afa6` (`Active` with webhook).
  2. Observe the Action Bar buttons in the top right.
  3. Navigate to `http://localhost:5173/detectors/e2c04068-8314-469b-8326-880bebbff71d` (`Active` without webhook).
  4. Observe the Action Bar buttons.
  5. Navigate to `http://localhost:5173/detectors/c7b0d134-5e6f-6a7b-8c9d-0e1f2a3b4c5d` (`Paused`).
  6. Observe the Action Bar buttons.
  7. Navigate to `http://localhost:5173/detectors/f8c1e245-6f7a-7b8c-9d0e-1f2a3b4c5d6e` (`Retired`).
  8. Observe the Action Bar.
- **Expected Result:**
  - Case 1 (Active w/ Webhook): Shows primary `Trigger Run` (bolt icon), `Pause` (pause icon), `Edit` (pencil icon), and `Retire` (trash/ban icon).
  - Case 2 (Active w/o Webhook): `Trigger Run` button is disabled with tooltip: `"No webhook configured for this detector"`; `Pause`, `Edit`, `Retire` are active.
  - Case 3 (Paused): Shows primary emerald `Resume` button (play icon), `Edit`, and `Retire`. `Trigger Run` is hidden or disabled.
  - Case 4 (Retired): Action Bar buttons are hidden; a prominent yellow/zinc decommission banner is displayed: `"This detector is permanently decommissioned. Historical run records are preserved in read-only mode."`

---

## TC-02-06 — Administrative Lifecycle: Pause, Resume, and Confirmation-Gated Retire (`DEC-009`)

- **Objective:** Verify administrative state transitions: pausing an active detector, resuming a paused detector, and retiring with mandatory confirmation modal.
- **Preconditions:** Authenticated as `ahmad.budi`; navigate to `http://localhost:5173/detectors/c7b0d134-5e6f-6a7b-8c9d-0e1f2a3b4c5d` (`Laboratory Order Sync Prober`, currently `Paused`).
- **Steps:**
  1. Click the **Resume** button.
  2. Observe toast notification and status badge update.
  3. Click the **Pause** button.
  4. Observe toast notification and status badge update.
  5. Click the **Resume** button again to restore active state.
  6. Click the **Retire** button.
  7. In the confirmation dialog, inspect the warning text.
  8. Click `"Cancel"` in the dialog.
  9. Click **Retire** again, and click `"Confirm Decommission"`.
- **Expected Result:**
  - Step 1: Status badge immediately updates to `Active` (emerald); toast displays: `"Detector resumed successfully"`.
  - Step 3: Status badge updates to `Paused` (slate); toast displays: `"Detector paused successfully"`.
  - Step 7: Confirmation modal `ConfirmRetireDialog` appears stating: `"Are you sure you want to retire this detector? This action is permanent and cannot be undone."`
  - Step 8: Dialog closes; detector remains Active.
  - Step 9: Modal confirms; status permanently transitions to `Retired`. Action Bar buttons disappear and are replaced by the decommission banner.
  - Linked active issues (if any) remain open and accessible (`DEC-009`).

---

# 7. Test Suite: Detector Registration & Edit Configuration (`S-05`)

## TC-03-01 — Navigation to Registration Form (`/detectors/new`) & Form Layout

- **Objective:** Verify that clicking `+ Register Detector` from the portfolio screen opens the registration form with clean default values.
- **Preconditions:** Authenticated as `ahmad.budi`; on `http://localhost:5173/detectors`.
- **Steps:**
  1. Click the primary **"+ Register Detector"** CTA button in the header.
  2. Observe browser navigation and screen layout.
- **Expected Result:**
  - Browser URL navigates to `http://localhost:5173/detectors/new`.
  - Screen title displays: `"Register New Detector"`.
  - Form fields rendered:
    - **Detector Name:** Text input with placeholder.
    - **Target Module:** Text input with placeholder (e.g., `Billing`, `Pharmacy`, `Laboratory`).
    - **Monitoring Scope:** Select dropdown with options (`DataQuality`, `BusinessLogic`, `Infrastructure`, `ProcessFlow`).
    - **Description:** Textarea input.
    - **Expected Cadence:** Numeric input + unit toggle buttons (`Minutes` / `Hours`).
    - **Webhook URL:** Optional text input.
  - Helper banner below Cadence input displays real-time calculated silence threshold.
  - Primary button: `"Register Detector"`; secondary button: `"Cancel"`.

---

## TC-03-02 — Client-Side Validation Rules (VeeValidate + Zod) & Duplicate Name Rejection

- **Objective:** Verify form validation for required fields, cadence constraints, invalid URL format, and duplicate detector name conflict rejection.
- **Preconditions:** On `http://localhost:5173/detectors/new`.
- **Steps:**
  1. Leave all fields empty and click `"Register Detector"`.
  2. Observe validation error messages.
  3. Enter Name: `"Bi"` (too short).
  4. Enter Cadence: `0` or negative number.
  5. Enter Webhook URL: `"not-a-valid-url"`.
  6. Fill in valid fields, but enter Name: `"Billing Journal Balance Checker"` (existing seeded name).
  7. Click `"Register Detector"`.
- **Expected Result:**
  - Step 1: Red validation messages appear under required fields (Name is required, Target Module is required, Description is required).
  - Step 3: Error: `"Name must be at least 3 characters"`.
  - Step 4: Error: `"Cadence must be greater than 0"`.
  - Step 5: Error: `"Please enter a valid HTTP or HTTPS URL"`.
  - Step 7: Form submission triggers `409 Conflict`; UI displays inline error or toast: `"A detector with this name already exists. Please choose a unique name."`

---

## TC-03-03 — Split Cadence Picker & Real-Time Dynamic 2× Silence Threshold Helper Banner (`BR-PRIMA-010`)

- **Objective:** Verify that the Split Cadence Picker converts between minutes and hours and dynamically updates the 2× silence threshold calculation text.
- **Preconditions:** On `http://localhost:5173/detectors/new`.
- **Steps:**
  1. In the Cadence numeric input, enter `5`.
  2. Click the **Minutes** unit button.
  3. Observe the helper banner text below the cadence input.
  4. Change the numeric input to `15`.
  5. Observe the helper banner text.
  6. Change the unit button to **Hours** and numeric input to `2`.
  7. Observe the helper banner text.
- **Expected Result:**
  - Step 3 displays:
    - Cadence badge: `"Cadence: 5 minutes (300s)"`
    - Silence threshold badge: `"Silence Threshold: 10 minutes (600s)"`
    - Helper explanation: `"PrIMA will automatically flag this detector as Silent and synthesize an infrastructure issue if no heartbeat arrives for 10 minutes."`
  - Step 5 updates immediately to: `"Cadence: 15 minutes · Silence Threshold: 30 minutes"`.
  - Step 7 updates immediately to: `"Cadence: 2 hours (7200s) · Silence Threshold: 4 hours (14400s)"`.

---

## TC-03-04 — Successful Registration of New Active Detector

- **Objective:** Verify that entering valid data registers a new detector, redirects to its detail workbench, and sets initial health to `Healthy` and admin status to `Active`.
- **Preconditions:** Authenticated as `ahmad.budi`; on `http://localhost:5173/detectors/new`.
- **Steps:**
  1. Fill the form with:
     - **Detector Name:** `Radiology PACS Sync Prober`
     - **Target Module:** `Radiology`
     - **Monitoring Scope:** `DataSync`
     - **Description:** `Monitors DICOM image arrival and PACS synchronization against radiology examination worklists.`
     - **Cadence:** `10` Minutes
     - **Webhook URL:** `https://probes.internal.hospital/api/detectors/radiology`
  2. Click **"Register Detector"**.
- **Expected Result:**
  - Submit button shows loading spinner.
  - Toast notification appears: `"Detector registered successfully"`.
  - Browser automatically redirects to the new detector's detail page (`/detectors/:id`).
  - Header displays: `Radiology PACS Sync Prober` with `Active` and `Healthy` badges.
  - 30-Day Pass Rate displays `100.0%` (0 runs).

---

## TC-03-05 — Edit Existing Detector (`/detectors/:id/edit`) & Name Immutability Lock (`BR-PRIMA-008`)

- **Objective:** Verify that editing an existing detector enforces name immutability with a disabled lock icon while permitting configuration updates.
- **Preconditions:** Authenticated as `ahmad.budi`; navigate to `http://localhost:5173/detectors/3fa85f64-5717-4562-b3fc-2c963f66afa6` (`Billing Journal Balance Checker`).
- **Steps:**
  1. In the Action Bar, click the **"Edit"** button.
  2. Observe browser navigation to `/detectors/3fa85f64-5717-4562-b3fc-2c963f66afa6/edit`.
  3. Inspect the **Detector Name** input field.
  4. Modify the **Description** (add `Updated via operational testing.`).
  5. Change **Cadence** to `6` Minutes.
  6. Click **"Save Changes"**.
- **Expected Result:**
  - The Detector Name input is completely disabled/read-only with a lock icon and explanatory banner: `"Detector name cannot be changed after registration to preserve historical audit trail integrity (BR-PRIMA-008)."`
  - Silence threshold helper banner immediately recalculates to: `"Cadence: 6 min · Silence threshold: 12 min"`.
  - Clicking `"Save Changes"` executes `PUT /api/detectors/:id`.
  - Toast notification displays: `"Detector configuration updated successfully"`.
  - Application navigates back to `/detectors/:id` showing updated description and cadence (`Every 6m`).

---

## TC-03-06 — Retired Detector Edit Lockout & Decommission Advisory Banner

- **Objective:** Verify that attempting to edit a retired detector displays a decommission advisory and disables form mutations.
- **Preconditions:** Authenticated as `ahmad.budi`; navigate to `http://localhost:5173/detectors/f8c1e245-6f7a-7b8c-9d0e-1f2a3b4c5d6e/edit` (`Legacy Bed Tracking Bridge`).
- **Steps:**
  1. Observe the screen banner and form inputs.
  2. Inspect the **Save Changes** button.
- **Expected Result:**
  - A prominent banner is displayed: `"This detector is retired and cannot be modified. Decommissioned detectors are permanently locked in read-only mode."`
  - All form fields are disabled.
  - The `"Save Changes"` button is disabled or hidden.

---

# 8. Test Suite: Historical Inspection Run Log & Telemetry Diagnostics (`S-06`)

## TC-04-01 — Navigation from Detector Detail to Full Run Log (`/detectors/:id/runs`) & Context Header

- **Objective:** Verify seamless navigation from Detector Detail (`S-04`) to the dedicated Inspection Run Log (`S-06`) with complete detector context.
- **Preconditions:** On `http://localhost:5173/detectors/3fa85f64-5717-4562-b3fc-2c963f66afa6`.
- **Steps:**
  1. In the Recent Runs header or page top, click **"View Full Run Log →"**.
  2. Observe the browser URL and page header.
  3. Click the back button on the far left.
- **Expected Result:**
  - Browser navigates to `http://localhost:5173/detectors/3fa85f64-5717-4562-b3fc-2c963f66afa6/runs`.
  - Breadcrumb displays: `Detectors > Billing Journal Balance Checker > Inspection Run Log`.
  - Back button smoothly navigates back to the detector detail screen.

---

## TC-04-02 — Date Range Picker Quick Presets (24h, 7d, 30d) & Outcome/Trigger Filtering

- **Objective:** Verify that the run log filter bar supports quick date preset toggles (24h, 7d, 30d) and instant outcome/trigger filtering pills.
- **Preconditions:** On `http://localhost:5173/detectors/3fa85f64-5717-4562-b3fc-2c963f66afa6/runs`.
- **Steps:**
  1. Click the **"24h"** date preset button.
  2. Observe the date range inputs and table rows.
  3. Click the **"30d"** date preset button.
  4. In the **Outcome** filter pills, click **"Finding Discovered"** (amber pill).
  5. In the **Trigger Source** filter pills, click **"Scheduled"**.
  6. Click the **"Reset Filters"** button.
- **Expected Result:**
  - Clicking 24h filters to runs within the last 24 hours.
  - Clicking 30d expands the range to 30 days, loading all seeded runs (at least 32 runs).
  - Clicking "Finding Discovered" filters table to show only runs with discovered anomalies.
  - Clicking "Scheduled" restricts runs to scheduled probe executions.
  - Clicking "Reset Filters" restores defaults (last 7 days, all outcomes, all triggers).

---

## TC-04-03 — Virtualized Run Log Display & Non-Disruptive Row Expansion

- **Objective:** Verify that the run log table displays Asia/Jakarta (WIB) timestamps, duration, findings count, and maintains open row state during refreshes.
- **Preconditions:** On `http://localhost:5173/detectors/3fa85f64-5717-4562-b3fc-2c963f66afa6/runs`.
- **Steps:**
  1. Inspect the table columns: Timestamp, Trigger Source, Outcome, Duration, Findings, Actions.
  2. Click the expand button on row #2.
  3. Click the `"Refresh"` button in the header toolbar.
- **Expected Result:**
  - Timestamps are formatted in Asia/Jakarta (WIB) with relative time (e.g., `21 Nov 2026, 11:45 WIB (15m ago)`).
  - Duration is formatted in milliseconds (e.g., `124 ms`).
  - Row #2 expands to show run execution details.
  - Refreshing the query preserves row #2 in its open expanded state without collapsing.

---

## TC-04-04 — Execution Failure Monospace Diagnostics & Discovered Issues Direct Navigation

- **Objective:** Verify that failed runs display failure reasons with copy buttons, and runs with discovered findings provide direct navigation links to the created/updated issues on `S-02`.
- **Preconditions:** Authenticated as `ahmad.budi`; navigate to `http://localhost:5173/detectors/b6a9c023-4d5e-5f6a-7b8c-9d0e1f2a3b4c/runs` (`EMR Database Replication Monitor`).
- **Steps:**
  1. Locate a run with outcome `ExecutionFailed`.
  2. Expand the run row.
  3. Inspect the failure block and click `"Copy Trace"`.
  4. Locate a run with outcome `FindingDiscovered`.
  5. Expand the run row and click the linked issue link (`FAIL:...` or finding issue).
- **Expected Result:**
  - The failed run displays the failure reason in dark monospace font with a copy button that copies the diagnostic text.
  - The finding run lists the associated issue entity reference / correlation key with a link navigating directly to `http://localhost:5173/issues/:issueId` on `S-02`.

---

# 9. Test Suite: On-Demand Webhook Probe Execution (`S-04`, `E-7`)

## TC-05-01 — Trigger Manual Probe Run via Action Bar on Webhook-Enabled Detector

- **Objective:** Verify that clicking `Trigger Run` on a detector with a configured webhook executes a synchronous probe run, persists the inspection run, and displays a toast notification.
- **Preconditions:** Authenticated as `ahmad.budi`; navigate to `http://localhost:5173/detectors/3fa85f64-5717-4562-b3fc-2c963f66afa6` (`Billing Journal Balance Checker`).
  *(Note: If testing against an active mock webhook, ensure mock server is listening at the detector's webhook URL or update detector webhook URL to `http://localhost:8088/probe` via edit screen).*
- **Steps:**
  1. In the Action Bar, click the primary **"Trigger Run"** button.
  2. Observe the button state and toast notification.
  3. Observe the Recent Runs table below.
- **Expected Result:**
  - The `Trigger Run` button disables and displays a loading spinner with text `"Running Probe..."`.
  - Request dispatches `POST /api/detectors/3fa85f64-5717-4562-b3fc-2c963f66afa6/trigger-run`.
  - When probe completes:
    - If mock webhook responded `Passed`: Toast displays: `"Probe run completed successfully (Clean Pass) in X ms"`.
    - Recent Runs table immediately prepends the new run at the top with Trigger Source `Manual`.
    - Total Runs (30d) counter increments by +1.

---

## TC-05-02 — Webhook Execution Error & Timeout Handling (HTTP 502/504 ProblemDetails)

- **Objective:** Verify that when a webhook endpoint is unreachable or times out past 15 seconds, the system records an `ExecutionFailed` run, marks the detector `Failing`, and renders a descriptive error toast.
- **Preconditions:** Authenticated as `ahmad.budi`; edit detector webhook URL to an unreachable endpoint (e.g., `http://127.0.0.1:59999/unreachable`) or run against an unconfigured remote host.
- **Steps:**
  1. Navigate to the detector detail screen.
  2. Click **"Trigger Run"**.
  3. Observe the loading state and subsequent error handling.
- **Expected Result:**
  - System attempts outbound HTTP connection with strict 15-second timeout (`BR-PRIMA-007`, `DEC-001`).
  - Backend catches unreachability or timeout (`DEC-002`):
    - Records an `InspectionRun` with `Outcome = 'ExecutionFailed'`, `TriggerSource = 'Manual'`, and `FailureReason`.
    - Updates detector `HealthStatus = 'Failing'`.
    - Auto-synthesizes or appends to `FAIL:{id}` infrastructure issue (`BR-PRIMA-005`).
    - Returns `502 Bad Gateway` or `504 Gateway Timeout` ProblemDetails.
  - Frontend displays an error toast / alert banner: `"Probe execution failed: Webhook unreachable or timed out"`.
  - Health badge on the sticky header updates to `Failing` (rose).

---

## TC-05-03 — Action Bar Disabled State on Detectors without Webhook Configuration

- **Objective:** Verify that detectors without a configured webhook URL disable the `Trigger Run` button with a clarifying tooltip.
- **Preconditions:** Authenticated as `ahmad.budi`; navigate to `http://localhost:5173/detectors/e2c04068-8314-469b-8326-880bebbff71d` (`Patient Admission Discharge Sync`).
- **Steps:**
  1. Inspect the `Trigger Run` button in the Action Bar.
  2. Hover the mouse over the `Trigger Run` button.
- **Expected Result:**
  - The `Trigger Run` button is visually disabled (`opacity-50 cursor-not-allowed`).
  - Tooltip appears with message: `"No webhook URL configured for this detector. Configure a webhook in Edit to enable on-demand runs."`

---

# 10. Test Suite: Automated Webhook Verification & Issue Gate Unlocking (`S-02`, `D-2`)

## TC-06-01 — Header Detector Badge Router Link from Issue Workbench (`S-02`) to Detector Detail (`S-04`)

- **Objective:** Verify that the detector badge in the Issue Detail Workbench (`S-02`) header functions as an active router link navigating directly to that detector's detail screen (`S-04`).
- **Preconditions:** Authenticated as `ahmad.budi`; navigate to `http://localhost:5173/issues/d0444444-4444-4444-4444-444444444401` (`LIS-QUEUE-4412`).
- **Steps:**
  1. Locate the detector badge in the sticky header (`Laboratory Order Sync Prober`).
  2. Observe the cursor when hovering over the detector badge.
  3. Click the detector badge.
- **Expected Result:**
  - Cursor is pointer; badge displays a hover highlight.
  - Clicking the badge immediately navigates to `http://localhost:5173/detectors/c7b0d134-5e6f-6a7b-8c9d-0e1f2a3b4c5d`.
  - The detector detail workbench (`S-04`) loads for `Laboratory Order Sync Prober`.

---

## TC-06-02 — Action Bar Verification Promotion on Investigating Issue with Remediation

- **Objective:** Verify that an issue in `Investigating` status with a recorded remediation promotes automated verification to the primary action if the detector has a configured webhook.
- **Preconditions:** Authenticated as `ahmad.budi`; on `http://localhost:5173/issues/d0444444-4444-4444-4444-444444444401` (`LIS-QUEUE-4412`, assigned to `ahmad.budi`, remediation recorded).
- **Steps:**
  1. Inspect the Action Bar in the top right of the workbench.
  2. Observe the primary and secondary action buttons.
- **Expected Result:**
  - Primary button is: **"Trigger Verification Re-run"** (with bolt icon, blue/indigo styling).
  - Secondary button is: **"Manual Verification"** (`M-03`).
  - "Mark Resolved" button is visible but locked (`BR-PRIMA-004`).

---

## TC-06-03 — Clean Automated Verification Re-Run & Verification Gate Unlocking (`BR-PRIMA-004`)

- **Objective:** Verify that triggering an automated verification re-run that returns zero findings records a clean verification record, displays a toast, and unlocks the `BR-PRIMA-004` Verification Gate for issue resolution.
- **Preconditions:** Authenticated as `ahmad.budi`; on `http://localhost:5173/issues/d0444444-4444-4444-4444-444444444401` (`LIS-QUEUE-4412`).
  *(Note: Ensure webhook endpoint returns `{ outcome: "Passed", findings: [] }` or test via mock).*
- **Steps:**
  1. Click **"Trigger Verification Re-run"**.
  2. Observe loading state.
  3. Observe toast notification, Verification Record card, and Action Bar.
  4. Click **"Mark Resolved"**.
  5. In Modal `M-04`, confirm resolution.
- **Expected Result:**
  - Button displays loading spinner: `"Verifying..."`.
  - Webhook returns clean pass:
    - Toast displays: `"Verification Passed: No data anomalies detected"`.
    - Verification Record card renders emerald banner: `"Automated Re-Run Passed — Verified Clean"`.
    - Verification Gate padlock changes to unlocked (green).
    - **"Mark Resolved"** button becomes active (emerald).
  - Step 5 transitions issue status to `Resolved` (read-only state).
  - Activity log records: `VerificationRecorded` and `Resolved`.

---

## TC-06-04 — Failed Automated Verification Re-Run & Verification Gate Lockout Preservation

- **Objective:** Verify that an automated verification re-run returning findings records a failed verification record, maintains the Verification Gate lock, and prompts for remediation retry.
- **Preconditions:** Authenticated as `ahmad.budi`; navigate to an investigating issue where the webhook probe returns findings or execution failure.
- **Steps:**
  1. Click **"Trigger Verification Re-run"**.
  2. Observe toast notification and Verification Record card.
  3. Inspect the **"Mark Resolved"** button.
- **Expected Result:**
  - Toast displays: `"Verification Failed: Anomaly still present in target system"`.
  - Verification Record card renders rose banner: `"Anomaly Still Present"`.
  - **"Mark Resolved"** button remains strictly disabled with tooltip: `"Resolution locked: Requires an un-superseded clean verification record (BR-PRIMA-004)."`
  - Action Bar prompts for `"Record Remediation"` (`M-02`) to retry.

---

## TC-06-05 — Graceful Fallback to Manual Verification (`M-03`) when Detector has No Webhook

- **Objective:** Verify that when an issue's detector has no configured webhook, the Action Bar gracefully promotes Manual Verification as primary with an explanatory helper note.
- **Preconditions:** Authenticated as `ahmad.budi`; navigate to `http://localhost:5173/issues/d0666666-6666-6666-6666-666666666601` (`SEP-202611-9982`, detector has no webhook).
- **Steps:**
  1. Inspect the Action Bar.
- **Expected Result:**
  - Primary button is: **"Record Verification"** (`M-03`).
  - Helper note displays: `"No webhook configured for automated re-run — manual verification required."`
  - Automated re-run button is not rendered as primary.

---

# 11. Test Suite: Cross-Feature Dashboard Upgrades & Dynamic Escalation (`S-01`)

## TC-07-01 — Conditional Top Alert Strip (`G-1`) Display for Escalated Issues

- **Objective:** Verify that the Issue Dashboard (`S-01`) displays a prominent top Alert Strip whenever escalated or reopened issues exist in the system.
- **Preconditions:** Authenticated as `ahmad.budi`; navigate to `http://localhost:5173/` (Dashboard).
  *(Demo data includes seeded escalated issue `RX-ESCALATED-099` with `EscalationLevel = 2`).*
- **Steps:**
  1. Observe the top region of the dashboard, directly above summary metric cards.
  2. Inspect the text and action link inside the Alert Strip.
- **Expected Result:**
  - An amber/rose Alert Strip is visibly rendered at the top of the dashboard.
  - Text displays: `⚠ 1 issue escalated · 0 issues reopened` (or matching live counts).
  - An action link is rendered: `"Filter Escalated →"`.

---

## TC-07-02 — Instant Escalated Issues Feed Filtering (`escalatedOnly = true`)

- **Objective:** Verify that clicking `"Filter Escalated →"` or toggling the `Escalated` pill filter isolates escalated issues in the active feed.
- **Preconditions:** On `http://localhost:5173/`.
- **Steps:**
  1. Click the `"Filter Escalated →"` link inside the top Alert Strip.
  2. Observe the filter bar pills and the active issue table rows.
  3. In the filter bar, click the **"Escalated"** toggle pill to turn it off.
- **Expected Result:**
  - Step 1 activates the `Escalated` toggle pill (amber highlight); URL or query parameter updates to `escalatedOnly=true`.
  - Active issue table immediately filters to show only issues with `EscalationLevel > 0` or `Severity = Critical` (displaying `RX-ESCALATED-099`).
  - Step 3 deactivates the filter, restoring the full list of active issues.

---

## TC-07-03 — Visual Escalation Badge Chips on Dashboard Feed Rows (`↑ Escalated (L{N})`)

- **Objective:** Verify that escalated issues display an amber `↑ Escalated (L2)` badge chip in the dashboard feed table.
- **Preconditions:** On `http://localhost:5173/`.
- **Steps:**
  1. Locate the row for `RX-ESCALATED-099`.
  2. Inspect the badges displayed in the Severity and Title columns.
- **Expected Result:**
  - Row displays a `Critical` severity badge (rose).
  - Beside the severity badge, an amber badge chip is rendered: `↑ Escalated (L2)`.
  - Hovering over the badge shows tooltip explaining dynamic multi-step SLA/recurrence escalation.

---

## TC-07-04 — Escalation History & SLA Audit Trail in Issue Activity Accordion (`G-2`)

- **Objective:** Verify that opening the issue detail workbench for an escalated issue displays chronological `SeverityEscalated` audit events with actor `"System"` and specific breach justifications.
- **Preconditions:** Authenticated as `ahmad.budi`; navigate to `http://localhost:5173/issues/d0999999-9999-9999-9999-999999999901` (`RX-ESCALATED-099`).
- **Steps:**
  1. Scroll down to the **Activity & Audit Trail** section.
  2. Expand the accordion.
  3. Inspect the activity log entries for `SeverityEscalated`.
- **Expected Result:**
  - Activity log displays `SeverityEscalated` events:
    - Actor: `System`.
    - Activity type badge: Amber `Severity Escalated`.
    - Summary descriptions document exact breach rationale (e.g., `"Issue unacknowledged for 45 minutes (SLA window: 30m). Escalated Low -> Medium."` and `"Issue unacknowledged for 75 minutes. Escalated Medium -> High."` or recurrence threshold breach).

---

# 12. Test Suite: Autonomous Background Supervision & Self-Healing Loop

## TC-08-01 — Silence Detection Past 2× Cadence & Fail-Safe Synthetic Issue Creation (`BR-PRIMA-010`, `BR-PRIMA-005`, `DEC-003`)

- **Objective:** Verify that when an active detector exceeds 2× its expected cadence without reporting telemetry, the background supervisor autonomously marks it `Silent` and synthesizes a `SILENT:{DetectorId}` infrastructure anomaly issue.
- **Preconditions:** Authenticated as `ahmad.budi`; local backend running with `MonitoringSupervisorWorker` active.
- **Steps:**
  1. Navigate to `/detectors/e2c04068-8314-469b-8326-880bebbff71d` (`Patient Admission Discharge Sync`).
  2. Observe detector health status (`Silent`).
  3. Inspect the linked issues section.
  4. Click the linked issue `SILENT:e2c04068-8314-469b-8326-880bebbff71d`.
- **Expected Result:**
  - Detector health is `Silent` (amber).
  - Linked issue is created with:
    - Title: `"Silent Detector: Patient Admission Discharge Sync has missed cadence"`
    - Scope: `Infrastructure`
    - Severity: `High`
    - Trace String explains: Cadence is 600s, silence threshold is 1200s (2× cadence).
  - Activity log records: `Created` by `System`.

---

## TC-08-02 — Cadence-Multiplied Prolonged Silence Occurrences (`DEC-005`)

- **Objective:** Verify that prolonged detector silence crossing 3×, 4×, etc. cadence windows appends occurrences capped to 1 per cadence window rather than spamming on every 60-second supervisor tick.
- **Preconditions:** Inspect the database or UI occurrences timeline for the silent detector issue `SILENT:e2c04068-8314-469b-8326-880bebbff71d`.
- **Steps:**
  1. On the issue detail workbench for `SILENT:...`, expand the **Occurrences Timeline**.
  2. Inspect the timestamps and count of occurrences.
- **Expected Result:**
  - Occurrences reflect cadence-multiplied milestones (`multiples >= 3` and `multiples > (OccurrenceCount + 1)` per `DEC-005`).
  - Occurrences are not created on every 60-second worker tick, maintaining clean audit fidelity without spamming.

---

## TC-08-03 — Ingestion Execution Failure Handling & Automatic Health Recovery (`DEC-004`)

- **Objective:** Verify that ingesting an `ExecutionFailed` telemetry payload marks detector `Failing` and synthesizes `FAIL:{id}`, and subsequent clean telemetry resets detector health to `Healthy` with a `DetectorRecovered` activity log while keeping the issue open for EDP verification.
- **Preconditions:** Local backend running at `http://localhost:5005`.
- **Steps:**
  1. In terminal, simulate clean telemetry recovery for the failing detector:
     ```powershell
     node src/tools/telemetry-simulator/simulate.mjs passed --detector b6a9c023-4d5e-5f6a-7b8c-9d0e1f2a3b4c
     ```
  2. In browser, navigate to `/detectors/b6a9c023-4d5e-5f6a-7b8c-9d0e1f2a3b4c`.
  3. Observe detector health status badge.
  4. Navigate to the linked issue `FAIL:b6a9c023-4d5e-5f6a-7b8c-9d0e1f2a3b4c`.
  5. Inspect issue status and activity log.
- **Expected Result:**
  - Detector health status transitions from `Failing` to `Healthy` (emerald).
  - The `FAIL:...` issue remains **Open** (governance preserved; issue is NOT automatically closed without EDP verification per `DEC-004`).
  - An activity log entry is appended to the issue:
    - Activity Type: `DetectorRecovered`
    - Actor: `System`
    - Summary: `"Detector resumed healthy reporting via run '...'. Issue remains open for EDP verification."`

---

## TC-08-04 — Dynamic Unacknowledged SLA Severity Escalation (+1 Step Capped at Critical) (`DEC-006`, `DEC-007`)

- **Objective:** Verify that unacknowledged issues in `Open` or `Reopened` status past the SLA timeout window are stepped up by +1 severity level (`Low` -> `Medium` -> `High` -> `Critical`) and capped at `Critical`.
- **Preconditions:** Inspect seeded issue `RX-ESCALATED-099` (`d0999999-9999-9999-9999-999999999901`).
- **Steps:**
  1. Navigate to `http://localhost:5173/issues/d0999999-9999-9999-9999-999999999901`.
  2. Inspect the Severity badge and Escalation badge in the header.
  3. Expand the Activity & Audit Trail accordion.
- **Expected Result:**
  - Initial severity was `Low`; current severity is `Critical`.
  - Escalation Level is `2`.
  - Activity log documents multi-step progression:
    - `SeverityEscalated`: Low -> Medium (SLA window breached).
    - `SeverityEscalated`: Medium -> Critical (SLA window breached).
  - Further supervisor passes do not escalate beyond `Critical`.

---

## TC-08-05 — Recurrence Threshold Severity Escalation (>= 5 Occurrences)

- **Objective:** Verify that an actively investigated issue (`Open`, `Acknowledged`, or `Investigating`) that accumulates 5 or more occurrences is automatically escalated by +1 severity step.
- **Preconditions:** Backend running with supervisor worker active.
- **Steps:**
  1. Inspect the activity logs of issues with `OccurrenceCount >= 5`.
- **Expected Result:**
  - When occurrence #5 arrives on an issue below `Critical`, supervisor worker detects threshold breach per `DEC-007`.
  - Severity steps up by +1 level.
  - Activity log records: `ActivityType = "SeverityEscalated"`, `Summary = "Issue recurred 5 times across monitoring cycles."`

---

# 13. Test Suite: Complete End-to-End Autonomous Supervision Smoke Test

## TC-09-01 — Comprehensive End-to-End Operational Lifecycle Verification

- **Objective:** Execute the entire Stage 3 autonomous supervision, detector portfolio, and webhook verification loop in a continuous sequence against the running application.
- **Preconditions:** Both Backend API (`http://localhost:5005`) and Frontend Client (`http://localhost:5173`) are running. Authenticated as `ahmad.budi`.
- **Steps:**
  1. **Portfolio Check:** Navigate to `http://localhost:5173/detectors`. Verify 4-state summary bar displays healthy, failing, silent, and paused counts.
  2. **Sidebar Alert Check:** Verify `Detectors` sidebar item displays animated alert dot with count `2`.
  3. **Register Detector:** Click `+ Register Detector`. Register `End-to-End Test Prober` with module `Radiology`, scope `ProcessFlow`, cadence `5` Minutes, and webhook `http://localhost:8088/probe`.
  4. **Verify Creation:** Confirm redirection to `/detectors/:id`; verify health is `Healthy` and admin status is `Active`.
  5. **Inspect Form Immutability:** Click `Edit`. Confirm Detector Name is disabled with lock icon. Change description and click `Save Changes`.
  6. **Administrative Governance:** Click `Pause`. Confirm status changes to `Paused`. Click `Resume`. Confirm status returns to `Active`.
  7. **Historical Run Log:** Click `View Full Run Log →`. Confirm 7-day presets, outcome filters, and virtualized table.
  8. **Dashboard Escalation Check:** Navigate to `http://localhost:5173/`. Verify top Alert Strip `⚠ 1 issue escalated`. Click `Filter Escalated →`. Verify `RX-ESCALATED-099` is isolated.
  9. **Workbench Navigation:** Click `RX-ESCALATED-099`. In header, click detector link `Prescription Stock Deductor`. Confirm instant navigation to `/detectors/:id`.
  10. **Automated Verification Loop:** Navigate to `http://localhost:5173/issues/d0444444-4444-4444-4444-444444444401` (`LIS-QUEUE-4412`).
      - Verify Action Bar displays primary `Trigger Verification Re-run`.
      - Record clean verification (via automated re-run or manual `M-03`).
      - Confirm `BR-PRIMA-004` Verification Gate unlocks.
      - Resolve issue and verify read-only state.
- **Expected Result:**
  - All 10 workflow stages execute cleanly without browser errors, network failures, or unhandled exceptions.
  - Verification Gate strictly protects resolution integrity.
  - System delivers complete autonomous supervision, detector portfolio management, and webhook verification.

---

# 14. Verification Summary & Next Workflow Stages

### 14.1 Acceptance Gate Criteria
The Stage 3 implementation achieves acceptance when:
- [ ] Automated Quality Gate passes 100% (TC-001: 295 backend tests, TC-002: clean frontend build, TC-003: Swagger API integrity).
- [ ] All 10 manual test suites (Sections 5 through 13) are executed by human testers and recorded in `TEST-EXECUTION`.
- [ ] Every test case achieves `PASS`. Any observed defect is documented with full evidence and handed off to the **Issuer** via `ics-issue-creation` as a `BUG` ISSUE.

### 14.2 Next Workflow Stage & Owning Role
Upon successful execution and validation of this test package:
1. **Human Tester:** Executes the test suites defined in this package, recording results, timestamps, and evidence via `TEST-EXECUTION` assisted by `ica-tester` (`ics-testing-assistance`).
2. **Tester (`ica-tester`):** Grants `TEST PASSED` gate once all test cases achieve `PASS` (or all defects are resolved through formal bug issues).
3. **SDLC Transition:** Following `TEST PASSED`, the feature transitions to **Stage 4** of the PrIMA Development Roadmap (`docs/PRIMA-DEVELOPMENT-ROADMAP.md`): *Retrospective Reporting & Period-Close Analytics* under the governance of the **Architect** (`ica-architect`).
