---
Title: PrIMA Stage 1 — Platform Foundation & Core Telemetry Ingestion Test Package
Code: PRIMA-STAGE-1
Artifact: TEST-PACKAGE
Version: 1.0
LastUpdated: 2026-11-21
Status: READY-FOR-TESTING
---

# 1. Overview & Purpose

This test package provides human-executable verification procedures for **PrIMA Stage 1: Platform Foundation & Core Telemetry Ingestion** (`Walking Skeleton & Live Ingestion Pipeline` milestone).

The test suite validates the integrated system as a whole, covering:
1. **Officer Authentication & Session Management (`S-00`):** Secure officer sign-in, session persistence, role display, and navigation guards.
2. **Live Active Issue Dashboard (`S-01`):** Operational KPI summary counters, strict issue feed prioritization, 10-second non-disruptive polling, multi-dimension filters, and Stage 1 navigation boundaries.
3. **Telemetry Ingestion Pipeline (`POST /api/ingestion/telemetry`):** API key security, detector guardrails, and liveness tracking.
4. **Core Domain Rules:** Deterministic deduplication (`BR-PRIMA-001`), automatic recurrence reopening with non-destructive governance invalidation (`BR-PRIMA-002`), and immutable occurrence audit trail (`BR-PRIMA-009`).
5. **Tooling & Test Automation:** Automated xUnit test suite (43 test cases) and the zero-dependency CLI telemetry simulator.

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
When `SeedDemoData == true` (default in Development mode), the following fixtures are automatically prepared:
- **Active Detectors:**
  - `Billing Journal Balance Checker` (Scope: `DataIntegrity`)
  - `Patient Admission Discharge Sync` (Scope: `ProcessIntegrity`)
  - `Prescription Stock Deductor` (Scope: `DataSync`)
  - `EMR Database Replication Monitor` (Scope: `Infrastructure`)
- **Paused Detector:**
  - `Laboratory Order Sync Prober` (Scope: `DataSync`, Status: `Paused`)
- **Pre-seeded Closed Issues (for Recurrence Verification):**
  - 1 `Resolved` issue with `CleanPassed = 1` verification record.
  - 1 `Dismissed` issue with operational justification record.

### 2.4 Telemetry Simulator CLI
Located at `src/tools/telemetry-simulator/simulate.mjs`:
- Usage: `node src/tools/telemetry-simulator/simulate.mjs [scenario] [options]`
- Options: `--url <endpoint>` (defaults to `http://localhost:5005/api/ingestion/telemetry`), `--key <key>` (defaults to `prima-dev-key-alpha-99`).

---

# 3. Test Execution Sequence

Testers should follow this recommended execution order:

```text
Section 4: Automated Quality Gate (TC-001, TC-002)
   ↓
Section 5: Officer Authentication & Session (TC-01-01 to TC-01-06)
   ↓
Section 6: Telemetry Ingestion & Guardrails (TC-02-01 to TC-02-06)
   ↓
Section 7: Business Rules & Recurrence (TC-03-01 to TC-03-05)
   ↓
Section 8: Live Dashboard & Workbench UI (TC-04-01 to TC-04-06)
```

---

# 4. Automated Quality Gate

## TC-001 — Backend Automated Regression & Domain Test Suite

- **Objective:** Verify that all 43 automated backend tests pass, verifying domain invariants, security filters, deduplication, auto-reopening, and dashboard feed query semantics.
- **Preconditions:** Host has .NET 8 SDK installed; local SQL Server running.
- **Steps:**
  1. Open terminal in the repository root directory.
  2. Execute:
     ```powershell
     dotnet test src/backend/PrIMA.slnx
     ```
- **Expected Result:**
  - All 43 test cases execute and pass cleanly (`Failed: 0`, `Passed: 43`, `Skipped: 0`).
  - Output displays: `Passed! - Failed: 0, Passed: 43, Skipped: 0`.

---

## TC-002 — Frontend Production Build & TypeScript Typecheck

- **Objective:** Verify that the frontend application compiles cleanly into production assets without TypeScript compiler errors.
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

# 5. Test Suite: Officer Authentication & Session Management (Screen S-00)

## TC-01-01 — Successful Officer Sign-In & Dashboard Redirection

- **Objective:** Verify that an active EDP officer can sign in with valid credentials, obtain a 12-hour JWT session, and be redirected to the active dashboard.
- **Preconditions:** Frontend running at `http://localhost:5173`; backend running at `http://localhost:5005`.
- **Test Data:**
  - Username: `ahmad.budi`
  - Password: `Hospital@2026!`
- **Steps:**
  1. Open browser and navigate to `http://localhost:5173/login`.
  2. Observe login card layout: centered workbench card, dark-slate background, "PrIMA — Proactive Inspection Monitoring App", and role pill "EDP / IT Support Sign In".
  3. Enter Username: `ahmad.budi`.
  4. Enter Password: `Hospital@2026!`.
  5. Click **Sign In** button.
- **Expected Result:**
  - Submit button briefly shows loading spinner.
  - User is immediately redirected to `http://localhost:5173/`.
  - Top header displays:
    - PrIMA system title.
    - Live WIB clock updating in real time.
    - Logged-in user badge displaying: "Ahmad Budi" with role pill "EdpOfficer".
    - Logout button.

---

## TC-01-02 — Invalid Credentials Error Handling

- **Objective:** Verify that the login form surfaces clear inline feedback when invalid credentials are provided without disclosing whether the username or password was incorrect.
- **Preconditions:** User is on the login page (`http://localhost:5173/login`).
- **Test Data:**
  - Username: `ahmad.budi`
  - Password: `WrongPassword123!`
- **Steps:**
  1. Navigate to `http://localhost:5173/login`.
  2. Enter Username: `ahmad.budi`.
  3. Enter Password: `WrongPassword123!`.
  4. Click **Sign In**.
- **Expected Result:**
  - Login fails with HTTP 401 Unauthorized.
  - An inline red alert banner is displayed above the inputs: `"Invalid username or password."`.
  - User remains on the login page; credentials are not cleared unexpectedly.

---

## TC-01-03 — Client-Side Form Validation for Empty Fields

- **Objective:** Verify that client-side form validation (VeeValidate + Zod) enforces non-empty fields before submission.
- **Preconditions:** User is on `http://localhost:5173/login`.
- **Steps:**
  1. Leave Username and Password inputs empty.
  2. Click **Sign In**.
- **Expected Result:**
  - No network request is dispatched.
  - Red inline validation messages appear below the inputs:
    - `"Username is required"`
    - `"Password is required"`
  - Submit button remains enabled for retry once values are entered.

---

## TC-01-04 — Session Persistence Across Page Reload (F5)

- **Objective:** Verify that the authenticated session survives full page reload (F5) via `localStorage` token storage.
- **Preconditions:** Logged in as `ahmad.budi` on `http://localhost:5173/`.
- **Steps:**
  1. Ensure you are on the Dashboard (`http://localhost:5173/`).
  2. Press **F5** (or browser Reload button) to trigger a hard page reload.
- **Expected Result:**
  - The application restores the session immediately without redirecting to `/login`.
  - The top navigation bar continues to display "Ahmad Budi" (`EdpOfficer`).
  - Active issues and summary counters load seamlessly.

---

## TC-01-05 — Protected Navigation Guard & Open Redirect Prevention

- **Objective:** Verify that unauthenticated access to private routes is blocked, and post-login redirection prevents open redirect attacks.
- **Preconditions:** Clear browser `localStorage` or open an Incognito browser window.
- **Steps:**
  1. In an unauthenticated window, navigate to `http://localhost:5173/`.
  2. Observe automatic redirection to `http://localhost:5173/login?redirect=%2F`.
  3. Now manually change the URL in the address bar to:
     `http://localhost:5173/login?redirect=https%3A%2F%2Fmalicious-site.com`
  4. Sign in with `siti.rahma` / `Hospital@2026!`.
- **Expected Result:**
  - Step 2: Unauthenticated user is immediately blocked from the dashboard and routed to `/login`.
  - Step 4: After sign-in, the open redirect is rejected; the application safely redirects the user to the internal dashboard `/` rather than the external site.

---

## TC-01-06 — Officer Sign-Out

- **Objective:** Verify that clicking Sign Out clears the stored token and terminates the session.
- **Preconditions:** Authenticated as an officer on `http://localhost:5173/`.
- **Steps:**
  1. Click the **Logout** button in the top right header.
- **Expected Result:**
  - `localStorage` items (`prima_token`, `prima_officer`) are removed.
  - User is immediately redirected to `http://localhost:5173/login`.
  - Pressing browser **Back** button does not allow re-entering the dashboard without signing in.

---

# 6. Test Suite: Telemetry Ingestion Pipeline & Guardrails

## TC-02-01 — Ingestion API Key Authentication (`X-Detector-API-Key`)

- **Objective:** Verify that the ingestion endpoint requires a valid `X-Detector-API-Key` header and rejects unauthorized callers.
- **Preconditions:** Backend running on `http://localhost:5005`.
- **Steps:**
  1. Open terminal in the repository root.
  2. Execute telemetry simulator with an invalid API key:
     ```powershell
     node src/tools/telemetry-simulator/simulate.mjs passed --key invalid-test-key-000
     ```
  3. Execute telemetry simulator with the valid configured key (`prima-dev-key-alpha-99`):
     ```powershell
     node src/tools/telemetry-simulator/simulate.mjs passed --key prima-dev-key-alpha-99
     ```
- **Expected Result:**
  - Step 2: Request fails with HTTP status `401 Unauthorized` and RFC 7807 problem details message: `"Missing or invalid X-Detector-API-Key header."`.
  - Step 3: Request succeeds with HTTP status `200 OK`.

---

## TC-02-02 — Clean Inspection Run Ingestion (`outcome: Passed`)

- **Objective:** Verify that a detector sweep with 0 findings (`Passed`) is recorded as an inspection run and updates the detector's healthy heartbeat without creating any issue.
- **Preconditions:** Backend running; valid API key available.
- **Steps:**
  1. Execute:
     ```powershell
     node src/tools/telemetry-simulator/simulate.mjs passed
     ```
- **Expected Result:**
  - Response status is `200 OK`.
  - Response JSON contains:
    - `"outcome": "Passed"`
    - `"processedFindingsCount": 0`
    - `"inspectionRunId": "<guid>"`
  - The detector's `LastRunAtUtc` and `LastHealthyHeartbeatUtc` are updated in the database.
  - No new issue is added to `dbo.Issues`.

---

## TC-02-03 — Guardrail: Paused Detector Rejection (HTTP 409 Conflict)

- **Objective:** Verify that telemetry submission for a detector whose administrative status is `Paused` is rejected with `409 Conflict`.
- **Preconditions:** Detector `Laboratory Order Sync Prober` is seeded with `AdminStatus = Paused`.
- **Steps:**
  1. Execute simulator with the paused detector scenario:
     ```powershell
     node src/tools/telemetry-simulator/simulate.mjs paused-detector
     ```
- **Expected Result:**
  - Response status is `409 Conflict`.
  - RFC 7807 ProblemDetails returned: `"Detector is not active. Current status: Paused"`.
  - No inspection run or issue is recorded.

---

## TC-02-04 — Guardrail: Unknown Detector ID (HTTP 404 Not Found)

- **Objective:** Verify that submitting telemetry with an unregistered `detectorId` returns `404 Not Found`.
- **Preconditions:** Backend running.
- **Steps:**
  1. Execute HTTP POST to `/api/ingestion/telemetry` using PowerShell with a random GUID:
     ```powershell
     $body = @{
       detectorId = "00000000-0000-0000-0000-000000000999"
       executionTimestampUtc = (Get-Date).ToUniversalTime().ToString("o")
       triggerSource = "Scheduled"
       outcome = "Passed"
       durationMs = 120
     } | ConvertTo-Json
     Invoke-RestMethod -Uri "http://localhost:5005/api/ingestion/telemetry" -Method Post -Headers @{"X-Detector-API-Key"="prima-dev-key-alpha-99"} -ContentType "application/json" -Body $body
     ```
- **Expected Result:**
  - Request fails with HTTP status `404 Not Found`.
  - RFC 7807 ProblemDetails returned: `"Detector with ID '00000000-0000-0000-0000-000000000999' was not found."`.

---

## TC-02-05 — Guardrail: Unsupported `ExecutionFailed` Outcome (HTTP 422)

- **Objective:** Verify that submitting telemetry with `outcome = "ExecutionFailed"` in Stage 1 is rejected with `422 Unprocessable Entity` (deferred to Stage 3).
- **Preconditions:** Backend running.
- **Steps:**
  1. Submit telemetry with `outcome = "ExecutionFailed"`:
     ```powershell
     $body = @{
       detectorId = "11111111-1111-1111-1111-111111111111"
       executionTimestampUtc = (Get-Date).ToUniversalTime().ToString("o")
       triggerSource = "Scheduled"
       outcome = "ExecutionFailed"
       durationMs = 50
       failureReason = "Timeout communicating with target database"
     } | ConvertTo-Json
     Invoke-RestMethod -Uri "http://localhost:5005/api/ingestion/telemetry" -Method Post -Headers @{"X-Detector-API-Key"="prima-dev-key-alpha-99"} -ContentType "application/json" -Body $body
     ```
- **Expected Result:**
  - Request fails with HTTP status `422 Unprocessable Entity`.
  - Validation error message: `"ExecutionFailed outcome is supported in Stage 3 with infrastructure anomaly synthesis."`.

---

## TC-02-06 — Guardrail: Intra-Batch Duplicate Target Reference Rejection (HTTP 422)

- **Objective:** Verify that a single batch containing duplicate target entity references is rejected with `422 Unprocessable Entity` to preserve batch determinism.
- **Preconditions:** Backend running.
- **Steps:**
  1. Submit telemetry containing two findings referencing the exact same target entity reference:
     ```powershell
     $body = @{
       detectorId = "11111111-1111-1111-1111-111111111111"
       executionTimestampUtc = (Get-Date).ToUniversalTime().ToString("o")
       triggerSource = "Scheduled"
       outcome = "FindingDiscovered"
       durationMs = 250
       findings = @(
         @{
           targetEntityReference = "BILL-TX-DUPLICATE-99"
           title = "First occurrence in batch"
           baseSeverity = "High"
           evidence = @{ module = "Billing"; trace = "Stack trace 1" }
         },
         @{
           targetEntityReference = " bill-tx-duplicate-99 "
           title = "Second occurrence in batch (same entity, different casing/whitespace)"
           baseSeverity = "High"
           evidence = @{ module = "Billing"; trace = "Stack trace 2" }
         }
       )
     } | ConvertTo-Json -Depth 5
     Invoke-RestMethod -Uri "http://localhost:5005/api/ingestion/telemetry" -Method Post -Headers @{"X-Detector-API-Key"="prima-dev-key-alpha-99"} -ContentType "application/json" -Body $body
     ```
- **Expected Result:**
  - Request fails with HTTP status `422 Unprocessable Entity`.
  - ProblemDetails message indicates duplicate target entity reference in findings batch.

---

# 7. Test Suite: Business Rules & Recurrence Reopening

## TC-03-01 — New Anomaly Ingestion & Initial Issue Creation (`BR-PRIMA-001`)

- **Objective:** Verify that submitting a new finding with a novel target entity reference creates an `Open` issue with `OccurrenceNumber = 1` and `OccurrenceCount = 1`.
- **Preconditions:** Backend and frontend running; authenticated as an officer on `http://localhost:5173/`.
- **Steps:**
  1. Execute simulator scenario `new-finding`:
     ```powershell
     node src/tools/telemetry-simulator/simulate.mjs new-finding
     ```
  2. Observe console output and switch to browser dashboard (`http://localhost:5173/`).
- **Expected Result:**
  - Simulator outputs HTTP 200 OK:
    - `"action": "Created"`
    - `"occurrenceNumber": 1`
    - `"status": "Open"`
  - Within 10 seconds, the new issue appears at the top of the Active Issue Feed on the frontend dashboard without refreshing the page.
  - The issue displays badge `Open`, occurrence pill `x1`, and severity badge matching the submitted finding.

---

## TC-03-02 — Recurring Finding on Active Issue (`BR-PRIMA-001` & `BR-PRIMA-009`)

- **Objective:** Verify that submitting the exact same target entity reference again does NOT create a duplicate issue, but increments `OccurrenceCount` to 2 and appends a new immutable occurrence snapshot.
- **Preconditions:** TC-03-01 executed successfully.
- **Steps:**
  1. Execute simulator scenario `duplicate-finding`:
     ```powershell
     node src/tools/telemetry-simulator/simulate.mjs duplicate-finding
     ```
  2. Observe console output and check browser dashboard.
- **Expected Result:**
  - Step 1: Simulator outputs HTTP 200 OK:
    - `"action": "Appended"`
    - `"occurrenceNumber": 2`
    - `"occurrenceCount": 2`
    - `"status": "Open"`
  - Step 2: On the dashboard, no new row is added. The existing issue updates its occurrence badge to `x2`, and its recency timestamp updates to "Just now".

---

## TC-03-03 — Recurrence on Resolved Issue Reopening (`BR-PRIMA-002`)

- **Objective:** Verify that when a finding recurs for an issue previously marked `Resolved`, the platform automatically transitions it to `Reopened`, clears resolution timestamps, and marks prior governance records `IsSuperseded = 1` without data loss.
- **Preconditions:** A pre-seeded `Resolved` issue exists in the database (CorrelationKey contains `DEMO-JOURNAL-BAL-001`).
- **Steps:**
  1. Execute simulator scenario `recurrence-resolved`:
     ```powershell
     node src/tools/telemetry-simulator/simulate.mjs recurrence-resolved
     ```
  2. Observe console output and check browser dashboard.
- **Expected Result:**
  - Simulator outputs HTTP 200 OK:
    - `"action": "Reopened"`
    - `"status": "Reopened"`
    - `"occurrenceNumber": 2`
  - On the dashboard:
    - The reopened issue appears in the active feed with purple badge `Reopened`.
    - Occurrence badge displays `x2`.
    - The top Summary bar increments the `Reopened` counter tile.
    - An informational banner appears at the top of the feed: `"Operational Note: Reopened issues detected in active queue."`.

---

## TC-03-04 — Recurrence on Dismissed Issue Reopening (`BR-PRIMA-002`)

- **Objective:** Verify that when a finding recurs for an issue previously marked `Dismissed`, the platform automatically reopens it to `Reopened` and marks the prior dismissal record `IsSuperseded = 1`.
- **Preconditions:** A pre-seeded `Dismissed` issue exists in the database (CorrelationKey contains `DEMO-EMR-REPL-001`).
- **Steps:**
  1. Execute simulator scenario `recurrence-dismissed`:
     ```powershell
     node src/tools/telemetry-simulator/simulate.mjs recurrence-dismissed
     ```
  2. Observe console output and check browser dashboard.
- **Expected Result:**
  - Simulator outputs HTTP 200 OK:
    - `"action": "Reopened"`
    - `"status": "Reopened"`
  - The issue reappears in the active feed with `Reopened` status badge.
  - The Summary counter for `Reopened` reflects the updated count.

---

## TC-03-05 — Atomic Multi-Finding Batch Ingestion

- **Objective:** Verify that multiple distinct findings submitted within a single inspection run are processed atomically and appear together in the active queue.
- **Preconditions:** Backend running.
- **Steps:**
  1. Execute simulator scenario `batch-findings`:
     ```powershell
     node src/tools/telemetry-simulator/simulate.mjs batch-findings
     ```
- **Expected Result:**
  - Simulator outputs HTTP 200 OK:
    - `"processedFindingsCount": 3`
    - `"results"` array lists 3 distinct created findings.
  - All 3 findings appear in the active issue feed on the dashboard on the next polling interval.

---

# 8. Test Suite: Live Active Dashboard Workbench (Screen S-01)

## TC-04-01 — Summary KPI Counters & WIB Midnight Boundary

- **Objective:** Verify that the 5 summary KPI tiles display accurate counts and that `Resolved Today` is bounded strictly within Asia/Jakarta (WIB) midnight.
- **Preconditions:** Authenticated officer on `http://localhost:5173/`.
- **Steps:**
  1. Navigate to `http://localhost:5173/`.
  2. Observe the Summary Counters Bar at the top of the dashboard.
- **Expected Result:**
  - 5 KPI tiles are rendered:
    - `Critical` (Crimson accent)
    - `High` (Amber/Orange accent)
    - `Open` (Blue accent)
    - `Reopened` (Purple accent)
    - `Resolved Today` (Green accent)
  - Tile counts match the active items in the database.
  - `Resolved Today` reflects resolutions that occurred between 00:00:00 WIB and 23:59:59 WIB of the current date.

---

## TC-04-02 — Strict Issue Feed Prioritization Order

- **Objective:** Verify that the active issue feed strictly sorts issues by `SeverityRank DESC, LastOccurrenceAtUtc DESC` so that urgent anomalies always appear at the top.
- **Preconditions:** Active issues with varied severities (`High`, `Medium`, `Low`, `Critical`) exist in the database.
- **Steps:**
  1. Observe the rows in the Active Issue Feed table.
  2. Check the order of severity levels and recency dates.
- **Expected Result:**
  - All `Critical` issues appear first, sorted by newest recency.
  - Followed by all `High` issues, sorted by newest recency.
  - Followed by `Medium` issues, then `Low` issues.
  - No low-severity issue appears above a high-severity issue regardless of its timestamp.

---

## TC-04-03 — Non-Disruptive 10-Second Auto-Polling

- **Objective:** Verify that the dashboard refreshes automatically every 10 seconds without screen flicker, scroll jumps, or resetting active user selections.
- **Preconditions:** Dashboard open in browser; user scrolled partway down the issue feed.
- **Steps:**
  1. Scroll down the active issue feed table.
  2. Type a partial search term (e.g., `Sync`) in the search box.
  3. Wait 15–20 seconds to observe two polling cycles.
  4. In a terminal, run `node src/tools/telemetry-simulator/simulate.mjs new-finding`.
  5. Watch the browser screen.
- **Expected Result:**
  - A small spinning sync icon in the header indicates background fetching.
  - The table does NOT flash a full-page loading skeleton.
  - The scroll position does NOT jump to the top.
  - The text in the search input is NOT wiped or interrupted.
  - The newly simulated finding appears automatically in the feed on the next 10-second tick.

---

## TC-04-04 — Multi-Dimensional Server-Side Filtering & Search

- **Objective:** Verify that filtering by Status, Severity, Scope, and text search correctly narrows the active feed results.
- **Preconditions:** Multiple active issues exist across various scopes and statuses.
- **Steps:**
  1. **Status Filter:** Select `Reopened` in the Status dropdown. Verify only issues with purple `Reopened` badges appear.
  2. **Severity Filter:** Change Severity to `High`. Verify only High-severity issues appear.
  3. **Scope Filter:** Change Scope to `DataIntegrity`. Verify only DataIntegrity issues appear.
  4. **Text Search:** Type a keyword (e.g. `Billing` or a CorrelationKey snippet) into the search box. Verify results match within 300ms.
  5. **Reset:** Click the **Reset Filters** button.
- **Expected Result:**
  - Each filter updates the feed accurately based on backend query parameters.
  - Clicking **Reset Filters** restores all dropdowns to `All` and clears the search box, restoring the full active issue feed.

---

## TC-04-05 — Active-Only Enforcement (Closed Status Exclusion)

- **Objective:** Verify that the dashboard feed displays only active issues and that attempting to query closed statuses directly is rejected with HTTP 422.
- **Preconditions:** Backend running.
- **Steps:**
  1. Confirm that the frontend Status dropdown offers only: `All`, `Open`, `Acknowledged`, `Investigating`, `Reopened` (with no option for `Resolved` or `Dismissed`).
  2. Execute a direct HTTP request attempting to query closed issues:
     ```powershell
     $token = (Invoke-RestMethod -Uri "http://localhost:5005/api/auth/login" -Method Post -ContentType "application/json" -Body '{"username":"ahmad.budi","password":"Hospital@2026!"}').token
     Invoke-RestMethod -Uri "http://localhost:5005/api/issues/dashboard-feed?status=Resolved" -Method Get -Headers @{"Authorization"="Bearer $token"}
     ```
- **Expected Result:**
  - Step 1: The UI status dropdown contains zero closed status options.
  - Step 2: The direct API query fails with HTTP `422 Unprocessable Entity` and ProblemDetails: `"Dashboard feed displays active issues only. Closed issues are not accessible via this endpoint."`.

---

## TC-04-06 — Stage 1 Navigation Boundaries & Non-Navigating Controls

- **Objective:** Verify that Stage 1 boundary constraints are enforced in the UI so that unavailable future features do not present dead links or broken workflows.
- **Preconditions:** Authenticated as an officer on `http://localhost:5173/`.
- **Steps:**
  1. Look at the left sidebar navigation links:
     - Check `Dashboard`.
     - Check `Detectors`.
     - Check `History & Reports`.
  2. Hover mouse over `Detectors` and `History & Reports`.
  3. Look at the `Resolved Today` KPI tile in the Summary Bar.
  4. Click on an active issue row in the feed table.
- **Expected Result:**
  - `Dashboard` is active and highlighted, displaying an active count badge pill (`open + reopened`).
  - `Detectors` is visually dimmed/disabled (`opacity-50`, cursor not-allowed); hovering displays tooltip: `"Coming in Stage 3"`. Clicking does nothing.
  - `History & Reports` is visually dimmed/disabled; hovering displays tooltip: `"Coming in Stage 4"`. Clicking does nothing.
  - `Resolved Today` KPI tile is non-clickable (cursor default); hovering displays tooltip: `"History view available in Stage 4"`.
  - Clicking an issue feed row does NOT navigate away or open a broken detail workbench (maintaining Stage 1 boundary until `S-02` in Stage 2).

---

# 9. Defect Recording Guide (For Human Testers)

When executing this test package, record all results in a companion `IS001-PRIMA-STAGE-1-TEST-EXECUTION.md` artifact.

If an observable behavior deviates from the **Expected Result**:
1. Mark the test case status as **`FAIL`**.
2. Document the **Actual Result** clearly and concisely.
3. Capture visual or log **Evidence** (console error, network response payload, screenshot).
4. Assign defect severity according to the following criteria:
   - **BLOCKER:** System crash, unhandled 500 error, data loss, unable to authenticate, or blocked from continuing testing.
   - **MAJOR:** Core business rule violation (e.g., duplicate issue created instead of appended, closed issue not reopened, invalid security bypass).
   - **MINOR:** UI alignment flaw, styling inconsistency, minor typo, or non-critical formatting issue.
5. Hand off the FAIL record to Issue Creation to formally capture a BUG ISSUE.

---

# 10. Next Workflow Stage

- **Next Stage:** Testing Assistance & Execution
- **Owning Role:** Human Tester (assisted by `ica-tester`)
- **Action:** Execute test cases in Section 4–8 sequentially and record execution results in `IS001-PRIMA-STAGE-1-TEST-EXECUTION.md`.
