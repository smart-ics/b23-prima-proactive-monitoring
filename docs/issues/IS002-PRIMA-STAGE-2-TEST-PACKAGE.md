---
Title: PrIMA Stage 2 — Issue Triage, Investigation & Verification-Gated Resolution Test Package
Code: PRIMA-STAGE-2
Artifact: TEST-PACKAGE
Version: 1.0
LastUpdated: 2026-11-21
Status: READY-FOR-TESTING
---

# 1. Overview & Purpose

This test package provides human-executable verification procedures for **PrIMA Stage 2: Issue Triage, Investigation & Verification-Gated Resolution** (`Complete End-to-End Manual Issue Resolution Loop` milestone).

The test suite validates the integrated system as a whole against the live running application, covering:
1. **Dashboard S-01 Linkage & Router Navigation:** Seamless transition from the active issue feed (`S-01`) to the operational workbench (`S-02`), copy button isolation (`@click.stop`), header back navigation, and authentication navigation guards.
2. **Issue Detail Workbench (`S-02`):** Desktop-first 2-column asymmetric workbench (`grid-cols-1 lg:grid-cols-12`), sticky header navigation, WIB operational clock, detector Stage 3 guardrail tooltip, 2x2 metadata well, null-safe IDR financial comparison strip, and dark monospace trace blocks with one-click copy.
3. **Structured Occurrences Timeline (`C-2`):** Collapsible chronological occurrences sorted newest-first (#N, #N-1, ...), relative and Asia/Jakarta (WIB) timestamps, and non-disruptive inline expansion stability across 10-second polling ticks.
4. **Issue Triage & Operational Accountability (`B-1`, `B-2`, `BR-PRIMA-008`):** Single-action "Take It" self-triage flow, explicit assignment to seeded EDP officers (`POST /api/issues/{id}/assign`), "Start Investigating" transition, and status preservation during reassignment (`OQ-007`).
5. **Append-Only Investigation Notes Stream (`C-3`):** Chronological notes history attributed to authenticated officers, note composer with `Ctrl+Enter` shortcut, and local draft preservation across background refetches.
6. **Remediation & Verification Loop (`D-1`, `D-3`, Modals `M-02` & `M-03`):** Documented operational remediation logging, dynamic Action Bar verification promotion (`OQ-001`), manual verification recording (`CleanPassed` vs. `AnomalyStillPresent`), and failed verification supersession on remediation retry (`GAP-005`).
7. **Mandatory Verification Gate Enforcement (`D-4`, `BR-PRIMA-004`):** Strict verification gate lockout preventing resolution without an un-superseded clean verification record, unlocking upon clean verification, and transitioning to read-only closed status upon resolution.
8. **Documented Issue Dismissal (`B-3`, Modal `M-01`, `BR-PRIMA-006`):** Structured dismissal reasons and client/server validation enforcing minimum 20 characters justification rationale with live character counter.
9. **Active Recurrence Verification Invalidation (`GAP-005`, `OQ-005`):** Automatic supersession of active verification records when recurring telemetry is ingested on an actively investigated anomaly.
10. **Immutable Activity Audit Trail (`BR-PRIMA-009`):** Collapsed-by-default PrimeVue Accordion displaying chronological lifecycle events from `dbo.IssueActivityLogs`.

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

### 2.3 Pre-seeded Demo Fixtures
When `SeedDemoData == true` (default in Development mode), `DevelopmentDemoDataSeeder.cs` idempotently seeds 7 realistic issues representing each lifecycle state:

| Entity Reference | Status | Severity | Detector | Deterministic ID | Key Characteristics |
|---|---|---|---|---|---|
| `BILL-2026-OPEN-001` | `Open` | Critical | Billing Journal Balance Checker | `d0777777-7777-7777-7777-777777777701` | Unassigned, Debit/Credit variance, ready for triage ("Take It" / "Assign"). |
| `RX-202611-042` | `Acknowledged` | High | Prescription Stock Deductor | `d0333333-3333-3333-3333-333333333301` | Assigned to `siti.rahma`, initial diagnostic note present, ready for "Start Investigating". |
| `LIS-QUEUE-4412` | `Investigating` | Medium | Laboratory Order Sync Prober | `d0444444-4444-4444-4444-444444444401` | Assigned to `ahmad.budi`, `ServiceRestart` remediation recorded, awaiting verification (`M-03`). |
| `EMR-DB-REPL02` | `Investigating` | High | EMR Database Replication Monitor | `d0555555-5555-5555-5555-555555555501` | Assigned to `ahmad.budi`, failed verification (`CleanPassed = false`), rose banner, ready for remediation retry. |
| `SEP-202611-9982` | `Investigating` | Medium | Patient Admission Discharge Sync | `d0666666-6666-6666-6666-666666666601` | Assigned to `siti.rahma`, clean verification (`CleanPassed = true`), gate unlocked, ready for "Mark Resolved". |
| `INV-2026-RESOLVED-001` | `Resolved` | Critical | Billing Journal Balance Checker | `d0111111-1111-1111-1111-111111111101` | Resolved today, clean verification, remediation history, read-only state. |
| `ADM-2026-DISMISSED-001` | `Dismissed` | Low | Patient Admission Discharge Sync | `d0222222-2222-2222-2222-222222222202` | Dismissed with valid rationale (`BR-PRIMA-006`), read-only state. |

### 2.4 Telemetry Simulator CLI
Located at `src/tools/telemetry-simulator/simulate.mjs`:
- Usage: `node src/tools/telemetry-simulator/simulate.mjs [scenario] [options]`
- Options: `--url <endpoint>` (defaults to `http://localhost:5005/api/ingestion/telemetry`), `--key <key>` (defaults to `prima-dev-key-alpha-99`).

---

# 3. Test Execution Sequence

Testers should follow this recommended execution order:

```text
Section 4: Automated Quality Gate (TC-001 to TC-003)
   ↓
Section 5: Dashboard S-01 Linkage & Router Navigation (TC-01-01 to TC-01-04)
   ↓
Section 6: Issue Detail Workbench Layout & Evidence (TC-02-01 to TC-02-05)
   ↓
Section 7: Issue Triage & Accountability Lifecycle (TC-03-01 to TC-03-05)
   ↓
Section 8: Remediation & Verification Cycle (TC-04-01 to TC-04-06)
   ↓
Section 9: Mandatory Verification Gate & Resolution (TC-05-01 to TC-05-04)
   ↓
Section 10: Documented Issue Dismissal (TC-06-01 to TC-06-04)
   ↓
Section 11: Active Recurrence Verification Invalidation (TC-07-01 to TC-07-02)
   ↓
Section 12: Complete End-to-End Manual Resolution Smoke Test (TC-08-01)
```

---

# 4. Automated Quality Gate

## TC-001 — Backend Automated Regression & Domain Test Suite

- **Objective:** Verify that all 177 automated backend tests pass cleanly, verifying domain invariants, verification gate rules (`BR-PRIMA-004`), dismissal rationale validation (`BR-PRIMA-006`), recurrence invalidation (`GAP-005`), API endpoints, composite projections, and demo seeder.
- **Preconditions:** Host has .NET 8 SDK installed; local SQL Server running.
- **Steps:**
  1. Open terminal in the repository root directory.
  2. Execute:
     ```powershell
     dotnet test src/backend/PrIMA.slnx
     ```
- **Expected Result:**
  - All 177 test cases execute and pass cleanly (`Failed: 0, Passed: 177, Skipped: 0`).
  - Duration is approximately 8–12 seconds.
  - Test output concludes with: `Passed! - Failed: 0, Passed: 177, Skipped: 0`.

---

## TC-002 — Frontend Production Build & TypeScript Typecheck

- **Objective:** Verify that the frontend application compiles cleanly into production assets without TypeScript compiler or Vite bundler errors.
- **Preconditions:** Node.js v20+ or v22 LTS installed.
- **Steps:**
  1. Open terminal in the repository root directory.
  2. Execute:
     ```powershell
     npm --prefix src/frontend run build
     ```
- **Expected Result:**
  - `vue-tsc -b` completes with 0 errors.
  - Vite bundles production assets into `src/frontend/dist/` successfully with 0 errors.

---

## TC-003 — OpenAPI Specification Integrity & Contract Verification

- **Objective:** Verify that the OpenAPI Swagger endpoint exposes all 10 Stage 2 issue lifecycle endpoints, the officer lookup endpoint, and generated TypeScript types align with backend contracts.
- **Preconditions:** Backend running at `http://localhost:5005`.
- **Steps:**
  1. Open browser and navigate to `http://localhost:5005/swagger/index.html`.
  2. Inspect the **Issues** and **Officers** tag groups.
- **Expected Result:**
  - The **Officers** group contains `GET /api/officers`.
  - The **Issues** group contains:
    - `GET /api/issues/{id}`
    - `POST /api/issues/{id}/take-it`
    - `POST /api/issues/{id}/assign`
    - `POST /api/issues/{id}/acknowledge`
    - `POST /api/issues/{id}/start-investigation`
    - `POST /api/issues/{id}/notes`
    - `POST /api/issues/{id}/remediation`
    - `POST /api/issues/{id}/verification/manual`
    - `POST /api/issues/{id}/resolve`
    - `POST /api/issues/{id}/dismiss`
  - Each endpoint displays appropriate HTTP status codes (e.g., 200, 201, 204, 401, 404, 409, 422 ProblemDetails).

---

# 5. Test Suite: Dashboard S-01 Linkage & Router Navigation

## TC-01-01 — Table Row Click Navigation to Issue Detail Workbench

- **Objective:** Verify that clicking an active issue table row on the dashboard (`S-01`) navigates directly to that issue's detail workbench (`S-02`) at `/issues/:id`.
- **Preconditions:** Authenticated as `ahmad.budi`; on Dashboard (`http://localhost:5173/`).
- **Steps:**
  1. Hover cursor over the table row for `BILL-2026-OPEN-001`.
  2. Observe cursor styling.
  3. Click anywhere on the row body (outside the copy button).
- **Expected Result:**
  - Row shows subtle hover highlight (`hover:bg-slate-800/40`) and mouse cursor is pointer (`cursor-pointer`).
  - Browser URL immediately updates to `http://localhost:5173/issues/d0777777-7777-7777-7777-777777777701`.
  - Workbench header displays: `BILL-2026-OPEN-001` with `Critical` and `Open` badges.

---

## TC-01-02 — Correlation Key Copy Button Click Isolation

- **Objective:** Verify that clicking the copy icon button inside an issue feed row copies the correlation key without triggering row navigation (`@click.stop`).
- **Preconditions:** Authenticated as `ahmad.budi`; on Dashboard (`http://localhost:5173/`).
- **Steps:**
  1. Locate the row for `BILL-2026-OPEN-001`.
  2. Click specifically on the copy icon button next to the correlation key.
- **Expected Result:**
  - The icon briefly changes to a green checkmark or displays a copy confirmation tooltip.
  - The correlation key `BILL-2026-OPEN-001` is copied to the operating system clipboard.
  - The browser remains on `http://localhost:5173/` and does NOT navigate to `/issues/:id`.

---

## TC-01-03 — Sticky Header Back Navigation to Dashboard

- **Objective:** Verify that clicking the back button in the `S-02` sticky header safely navigates the officer back to the active issue dashboard (`S-01`).
- **Preconditions:** On `http://localhost:5173/issues/d0777777-7777-7777-7777-777777777701`.
- **Steps:**
  1. Locate the chevron-left back button on the far left of the sticky header.
  2. Click the back button.
- **Expected Result:**
  - Application navigates back to `http://localhost:5173/` (Dashboard).
  - Active issue feed and summary counters are displayed without full page reload.

---

## TC-01-04 — Non-Existent Issue ID Handling (HTTP 404 & Friendly Error State)

- **Objective:** Verify that navigating directly to an invalid or non-existent issue GUID displays a clean error fallback screen without crashing the SPA.
- **Preconditions:** Authenticated as `ahmad.budi`.
- **Steps:**
  1. In the browser address bar, enter: `http://localhost:5173/issues/00000000-0000-0000-0000-000000000999`.
  2. Press Enter.
- **Expected Result:**
  - Loading skeleton briefly displays while querying the backend.
  - Network request returns HTTP `404 Not Found`.
  - An error state card is presented informing the user: `"Issue Not Found"` or `"Unable to load issue details"`.
  - An action button is visible: `"Return to Dashboard"` (or `"Back to Safety"`).
  - Clicking the return button navigates back to `/`.

---

# 6. Test Suite: Issue Detail Workbench Layout & Evidence (Screen S-02)

## TC-02-01 — Sticky Header Badges, WIB Clock & Stage 3 Tooltip Guardrail

- **Objective:** Verify that the sticky top header correctly displays issue severity, status, live Asia/Jakarta (WIB) time, copyable correlation key, and the non-navigating detector badge (`OQ-006`).
- **Preconditions:** Authenticated as `ahmad.budi`; navigate to `http://localhost:5173/issues/d0777777-7777-7777-7777-777777777701`.
- **Steps:**
  1. Inspect the sticky header bar at the top of the viewport.
  2. Check the severity badge and status badge.
  3. Hover mouse over the detector badge `"Billing Journal Balance Checker"`.
  4. Click on the detector badge.
  5. Click the copy icon button next to the correlation key in the header.
- **Expected Result:**
  - Header displays `Critical` severity badge (crimson accent) and `Open` status badge (blue accent).
  - Live WIB time is visible and ticks in real time.
  - Hovering over the detector badge displays a tooltip: `"Detector portfolio detail available in Stage 3"`.
  - Clicking the detector badge does NOT navigate away or trigger any broken route.
  - Clicking the copy button copies `BILL-2026-OPEN-001` to clipboard with visual confirmation feedback.

---

## TC-02-02 — Latest Telemetry Evidence & Null-Safe IDR Financial Strip

- **Objective:** Verify that the Latest Evidence Card renders the 2x2 metadata well and formats financial fields strictly in Indonesian Rupiah (`IDR`), hiding the strip cleanly when financial data is null.
- **Preconditions:** Authenticated as `ahmad.budi`.
- **Steps:**
  1. Navigate to `http://localhost:5173/issues/d0777777-7777-7777-7777-777777777701` (`BILL-2026-OPEN-001` with financial variance).
  2. Observe the **Latest Evidence** card in the left column.
  3. Inspect the 2x2 metadata well (Module, Source Table, Record Keys, Run ID).
  4. Inspect the Financial Comparison Strip.
  5. Now navigate to `http://localhost:5173/issues/d0555555-5555-5555-5555-555555555501` (`EMR-DB-REPL02` with non-financial infrastructure data).
- **Expected Result:**
  - For `BILL-2026-OPEN-001`:
    - 2x2 well displays: Module: `Billing`, Source Table: `dbo.BillingJournal`, Record Keys: `JournalId: 88129`.
    - Financial Strip is visible with 3 columns:
      - **Debit Total:** Formatted in IDR (e.g. `Rp 15.450.000,00` or equivalent Indonesian currency format).
      - **Credit Total:** Formatted in IDR (e.g. `Rp 14.950.000,00`).
      - **Variance Delta:** Formatted in IDR with rose highlight (e.g. `Rp 500.000,00`).
  - For `EMR-DB-REPL02`:
    - Metadata well displays infrastructure details (e.g. `ReplMonitor`).
    - The Financial Comparison Strip is completely hidden (null-safe rendering).

---

## TC-02-03 — Monospace Trace Block & One-Click Copy

- **Objective:** Verify that diagnostic trace details are formatted in a dark monospace code block with an operational copy-to-clipboard button.
- **Preconditions:** On `http://localhost:5173/issues/d0777777-7777-7777-7777-777777777701`.
- **Steps:**
  1. In the **Latest Evidence** card, scroll to the Trace String block.
  2. Observe styling and typography.
  3. Click the **Copy Trace** button in the upper right of the trace block.
  4. Paste into a text editor (e.g. Notepad).
- **Expected Result:**
  - Trace block is styled with dark slate background, monospace font (`font-mono`), and horizontal scrollbar if needed.
  - Clicking **Copy Trace** briefly shows a checkmark or toast `"Trace copied to clipboard"`.
  - The pasted text matches the exact raw trace content without truncating stack traces or JSON.

---

## TC-02-04 — Collapsible Chronological Occurrences Timeline & Polling Stability

- **Objective:** Verify that occurrences are listed newest-first (#N, #N-1, ...), can be expanded inline, and do not collapse or flicker during the 10-second background polling cycle.
- **Preconditions:** On `http://localhost:5173/issues/d0777777-7777-7777-7777-777777777701`.
- **Steps:**
  1. Scroll down to the **Occurrences Timeline** section in the left column.
  2. Observe occurrence numbers and timestamps (relative and WIB format).
  3. Click an occurrence item row to expand its inline telemetry evidence details.
  4. Confirm the expansion opens with detailed record keys and variance information.
  5. Wait 15–20 seconds to allow at least one 10-second background polling cycle to occur.
- **Expected Result:**
  - Occurrences are ordered newest-first (e.g., `#3`, `#2`, `#1`).
  - Timestamps include both relative time (e.g., `10 minutes ago`) and absolute WIB time (`Asia/Jakarta`).
  - Clicking an occurrence expands its inline evidence details.
  - During and after the 10-second polling cycle, the expanded occurrence remains open and does NOT collapse or scroll unexpectedly.

---

## TC-02-05 — Activity Log Accordion Audit Trail Verification (`BR-PRIMA-009`)

- **Objective:** Verify that the chronological audit log in `dbo.IssueActivityLogs` is rendered inside a collapsed-by-default PrimeVue Accordion and displays complete event attribution.
- **Preconditions:** Navigate to `http://localhost:5173/issues/d0111111-1111-1111-1111-111111111101` (`INV-2026-RESOLVED-001`).
- **Steps:**
  1. Scroll to the bottom of the right column to locate the **Activity Log** card.
  2. Verify that the accordion is collapsed by default.
  3. Click the accordion header to expand the activity history.
  4. Review the timeline of events.
- **Expected Result:**
  - Accordion is collapsed by default upon page load.
  - Expanding the accordion displays a chronological audit stream including events such as:
    - `Created` (Detector Ingestion)
    - `InvestigationStarted` / `Assigned` (Officer)
    - `NoteAdded` (Officer)
    - `RemediationRecorded` (Officer)
    - `VerificationRecorded` (Officer)
    - `Resolved` (Officer)
  - Each item displays: Activity Type badge, Actor Name, Summary description, and relative + WIB timestamp.

---

# 7. Test Suite: Issue Triage & Accountability Lifecycle

## TC-03-01 — Single-Action "Take It" Flow on Open Issue

- **Objective:** Verify that an unassigned `Open` issue can be claimed in a single action via the "Take It" button, transitioning it immediately to `Investigating` and assigning it to the authenticated officer.
- **Preconditions:** Authenticated as `ahmad.budi`; navigate to `http://localhost:5173/issues/d0777777-7777-7777-7777-777777777701` (`BILL-2026-OPEN-001`, status `Open`, unassigned).
- **Steps:**
  1. Observe the Action Bar in the sticky header:
     - Primary button: `Take It` (blue/indigo accent).
     - Secondary button: `Assign to…`.
     - Danger button: `Dismiss`.
  2. Click the **Take It** button.
- **Expected Result:**
  - Button displays a brief loading state.
  - Toast notification appears: `"Issue claimed. You are now investigating this issue."`.
  - Issue status badge changes immediately from `Open` to `Investigating` (amber/orange accent).
  - In the right-column **Ownership** card, Assignee updates to `"Ahmad Budi"`.
  - In the **Verification Gate** card, Step 1 ("Investigation Started") is checked.
  - Action Bar dynamically updates: primary button becomes `Record Remediation` (`M-02`), and `Mark Resolved` is visible but disabled with lock icon (`🔒 Remediation & verification required`).

---

## TC-03-02 — Explicit Assignment via "Assign Officer" Modal

- **Objective:** Verify that clicking "Assign to…" opens the Assign Officer modal, fetches active officers from `GET /api/officers`, and transitions an `Open` issue to `Acknowledged` (`OQ-002`).
- **Preconditions:** Ingest a fresh open finding via telemetry simulator:
  ```powershell
  node src/tools/telemetry-simulator/simulate.mjs new-finding
  ```
  Authenticated as `ahmad.budi`; on Dashboard (`http://localhost:5173/`).
- **Steps:**
  1. Click on the newly created issue row in the feed.
  2. In the sticky header Action Bar, click **Assign to…**.
  3. Verify that the **Assign Officer** modal opens (`AssignOfficerModal.vue`).
  4. Open the Officer dropdown.
  5. Select `Siti Rahma` (`SystemAnalyst`).
  6. Click **Assign Officer** button.
- **Expected Result:**
  - Dropdown lists all active seeded officers (`Ahmad Budi`, `Siti Rahma`, `Joko Santoso`) sorted alphabetically with role badges.
  - Submitting assignment closes the modal and displays a success toast.
  - Issue status badge transitions from `Open` to `Acknowledged` (purple accent).
  - Right-column Assignee updates to `"Siti Rahma"`.
  - Acknowledged timestamp is set to current time.
  - Action Bar updates: for Siti Rahma, primary button is now `Start Investigating`.

---

## TC-03-03 — "Start Investigating" Transition from Acknowledged Status

- **Objective:** Verify that an assigned officer can begin active investigation on an `Acknowledged` issue, moving it to `Investigating`.
- **Preconditions:** Authenticated as `siti.rahma`; navigate to `http://localhost:5173/issues/d0333333-3333-3333-3333-333333333301` (`RX-202611-042`, status `Acknowledged`, assigned to `siti.rahma`).
- **Steps:**
  1. Observe the Action Bar: primary button is **Start Investigating** (emerald/teal accent).
  2. Click **Start Investigating**.
- **Expected Result:**
  - Status badge updates from `Acknowledged` to `Investigating`.
  - Toast notification appears: `"Investigation started."`.
  - Action Bar transitions to the `Investigating` state matrix (primary: `Record Remediation`, secondary: `Add Note`, `Reassign`, `Dismiss`).
  - An activity log entry is appended: `InvestigationStarted` by `Siti Rahma`.

---

## TC-03-04 — Investigating Reassignment Status Preservation (`OQ-007`)

- **Objective:** Verify that reassigning an issue that is already in `Investigating` status updates the assignee while preserving the `Investigating` status without lifecycle regression.
- **Preconditions:** Authenticated as `siti.rahma`; on `http://localhost:5173/issues/d0333333-3333-3333-3333-333333333301` (now in `Investigating`).
- **Steps:**
  1. In the right-column **Ownership & Timestamps** card, click the **Reassign** button.
  2. In the Assign Officer modal, select `Ahmad Budi`.
  3. Click **Assign Officer**.
- **Expected Result:**
  - Modal closes with success toast.
  - Status remains `Investigating` (does NOT regress to `Acknowledged` or `Open`).
  - Assignee updates to `"Ahmad Budi"`.
  - Activity log records: `Assigned` (or `Reassigned`) to `Ahmad Budi` by `Siti Rahma`.

---

## TC-03-05 — Append-Only Diagnostic Notes Stream & Draft Preservation

- **Objective:** Verify that diagnostic notes can be appended with `Ctrl+Enter`, attributed to the officer, and uncommitted text drafts survive background 10-second polling ticks.
- **Preconditions:** Authenticated as `ahmad.budi`; on `http://localhost:5173/issues/d0444444-4444-4444-4444-444444444401`.
- **Steps:**
  1. Scroll down to the **Investigation Notes** card in the left column.
  2. Type in the note composer: `"Verified database lock contention on LIS table. Preparing service restart."`.
  3. Wait 15–20 seconds (observe the background sync spinner in the header tick at least once).
  4. Confirm that the typed text remains intact in the textarea.
  5. Press **Ctrl + Enter** (or **Cmd + Enter** on macOS) while inside the textarea.
- **Expected Result:**
  - Draft text is not cleared or interrupted by background polling ticks.
  - Pressing `Ctrl+Enter` submits the note.
  - Composer input is cleared.
  - New note immediately appears at the bottom of the notes stream displaying:
    - Author: `"Ahmad Budi"`
    - Timestamp: `"Just now"` / current WIB time.
    - Content: `"Verified database lock contention on LIS table. Preparing service restart."`.
  - Activity log records `NoteAdded`.

---

# 8. Test Suite: Remediation & Verification Cycle

## TC-04-01 — Record Remediation Action via Modal M-02

- **Objective:** Verify that an officer can record an operational remediation action using modal `M-02`, documenting category and SOP description.
- **Preconditions:** Authenticated as `ahmad.budi`; navigate to an issue in `Investigating` status without remediation (e.g. `BILL-2026-OPEN-001` claimed in TC-03-01).
- **Steps:**
  1. In the sticky Action Bar, click **Record Remediation** (primary button).
  2. Observe that modal `RecordRemediationModal.vue` (`M-02`) opens.
  3. Inspect the Category dropdown options (`DataCorrection`, `ServiceRestart`, `ConfigurationAdjustment`, `CodeRemediationRequested`).
  4. Select Category: `DataCorrection`.
  5. In Summary Description, enter: `"Executed journal balancing correction script fix_journal_88129.sql against BillingJournal table."`.
  6. Click **Save Remediation**.
- **Expected Result:**
  - Modal closes and displays success toast: `"Remediation action recorded successfully."`.
  - Right-column **Remediation History** card displays the newly recorded remediation with `DataCorrection` badge, officer name `Ahmad Budi`, timestamp, and description.
  - In **Verification Gate** card, Step 2 ("Remediation Recorded") is now checked.
  - Activity log records `RemediationRecorded`.

---

## TC-04-02 — Dynamic Action Bar Verification Promotion (`OQ-001`)

- **Objective:** Verify that once remediation is recorded, the Action Bar dynamically promotes `Manual Verification` (`M-03`) to the primary action button.
- **Preconditions:** Completed TC-04-01; issue is in `Investigating` with remediation recorded.
- **Steps:**
  1. Observe the sticky Action Bar in the header.
  2. Observe the primary and secondary buttons.
- **Expected Result:**
  - Primary button is now **Manual Verification** (blue/indigo accent, `M-03`).
  - **Mark Resolved** button is visible but still disabled (`bg-slate-700/50 text-slate-400 cursor-not-allowed`) with tooltip/indicator: `"🔒 Clean verification required"`.
  - Secondary buttons include: `Add Note`, `Dismiss`.

---

## TC-04-03 — Record Failed Manual Verification (`AnomalyStillPresent`)

- **Objective:** Verify that recording a failed manual verification outcome (`AnomalyStillPresent`) leaves the issue in `Investigating`, surfaces a rose warning banner, and keeps the Verification Gate locked.
- **Preconditions:** Authenticated as `ahmad.budi`; on issue with remediation recorded.
- **Steps:**
  1. Click the primary **Manual Verification** button in the Action Bar.
  2. In the `ManualVerificationModal.vue` dialog, select the second card: **Anomaly Still Present / Failed** (`AnomalyStillPresent`).
  3. Observe that the selected card displays a rose highlight and warning indicator.
  4. In Evidence Notes, enter: `"Re-ran journal balance query; debit and credit still differ by Rp 500.000."`.
  5. Click **Record Verification**.
- **Expected Result:**
  - Modal closes and displays toast notification.
  - In the right column:
    - **Verification Record** card displays outcome chip: `"Anomaly Still Present"` (rose badge).
    - A prominent rose alert banner appears: `"Latest verification failed. Anomaly is still present. Further remediation is required."`.
    - **Verification Gate** card: Step 3 ("Verification Passed Clean") remains unchecked with a red/rose warning state.
  - In sticky Action Bar:
    - Primary button updates to **Record Remediation (Retry)**.
    - **Mark Resolved** button remains strictly locked.

---

## TC-04-04 — Failed Verification State & Remediation Retry Action Bar Matrix

- **Objective:** Verify that pre-seeded fixture `EMR-DB-REPL02` accurately renders the failed verification state and presents the remediation retry workflow.
- **Preconditions:** Authenticated as `ahmad.budi`; navigate to `http://localhost:5173/issues/d0555555-5555-5555-5555-555555555501` (`EMR-DB-REPL02`).
- **Steps:**
  1. Inspect the sticky header Action Bar.
  2. Inspect the **Verification Gate** card and **Verification Record** card in the right column.
- **Expected Result:**
  - Sticky header Action Bar:
    - Primary button: `Record Remediation` (Retry).
    - Secondary button: `Manual Verification`.
    - Disabled button: `Mark Resolved` (`🔒 Clean verification required`).
  - Verification Record card displays prior failed verification attempt by Ahmad Budi (`CleanPassed = false`).
  - Rose alert banner is displayed.

---

## TC-04-05 — Supersession of Failed Verification on Remediation Retry (`GAP-005`)

- **Objective:** Verify that recording a new remediation attempt supersedes the prior failed verification record (`IsSuperseded = 1`), clearing the failure banner and requiring new verification.
- **Preconditions:** On `http://localhost:5173/issues/d0555555-5555-5555-5555-555555555501` (`EMR-DB-REPL02`).
- **Steps:**
  1. In the Action Bar, click **Record Remediation** (Retry).
  2. In modal `M-02`:
     - Select Category: `ConfigurationAdjustment`.
     - Enter Description: `"Restarted secondary SQL replication distributor service and cleared queue."`.
  3. Click **Save Remediation**.
- **Expected Result:**
  - Remediation is recorded; Remediation History card shows 2 entries.
  - The previous failed verification is superseded (`IsSuperseded = 1`).
  - The rose failure banner disappears from the Verification Record card.
  - Action Bar promotes `Manual Verification` to primary button.
  - Step 3 in Verification Gate is reset to unverified.

---

## TC-04-06 — Record Clean Manual Verification (`CleanPassed`)

- **Objective:** Verify that recording a successful manual verification (`CleanPassed`) marks Step 3 as passed and unlocks the Verification Gate.
- **Preconditions:** Completed TC-04-05; on `http://localhost:5173/issues/d0555555-5555-5555-5555-555555555501`.
- **Steps:**
  1. In the Action Bar, click **Manual Verification**.
  2. In modal `M-03`, select the first card: **Anomaly Resolved / Clean Query** (`CleanPassed`).
  3. Observe emerald ring highlight on card.
  4. In Evidence Notes, enter: `"Replication queue length is 0 and sync lag is 12ms. All tables in sync."`.
  5. Click **Record Verification**.
- **Expected Result:**
  - Modal closes with success toast.
  - Verification Record card displays emerald chip: `"Clean Passed"`.
  - Evidence notes and Ahmad Budi attribution are displayed.
  - In **Verification Gate** card:
    - All 3 checklist items are checked.
    - Card banner turns solid emerald: `"Verification Gate Unlocked"`.
  - Sticky Action Bar:
    - **Mark Resolved** button is now unlocked, styled with solid emerald background (`bg-emerald-600 hover:bg-emerald-500 text-white`).

---

# 9. Test Suite: Mandatory Verification Gate & Issue Resolution (`BR-PRIMA-004`)

## TC-05-01 — Verification Gate Direct API Lockout Enforcement (HTTP 409 Conflict)

- **Objective:** Verify that attempting to resolve an issue via direct API request without an un-superseded clean verification record returns HTTP `409 Conflict` (RFC 7807 ProblemDetails).
- **Preconditions:** Backend running; obtain officer JWT token.
- **Steps:**
  1. Open terminal and execute direct resolve command against an unverified issue (e.g. `LIS-QUEUE-4412` which is awaiting verification):
     ```powershell
     $token = (Invoke-RestMethod -Uri "http://localhost:5005/api/auth/login" -Method Post -ContentType "application/json" -Body '{"username":"ahmad.budi","password":"Hospital@2026!"}').token
     try {
       Invoke-RestMethod -Uri "http://localhost:5005/api/issues/d0444444-4444-4444-4444-444444444401/resolve" -Method Post -Headers @{"Authorization"="Bearer $token"}
     } catch {
       $_.Exception.Response.StatusCode.value__
       (Get-Content -Raw $error[0].ErrorDetails.Message)
     }
     ```
- **Expected Result:**
  - Request fails with HTTP status `409 Conflict`.
  - ProblemDetails response contains: `"Cannot resolve issue. Mandatory verification gate requires a clean, passed verification record."`.
  - The issue status in database remains `Investigating`.

---

## TC-05-02 — Verification Gate Unlocked Fixture Verification (`SEP-202611-9982`)

- **Objective:** Verify that pre-seeded fixture `SEP-202611-9982` accurately loads with the Verification Gate fully unlocked and ready for one-click resolution.
- **Preconditions:** Authenticated as `siti.rahma`; navigate to `http://localhost:5173/issues/d0666666-6666-6666-6666-666666666601`.
- **Steps:**
  1. Observe the **Verification Gate** card in the right column.
  2. Observe the sticky Action Bar in the header.
- **Expected Result:**
  - **Verification Gate** card shows all 3 items checked (Investigation Started, Remediation Recorded, Verification Passed Clean).
  - Banner is emerald: `"Verification Gate Unlocked"`.
  - Sticky Action Bar:
    - Primary button is **Mark Resolved** with emerald styling (`bg-emerald-600`).
    - Tooltip indicates: `"Verification gate passed. Ready to resolve."`.

---

## TC-05-03 — Definitive Issue Resolution & Active Dashboard Removal

- **Objective:** Verify that clicking "Mark Resolved" transitions the issue to `Resolved`, removes it from the active dashboard feed (`S-01`), and increments the `Resolved Today` KPI counter.
- **Preconditions:** On `http://localhost:5173/issues/d0666666-6666-6666-6666-666666666601` (Gate unlocked).
- **Steps:**
  1. Click **Mark Resolved** in the Action Bar.
  2. Confirm resolution in any confirmation dialog if prompted.
  3. Observe immediate UI state changes on the workbench.
  4. Click the Back button to navigate to the Dashboard (`http://localhost:5173/`).
  5. Check the Summary Counters and the Active Issue Feed.
- **Expected Result:**
  - On Workbench:
    - Toast notification: `"Issue successfully resolved."`.
    - Status badge updates to `Resolved` (emerald accent).
    - Action Bar removes all mutating action buttons, rendering read-only state badge `"Resolved"`.
    - Verification Gate card shows emerald `"Issue Resolved"`.
    - Activity Log records `Resolved` by `Siti Rahma`.
  - On Dashboard:
    - `SEP-202611-9982` is NO LONGER present in the active issue feed.
    - The `Resolved Today` summary counter tile has incremented by 1.

---

## TC-05-04 — Read-Only Workbench State on Resolved Issues (`INV-2026-RESOLVED-001`)

- **Objective:** Verify that pre-seeded resolved issue `INV-2026-RESOLVED-001` renders in strict read-only mode, disabling all editing actions while presenting complete historical governance records.
- **Preconditions:** Authenticated as `ahmad.budi`; navigate to `http://localhost:5173/issues/d0111111-1111-1111-1111-111111111101`.
- **Steps:**
  1. Inspect the sticky Action Bar.
  2. Inspect the Note composer textarea.
  3. Inspect the right column cards (Verification, Remediation, Ownership).
- **Expected Result:**
  - Action Bar contains NO action buttons (`Take It`, `Assign`, `Remediation`, `Verification`, `Dismiss` are all absent); only a read-only `Resolved` badge is shown.
  - Note composer textarea is either hidden or read-only/disabled with notice: `"This issue is resolved and closed for edits."`.
  - Right column displays complete history: Remediation card with `DataCorrection` details, Verification Record card with `CleanPassed` details, and Ownership card with `ResolvedAt` timestamp.

---

# 10. Test Suite: Documented Issue Dismissal (`BR-PRIMA-006`, Modal M-01)

## TC-06-01 — Dismissal Rationale Minimum Length Enforcement (< 20 Chars)

- **Objective:** Verify that dismissing an issue strictly enforces a minimum 20-character rationale on both client and server, preventing premature submission.
- **Preconditions:** Authenticated as `ahmad.budi`; navigate to an active issue (e.g. `BILL-2026-OPEN-001` or `LIS-QUEUE-4412`).
- **Steps:**
  1. In the sticky Action Bar, click **Dismiss** button (secondary danger button).
  2. Observe that modal `DismissIssueModal.vue` (`M-01`) opens.
  3. Select Reason: `FalsePositive`.
  4. In Justification Rationale, type: `"Testing short"`.
  5. Observe the live character counter.
  6. Attempt to click the **Dismiss Issue** submit button.
- **Expected Result:**
  - Live character counter displays: `13 / 20 characters minimum` with rose color styling.
  - The submit button is disabled (`disabled` attribute / opacity reduced).
  - Typing additional characters past 20 (e.g. `"Testing short rationale that exceeds twenty chars."`) changes the counter styling to emerald and enables the submit button.

---

## TC-06-02 — Successful Issue Dismissal with Structured Reason

- **Objective:** Verify that submitting a valid dismissal closes the issue, records dismissal details, appends activity audit log, and removes the issue from the active feed.
- **Preconditions:** Ingest a fresh open finding via telemetry simulator:
  ```powershell
  node src/tools/telemetry-simulator/simulate.mjs new-finding
  ```
  Navigate to the newly created issue workbench.
- **Steps:**
  1. Click **Dismiss** in the Action Bar.
  2. In modal `M-01`:
     - Select Reason: `AcceptedOperationalException`.
     - Enter Justification Rationale: `"Known maintenance downtime approved by EDP Lead for system migration."` (74 chars).
  3. Click **Dismiss Issue**.
- **Expected Result:**
  - Modal closes with success toast: `"Issue dismissed."`.
  - Status badge updates to `Dismissed` (slate/zinc accent).
  - Right column renders **Dismissal Record** card displaying:
    - Reason: `AcceptedOperationalException`
    - Dismissed By: `Ahmad Budi`
    - Justification Rationale callout quote.
  - Activity log records `Dismissed`.
  - Navigating back to Dashboard confirms the issue is removed from the active feed.

---

## TC-06-03 — Dismissal Record Display on Pre-seeded Fixture (`ADM-2026-DISMISSED-001`)

- **Objective:** Verify that pre-seeded dismissed issue `ADM-2026-DISMISSED-001` cleanly displays the dismissal card and enforces read-only state.
- **Preconditions:** Authenticated as `ahmad.budi`; navigate to `http://localhost:5173/issues/d0222222-2222-2222-2222-222222222202`.
- **Steps:**
  1. Inspect the right column and Action Bar.
- **Expected Result:**
  - Right column renders **Dismissal Record** card with `FalsePositive` chip and rationale quote.
  - Action Bar renders read-only `Dismissed` badge with zero mutating buttons.
  - In Ownership card, `Dismissed At` timestamp is visible.

---

## TC-06-04 — Direct Closed Issue Dismissal Rejection (HTTP 409 Conflict)

- **Objective:** Verify that calling the dismissal endpoint on an already closed issue (`Resolved` or `Dismissed`) returns HTTP `409 Conflict`.
- **Preconditions:** Backend running; obtain officer JWT token.
- **Steps:**
  1. Execute HTTP POST to dismiss `INV-2026-RESOLVED-001` via PowerShell:
     ```powershell
     $token = (Invoke-RestMethod -Uri "http://localhost:5005/api/auth/login" -Method Post -ContentType "application/json" -Body '{"username":"ahmad.budi","password":"Hospital@2026!"}').token
     try {
       $body = @{ dismissalReason = "FalsePositive"; justificationRationale = "Attempting to dismiss already resolved issue." } | ConvertTo-Json
       Invoke-RestMethod -Uri "http://localhost:5005/api/issues/d0111111-1111-1111-1111-111111111101/dismiss" -Method Post -Headers @{"Authorization"="Bearer $token"} -ContentType "application/json" -Body $body
     } catch {
       $_.Exception.Response.StatusCode.value__
       (Get-Content -Raw $error[0].ErrorDetails.Message)
     }
     ```
- **Expected Result:**
  - Request fails with HTTP status `409 Conflict`.
  - ProblemDetails response indicates that closed issues cannot be dismissed.

---

# 11. Test Suite: Active Recurrence Verification Invalidation (`GAP-005`, `OQ-005`)

## TC-07-01 — Recurrence on Investigating Issue Invalidates Clean Verification

- **Objective:** Verify that when telemetry ingestion detects a recurrence matching an active `Investigating` issue that has a clean verification record, the verification is automatically superseded (`IsSuperseded = 1`) and the Verification Gate re-locks.
- **Preconditions:**
  - Pre-seeded issue `SEP-202611-9982` (`d0666666-6666-6666-6666-666666666601`) is in `Investigating` status with `CleanPassed = true` verification.
  - Associated detector is `Patient Admission Discharge Sync` (ID: `e2c04068-8314-469b-8326-880bebbff71d`).
- **Steps:**
  1. Open browser to `http://localhost:5173/issues/d0666666-6666-6666-6666-666666666601`.
  2. Confirm that the **Verification Gate** card is currently unlocked (emerald).
  3. In a terminal, execute an ingestion request submitting telemetry for `SEP-202611-9982`:
     ```powershell
     $body = @{
       detectorId = "e2c04068-8314-469b-8326-880bebbff71d"
       executionTimestampUtc = (Get-Date).ToUniversalTime().ToString("o")
       triggerSource = "Scheduled"
       outcome = "AnomalyDetected"
       durationMs = 250
       findings = @(
         @{
           targetEntityReference = "SEP-202611-9982"
           title = "Patient Admission BPJS SEP Sync Anomaly"
           baseSeverity = "Medium"
           module = "Admission"
           sourceTable = "dbo.PatientAdmissions"
           recordKeys = "AdmissionId: 44091, SEPNo: 0115R0011124V009982"
           traceString = "BPJS VClaim WebService returned status 201: SEP bridging record unconfirmed."
         }
       )
     } | ConvertTo-Json -Depth 5
     Invoke-RestMethod -Uri "http://localhost:5005/api/ingestion/telemetry" -Method Post -Headers @{"X-Detector-API-Key"="prima-dev-key-alpha-99"} -ContentType "application/json" -Body $body
     ```
  4. Return to the browser and wait up to 10 seconds for the next polling tick (or press manual refresh).
- **Expected Result:**
  - Occurrence count increments (e.g. from `#1` to `#2`).
  - In **Verification Gate** card:
    - Step 3 ("Verification Passed Clean") is re-locked / unchecked.
    - Banner reverts from emerald to amber/rose.
  - In sticky Action Bar:
    - **Mark Resolved** button is disabled again (`🔒 Clean verification required`).
    - Primary button updates to **Manual Verification** (or **Record Remediation**).
  - Attempting to resolve directly via API fails with `409 Conflict`.

---

## TC-07-02 — Verification Invalidation Audit Trail in Activity Log

- **Objective:** Verify that active recurrence invalidation records an atomic audit log entry in `dbo.IssueActivityLogs`.
- **Preconditions:** Completed TC-07-01 on `SEP-202611-9982`.
- **Steps:**
  1. Scroll down to the **Activity Log** accordion in the right column.
  2. Expand the accordion.
  3. Look for the newest entries.
- **Expected Result:**
  - An activity log entry is visible with:
    - Activity Type: `VerificationInvalidated` (or `RecurrenceDetected`)
    - Actor Name: `System (Ingestion Pipeline)`
    - Summary indicates that a new occurrence arrived while investigating, invalidating prior verifications.
    - Timestamp matches the recurrence ingestion time.

---

# 12. Test Suite: Complete End-to-End Manual Issue Resolution Loop Walkthrough

## TC-08-01 — End-to-End Manual Issue Resolution Loop Walkthrough

- **Objective:** Perform a comprehensive end-to-end walkthrough of the complete manual resolution loop from telemetry discovery through triage, investigation, remediation, verification, and resolution, verifying all observable UI states and database updates.
- **Preconditions:** System services running; authenticated as `ahmad.budi`.
- **Steps:**
  1. **Telemetry Ingestion:** Open a terminal and run:
     ```powershell
     node src/tools/telemetry-simulator/simulate.mjs new-finding
     ```
     Note the generated entity reference in the console output (e.g. `SIM-ENT-xxx`).
  2. **Dashboard Feed Discovery:**
     - Open Dashboard (`http://localhost:5173/`).
     - Wait for the next 10-second polling cycle.
     - Observe the newly created issue appear in the Active Issue Feed with status `Open`.
  3. **Open Workbench:**
     - Click the table row to navigate to `/issues/:id`.
     - Verify `S-02` loads with sticky header, `Open` badge, and 2x2 metadata well.
  4. **Triage ("Take It"):**
     - Click **Take It** in the Action Bar.
     - Verify status transitions to `Investigating` and assignee becomes `"Ahmad Budi"`.
  5. **Add Investigation Note:**
     - In the Investigation Notes stream composer, type: `"Investigating root cause in database transaction logs."`.
     - Press `Ctrl+Enter`.
     - Verify note appears with Ahmad Budi author chip.
  6. **Record Remediation Action (`M-02`):**
     - In Action Bar, click **Record Remediation**.
     - Select Category: `ConfigurationAdjustment`.
     - Description: `"Adjusted connection pool timeout in hospital application config."`.
     - Click **Save Remediation**.
     - Verify Step 2 of Verification Gate is checked.
     - Verify Action Bar promotes **Manual Verification** to primary button.
  7. **Record Failed Verification (`M-03`):**
     - Click **Manual Verification**.
     - Select **Anomaly Still Present / Failed**.
     - Evidence Notes: `"Timeout still observed during peak load test."`.
     - Click **Record Verification**.
     - Verify rose warning banner appears in Verification Record card.
     - Verify Action Bar primary button becomes **Record Remediation (Retry)**.
  8. **Remediation Retry (`M-02`):**
     - Click **Record Remediation** (Retry).
     - Select Category: `ServiceRestart`.
     - Description: `"Restarted connection broker service to clear hanging sockets."`.
     - Click **Save Remediation**.
     - Verify rose warning banner is cleared (prior failure superseded).
  9. **Record Clean Verification (`M-03`):**
     - Click **Manual Verification**.
     - Select **Anomaly Resolved / Clean Query** (`CleanPassed`).
     - Evidence Notes: `"Zero timeout errors observed over 10-minute probe. Clean connection established."`.
     - Click **Record Verification**.
     - Verify Verification Gate card turns solid emerald ("Verification Gate Unlocked").
     - Verify **Mark Resolved** button in Action Bar becomes primary emerald button.
  10. **Resolve Issue:**
      - Click **Mark Resolved**.
      - Verify workbench updates to read-only status with emerald `Resolved` badge.
  11. **Verify Dashboard Removal:**
      - Click header Back button to return to Dashboard (`http://localhost:5173/`).
      - Confirm the issue is no longer present in the Active Issue Feed.
      - Confirm the `Resolved Today` counter has incremented.
- **Expected Result:**
  - The complete issue resolution lifecycle progresses smoothly through all stages without manual database intervention, console exceptions, or UI state regressions.
  - Every transition is accurately recorded in `dbo.IssueActivityLogs`.

---

# 13. Defect Recording Guide (For Human Testers)

When executing this test package, record all execution observations, verdicts, and evidence in a companion `IS002-PRIMA-STAGE-2-TEST-EXECUTION.md` artifact.

If an observable behavior deviates from the **Expected Result**:
1. Mark the test case verdict as **`FAIL`**.
2. Document the **Actual Result** clearly and concisely, contrasting it with the Expected Result.
3. Capture verifiable **Evidence**:
   - Browser developer console error log screenshot / text.
   - Network tab request payload, response status code, and RFC 7807 problem details JSON.
   - Database query state from SQL Server Management Studio or Azure Data Studio.
4. Assign a Defect Severity according to the following standard:
   - **BLOCKER:** System crash, unhandled 500 error, database deadlock, inability to sign in or navigate to the workbench, or preventing testing from proceeding.
   - **MAJOR:** Core domain rule or invariant violation (e.g. Verification Gate bypassed, issue resolved without clean verification, dismissal allowed with < 20 characters rationale, recurrence invalidation failed, audit log missing).
   - **MINOR:** Visual misalignment, styling inconsistency, minor typo, or non-critical formatting issue that does not violate domain rules.
5. In accordance with SDLC governance, hand off any `FAIL` defect record directly to Issue Intake (`ica-issuer`) to produce a formal `BUG` ISSUE artifact. Testers do not fix code or bypass quality gates.

---

# 14. Next Workflow Stage

- **Next Stage:** Testing Assistance & Execution
- **Owning Role:** Human Tester (assisted by `ica-tester`)
- **Action:** Execute the test cases in Section 4–12 sequentially against the running application and record verdicts and evidence in `docs/issues/IS002-PRIMA-STAGE-2-TEST-EXECUTION.md`.
