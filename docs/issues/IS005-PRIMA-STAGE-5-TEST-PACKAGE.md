---
Title: PrIMA Stage 5 — Detector Handshake Protocol, Pilot SIMRS Detector Suite & Hospital Production Rollout Test Package
Code: PRIMA-STAGE-5
Artifact: TEST-PACKAGE
Version: 1.0
LastUpdated: 2026-11-22
Status: READY-FOR-TESTING
---

# 1. Overview & Purpose

This test package provides formal, human-executable verification procedures for **PrIMA Stage 5: Detector Handshake Protocol, Pilot SIMRS Detector Suite & Hospital Production Rollout** (`Hospital On-Premise Production Go-Live & Automated Detector Enrollment` milestone).

It synthesizes the operational requirements and scenario scripts from `docs/operations/PRIMA-UAT-AND-HYPERCARE-PLAYBOOK.md`, the architectural specifications in `docs/issues/IS005-PRIMA-STAGE-5-ARCHITECTURE.md`, and the completed implementation slices from `docs/issues/IS005-PRIMA-STAGE-5-IMPLEMENTATION-PLAN.md`.

The test package validates the completed system end-to-end against a live running hospital staging environment, covering:
1. **Universal Incident Lifecycle & Operations (`S-01`, `S-02`, Use Cases `A-1` through `D-4`):** Issue triage, officer assignment, justified dismissal (>=20 chars rationale), occurrence timeline inspection, structured financial and non-financial telemetry evidence, remediation recording, and the mandatory Verification Gate (`D-2` automated webhook re-run vs. `D-3` manual verification).
2. **Zero-Drift Automated Detector Enrollment (`S-03`, `DEC-001`, `DEC-002`, `DEC-004`):** EDP officer issuance of 24h single-use enrollment tokens (`PRM-TOKEN-...`), Quick Enroll modal with copyable snippets, pending tokens strip with active revocation, anonymous manifest handshake endpoint (`POST /api/detectors/handshake`), and automatic per-detector hashed API key (`ApiKeyHash`) provisioning.
3. **Reference Detector SDK & Cadence Supervision (`PrIMA.Detectors`, `DEC-003`, `DEC-005`):** Standardized .NET 8 runner executing in continuous background loop and one-shot (`--run-once`) modes, local credential caching in `.prima-credentials.json`, embedded Kestrel webhook listener (`/webhooks/{detectorKey}`), and non-blocking cadence drift calculation (`±50%` interval divergence, `BR-PRIMA-010`).
4. **Pilot SIMRS Detector Suite across All 4 Scopes (`BR-PRIMA-007`, `DEC-006`, `DEC-007`):** 6 production probes validating realistic hospital anomalies against `simrs.*` sandbox tables:
   - *Journal Balance Detector* (`DataIntegrity`, Accounting, `simrs.acc_journal_entries`)
   - *Duplicate Receivable Detector* (`DataIntegrity`, Billing, `simrs.ar_receivables`)
   - *Unprocessed Billing Pipeline Detector* (`ProcessIntegrity`, Billing, `simrs.trx_patient_bills`)
   - *EMR Synchronization Detector* (`DataSync`, EMR, `simrs.emr_sync_logs`)
   - *SIMRS–HFIS Doctor Schedule Detector* (`DataSync`, Outpatient/BPJS, `simrs.hfis_doctor_schedules`)
   - *Remote Print Server Queue Detector* (`Infrastructure`, PrintSpooler, `simrs.infra_print_queues`)
5. **Non-Intrusive Safety Guardrails (`BR-PRIMA-007`, `DEC-008`):** Programmatic read-only enforcement (`ReadOnlySimrsConnectionFactory`) with `ApplicationIntent=ReadOnly`, `SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED`, `SET LOCK_TIMEOUT 2000`, 5-second command timeouts, static DML/DDL query rejection (`SimrsSqlSafetyGuard`), sub-500ms execution times, and zero blocking locks on hospital OLTP.
6. **Detector Portfolio & Operational Governance (`S-03`, `S-04`, `S-05`, `E-1` through `E-7`):** Status-sensitive action bars, pause/resume state toggling with silence alert suppression, on-demand manual probe dispatch, automated silence detection (`BR-PRIMA-010`), and SLA auto-escalation (`BR-PRIMA-004`).
7. **Inspection Run Log & Historical Reporting (`S-06`, `S-07`, `F-1`, `H-1`, `H-2`):** Run diagnostics, outcome filters, period-close reliability KPI grid, pure CSS proportion bars, and deep-link historical ledger auditing.
8. **On-Premise Production Deployment & Maintenance (`deploy/`, `DEC-009`):** Deep database health probe (`GET /api/health`), automated daily 90-day retention pruning of clean `Passed` runs (`BR-PRIMA-009`), IIS/Nginx reverse proxy configurations, Windows Service setup scripts, and hospital EDP operational runbooks (`PRIMA-EDP-REMEDIATION-SOP.md`, `PRIMA-UAT-AND-HYPERCARE-PLAYBOOK.md`).

---

# 2. Test Environment & Prerequisites

### 2.1 System Services Setup
Verify that the following components are running before starting test execution:
- **Database:** Microsoft SQL Server 2022 (Local or Staging instance).
  - DbUp Migration `005_Stage5HandshakeAndSimrsSandbox.sql` must have executed successfully.
  - Verification query:
    ```sql
    SELECT TABLE_SCHEMA, TABLE_NAME 
    FROM INFORMATION_SCHEMA.TABLES 
    WHERE TABLE_SCHEMA IN ('dbo', 'simrs')
    ORDER BY TABLE_SCHEMA, TABLE_NAME;
    ```
- **Backend Web API (`PrIMA.Api`):**
  - Run command: `dotnet run --project src/backend/PrIMA.Api/PrIMA.Api.csproj`
  - URL: `http://localhost:5005` (or HTTPS via staging reverse proxy).
  - Health probe: `GET http://localhost:5005/api/health` returns `200 OK` `{ "status": "Healthy", "database": "Connected" }`.
- **Frontend SPA (`PrIMA.Client`):**
  - Run command: `npm --prefix src/frontend run dev`
  - URL: `http://localhost:5173` (Vite dev server with `/api` proxy).
- **Detector Runner (`PrIMA.Detectors`):**
  - Available to execute via `dotnet run --project src/backend/PrIMA.Detectors/PrIMA.Detectors.csproj`.

### 2.2 Seeded EDP Officer Accounts
Ensure the standard EDP operational accounts are seeded:

| Username | Password | Full Name | Role | Active |
|---|---|---|---|---|
| `ahmad.budi` | `Hospital@2026!` | Ahmad Budi | `EdpOfficer` | Yes |
| `siti.rahma` | `Hospital@2026!` | Siti Rahma | `SystemAnalyst` | Yes |
| `edp.lead` | `Hospital@2026!` | Joko Santoso | `Lead` | Yes |

### 2.3 Staging Sandbox Test Fixtures
The migration seeds 6 pilot anomaly test fixtures in `simrs.*` tables alongside normal records:

| Sandbox Table | Seeded Anomaly Key | Expected Finding & Scope | Diagnostic Symptom |
|---|---|---|---|
| `simrs.acc_journal_entries` | `JRN-202611-ERR01` | `DataIntegrity` (Accounting) | Unbalanced journal: Debit Rp 15.000.000 vs. Credit Rp 14.500.000 (Variance Rp 500.000). |
| `simrs.ar_receivables` | `TRX-AR-202611-DUP01` | `DataIntegrity` (Billing/AR) | Duplicate receivable record created within 3 seconds for transaction `TRX-AR-202611-DUP01`. |
| `simrs.trx_patient_bills` | `BILL-202611-NOJRN01` | `ProcessIntegrity` (Billing) | Finalized cashier bill aged > 30 minutes with `journal_status = 'PENDING'`. |
| `simrs.emr_sync_logs` | `ENC-202611-FAIL01` | `DataSync` (EMR / SatuSehat) | Outpatient encounter sync failed with `HTTP 500` after 3 retries. |
| `simrs.hfis_doctor_schedules` | `SCH-OBG-002` | `DataSync` (Outpatient/BPJS) | BPJS HFIS schedule mismatch: Dr. Bambang active in SIMRS but inactive in HFIS mirror. |
| `simrs.infra_print_queues` | `PRN-KASIR-02` | `Infrastructure` (PrintSpooler) | Cashier print spooler offline with 8 pending print jobs aged > 15 minutes. |

---

# 3. Test Execution Sequence

The testing process should proceed through the following stages:

```text
Section 4: Automated Quality Gates (TC-001 to TC-004)
   ↓
Section 5: Dashboard & Issue Monitoring (TC-01-01 to TC-01-03) [Group A]
   ↓
Section 6: Issue Triage & Ownership (TC-02-01 to TC-02-03) [Group B]
   ↓
Section 7: Investigation & Telemetry Evidence (TC-03-01 to TC-03-03) [Group C]
   ↓
Section 8: Remediation & Verification Gate (TC-04-01 to TC-04-04) [Group D]
   ↓
Section 9: Detector Portfolio, Handshake Enrollment & Cadence Supervision (TC-05-01 to TC-05-07) [Group E]
   ↓
Section 10: Inspection Run History & Diagnostics (TC-06-01) [Group F]
   ↓
Section 11: Notifications & Escalation Awareness (TC-07-01 to TC-07-02) [Group G]
   ↓
Section 12: Historical Reliability Reporting & Period-Close Audit (TC-08-01 to TC-08-02) [Group H]
   ↓
Section 13: Pilot SIMRS Detector Suite & Non-Intrusive Safety (TC-09-01 to TC-09-03) [Group I]
   ↓
Section 14: Deployment Assets, Retention Pruning & Deep Health Check (TC-10-01 to TC-10-03) [Group J]
   ↓
Section 15: End-to-End Hospital Go-Live Staging Simulation Smoke Test (TC-11-01)
```

---

# 4. Automated Quality Gates

## TC-001 — Full Backend Test Suite Execution
- **Objective:** Verify that 100% of the backend solution tests execute cleanly, including unit tests, domain invariant checks, integration tests, and full-stack Stage 5 smoke tests.
- **Preconditions:** .NET 8 SDK installed; local SQL Server running.
- **Steps:**
  1. Open terminal in repository root.
  2. Execute:
     ```powershell
     dotnet test src/backend/PrIMA.slnx
     ```
- **Expected Result:**
  - Test runner executes all test projects (`PrIMA.Domain.Tests`, `PrIMA.Application.Tests`, `PrIMA.Infrastructure.Tests`, `PrIMA.Api.Tests`, `PrIMA.Detectors.Tests`).
  - Total test count: 447 tests executed.
  - Results: 447 passed, 0 failed, 0 skipped.
  - `Stage5FullStackQualityGateSmokeTests` passes with complete handshake, ingestion, cadence drift, webhook, health check, and retention pruning assertions.

---

## TC-002 — Frontend Static Compliance Audit (`audit:compliance`, `DEC-008`)
- **Objective:** Assert zero violations against `PRIMA-UI-UX-GUIDELINE.md §10` architectural constraints.
- **Preconditions:** Node.js v20+ or v22 LTS installed.
- **Steps:**
  1. Open terminal in repository root.
  2. Execute:
     ```powershell
     npm --prefix src/frontend run audit:compliance
     ```
- **Expected Result:**
  - Audit script completes with exit code `0`.
  - Violations reported:
    - Zero pagination props (`no-paginator`): 0 violations.
    - Zero external charting libraries (`no-external-charts`): 0 violations.
    - Zero native browser dialogs (`no-native-dialogs`): 0 violations.
    - Zero cross-slice feature imports (`no-cross-slice-imports`): 0 violations.

---

## TC-003 — Frontend Production Build & TypeScript Typecheck
- **Objective:** Verify that all frontend views (`S-01` through `S-07`), components, and generated contracts compile cleanly without TypeScript or bundler errors.
- **Preconditions:** Node.js v20+ or v22 LTS installed.
- **Steps:**
  1. Open terminal in repository root.
  2. Execute:
     ```powershell
     npm --prefix src/frontend run build
     ```
- **Expected Result:**
  - `vue-tsc -b` completes with 0 errors.
  - Vite bundles all production chunks into `src/frontend/dist/` successfully with 0 errors. Exit code `0`.

---

## TC-004 — OpenAPI Contract Synchronization & Client Types
- **Objective:** Verify that backend Swagger generation and frontend TypeScript contracts are synchronized without drift.
- **Preconditions:** .NET 8 SDK and Node.js installed.
- **Steps:**
  1. Run Swagger document test:
     ```powershell
     dotnet test src/backend/PrIMA.Api.Tests --filter FullyQualifiedName~SwaggerDocumentTests
     ```
  2. Verify TypeScript contract file `src/frontend/src/shared/types/api.generated.ts` is in sync with OpenAPI spec.
- **Expected Result:**
  - Swagger test passes.
  - Contract contains strongly typed interfaces for `EnrollmentTokenResponse`, `GenerateEnrollmentTokenRequest`, `DetectorHandshakeRequest`, `DetectorHandshakeResponse`, and updated `DetectorDto` (with `cadenceDriftWarning`, `version`, `lastActualIntervalSeconds`).

---

# 5. Group A: Dashboard & Issue Monitoring

## TC-01-01 — View Issue Dashboard (Use Case `A-1`)
- **Objective:** Verify operational health summary and virtualized issue feed on Screen `S-01`.
- **Preconditions:** Logged into PrIMA client as an EDP officer. Telemetry has been submitted by pilot detectors.
- **Steps:**
  1. Navigate to `/` or click **"Dashboard"** in sidebar navigation.
  2. Inspect the KPI summary strip at the top (Open, Investigating, Critical, High counts).
  3. Inspect the virtualized issue table.
- **Expected Result:**
  - Top summary cards show accurate counts.
  - Active issues are grouped and ordered by priority (Critical/High first, then by age).
  - Clean status indicators and severity badges are displayed.
  - Zero pagination controls exist; virtual scrolling handles the list smoothly without pagination buttons.

---

## TC-01-02 — Filter & Search Issues (Use Case `A-2`)
- **Objective:** Verify responsive multi-attribute filtering and instant search on `S-01`.
- **Preconditions:** Multiple issues exist across different severities, scopes, and modules.
- **Steps:**
  1. In the search input, type `"JRN"` or `"Billing"`.
  2. Filter by Severity: Select `"Critical"`.
  3. Filter by Scope: Select `"DataIntegrity"`.
  4. Clear all filters.
- **Expected Result:**
  - Issue feed updates immediately without full page reloads.
  - Search matches both issue titles and entity correlation keys.
  - Clearing filters restores the full active issue list.

---

## TC-01-03 — View Issue Detail (Use Case `A-3`)
- **Objective:** Verify seamless transition from `S-01` to `S-02` with full operational context.
- **Preconditions:** At least one active issue displayed on `S-01`.
- **Steps:**
  1. Click an issue row on `S-01` (e.g., *Unbalanced Journal Batch JRN-202611-ERR01*).
  2. Observe the page transition to `S-02 (Issue Detail)`.
- **Expected Result:**
  - Full operational context loads: Issue title, unique correlation key (`{detectorId}:{entity}`), severity badge, target entity reference, current status (`Open`), first/last occurrence timestamps, and structured telemetry evidence card.

---

# 6. Group B: Issue Triage & Ownership

## TC-02-01 — Acknowledge Issue (Use Case `B-1`)
- **Objective:** Acknowledge an open issue, halting escalation timers and recording officer attribution.
- **Preconditions:** Issue in `Open` status selected on `S-02`. Authenticated as `ahmad.budi`.
- **Steps:**
  1. On the action panel of `S-02`, click the **"Acknowledge"** button.
  2. Observe UI feedback.
- **Expected Result:**
  - Toast notification confirms acknowledgment.
  - Issue status transitions from `Open` to `Acknowledged`.
  - SLA escalation timer stops.
  - Activity log records the acknowledgment event attributed to `ahmad.budi`.

---

## TC-02-02 — Assign Issue to Officer (Use Case `B-2`)
- **Objective:** Assign an acknowledged issue to an EDP officer to begin investigation.
- **Preconditions:** Issue is in `Acknowledged` (or `Open`) status on `S-02`.
- **Steps:**
  1. Click **"Assign to Me"** (or select officer from dropdown).
  2. Confirm assignment in dialog.
- **Expected Result:**
  - Issue status transitions to `Investigating`.
  - Assigned officer displays the logged-in officer name.
  - Activity log appends assignment record.

---

## TC-02-03 — Dismiss Issue with Justification (Use Case `B-3`, Modal `M-01`)
- **Objective:** Enforce `BR-PRIMA-006` mandatory justification (>= 20 characters) when dismissing false positives or planned maintenance issues.
- **Preconditions:** Issue in `Open` or `Investigating` status on `S-02`.
- **Steps:**
  1. Click **"Dismiss Issue"** button. Modal `M-01` opens.
  2. Select Reason: `"PlannedMaintenance"`.
  3. Enter short rationale (< 20 characters): `"test dismiss"`.
  4. Attempt to click **"Confirm Dismissal"**.
  5. Update rationale with >= 20 characters: `"Routine server maintenance approved by Head of IT for migration"`.
  6. Click **"Confirm Dismissal"**.
- **Expected Result:**
  - Submitting with < 20 characters is blocked by validation error message (`BR-PRIMA-006`).
  - Submitting valid rationale successfully transitions issue to `Dismissed`.
  - Issue is removed from active `S-01` list and archived in `S-07`.

---

# 7. Group C: Investigation & Telemetry Evidence

## TC-03-01 — Review Structured Telemetry Evidence (Use Case `C-1`)
- **Objective:** Verify formatted rendering of financial vs. non-financial structured telemetry evidence.
- **Preconditions:** Issues from different detectors available on `S-02`.
- **Steps:**
  1. For `JournalBalanceDetector` issue: Inspect the Evidence Card on `S-02`. Verify fields: `Source Table` (`simrs.acc_journal_entries`), `Record Keys`, `Debit Total`, `Credit Total`, `Variance Delta`.
  2. For `PrintQueueSpoolerDetector` issue: Inspect the Evidence Card. Verify non-financial fields (queue name, stuck jobs count, oldest job age) while financial fields are absent or null.
- **Expected Result:**
  - Evidence card displays cleanly formatted currency amounts (e.g. `Rp 15.000.000`) and structured key-value pairs without raw unescaped JSON strings.

---

## TC-03-02 — Review Occurrence History Timeline (Use Case `C-2`)
- **Objective:** Inspect immutable historical occurrences for recurring anomalies (`BR-PRIMA-009`).
- **Preconditions:** Issue with Occurrence Count > 1 selected on `S-02`.
- **Steps:**
  1. Scroll down to the **"Occurrences History"** section on `S-02`.
  2. Expand individual occurrence items.
- **Expected Result:**
  - Each occurrence displays its exact ingestion timestamp, run ID link, and snapshot of finding evidence at that point in time in reverse chronological order.

---

## TC-03-03 — Add Investigation Note (Use Case `C-3`)
- **Objective:** Add collaborative investigation notes to active issue.
- **Preconditions:** Issue is in `Investigating` status on `S-02`.
- **Steps:**
  1. In the "Investigation Notes" panel, enter note: `"Checked SIMRS accounting UI; batch has unbalanced credit on line 4."`
  2. Click **"Add Note"**.
- **Expected Result:**
  - Note appears immediately in timeline with authenticated officer name and UTC timestamp.

---

# 8. Group D: Remediation & Verification Gate

## TC-04-01 — Record Remediation Action (Use Case `D-1`, Modal `M-02`)
- **Objective:** Record operational remediation action categorizing the correction type.
- **Preconditions:** Issue is in `Investigating` status on `S-02`.
- **Steps:**
  1. Click **"Record Remediation"** button. Modal `M-02` opens.
  2. Select Action Category: `"DataCorrection"`.
  3. Enter Description: `"Added balancing credit entry in SIMRS journal correction menu per SOP-PRIMA-EDP-001."`
  4. Click **"Save Remediation"**.
- **Expected Result:**
  - Remediation record is saved and displayed in the remediation card.
  - Activity log appends the remediation action.

---

## TC-04-02 — Verification Gate Enforcement & Automated Re-Run (Use Case `D-2`, `D-4`)
- **Objective:** Enforce `BR-PRIMA-004` Verification Gate (blocking resolution until clean verification is recorded) and verify automated probe re-run.
- **Preconditions:** Remediation recorded on issue, but verification NOT yet performed.
- **Steps:**
  1. Inspect the **"Resolve Issue"** button on `S-02`.
  2. Verify that **"Resolve Issue"** is strictly **DISABLED**.
  3. In staging SIMRS database, fix the anomaly data (or update record to balanced).
  4. Click **"Trigger Verification Re-run"** (`D-2`).
  5. Wait for the synchronous webhook response (within 15 seconds).
- **Expected Result:**
  - Probe executes and returns `Outcome: Passed`.
  - System automatically creates a `CleanPassed` verification record.
  - The **"Resolve Issue"** button transitions to **ENABLED**.

---

## TC-04-03 — Manual Verification Fallback (Use Case `D-3`, Modal `M-03`)
- **Objective:** Record manual verification when webhook re-run is unavailable or unnecessary.
- **Preconditions:** Issue in `Investigating` status with remediation recorded.
- **Steps:**
  1. Click **"Record Manual Verification"** button. Modal `M-03` opens.
  2. Enter verification notes: `"Executed Diagnostic SQL in SSMS; confirmed variance is 0."`
  3. Select Outcome: `"CleanPassed"`.
  4. Click **"Save Verification"**.
- **Expected Result:**
  - Manual verification certificate is created and appended to the issue.
  - Verification Gate clears; **"Resolve Issue"** button becomes active.

---

## TC-04-04 — Mark Issue Resolved (Use Case `D-4`)
- **Objective:** Transition issue to `Resolved` status after clearing the Verification Gate.
- **Preconditions:** Issue has a valid `CleanPassed` verification record.
- **Steps:**
  1. Click **"Resolve Issue"** button on `S-02`.
  2. Confirm resolution in prompt dialog.
- **Expected Result:**
  - Issue status updates to `Resolved`.
  - Resolution timestamp and officer attribution are recorded.
  - Issue is removed from active `S-01` feed and marked resolved.

---

# 9. Group E: Detector Portfolio, Handshake Enrollment & Cadence Supervision

## TC-05-01 — View Detector Portfolio (Use Case `E-1`)
- **Objective:** Inspect detector inventory, health states, and cadence summary on Screen `S-03`.
- **Preconditions:** Detectors registered in system.
- **Steps:**
  1. Click **"Detectors"** in sidebar navigation.
  2. Inspect detector cards and summary strip. Verify all 6 pilot detectors are present.
  3. Verify health badges (`Healthy`, `Silent`, `Failing`), cadence, and last run timestamp.
- **Expected Result:**
  - All detectors display correct health state and operational scope (`DataIntegrity`, `ProcessIntegrity`, `DataSync`, `Infrastructure`).

---

## TC-05-02 — Quick Enroll Modal & Plaintext Token Display (`DEC-004`)
- **Objective:** Verify generation of single-use 24-hour enrollment token with copyable environment snippets on `S-03`.
- **Preconditions:** Authenticated as an EDP officer on `S-03`.
- **Steps:**
  1. On `S-03` header, click **"Quick Enroll (Handshake)"** button.
  2. In Step 1, enter label: `"Staging Inpatient Runner"`.
  3. Click **"Generate Enrollment Token"**.
  4. In Step 2, inspect the plaintext token monospace block (`PRM-TOKEN-...`).
  5. Click **"Copy"** button on the environment variable snippet block (`PRIMA_ENROLLMENT_TOKEN`, `PRIMA_SERVER_URL`).
  6. Close the modal and re-open it.
- **Expected Result:**
  - Plaintext token secret is generated and displayed once.
  - Copy button confirms clipboard copy with toast message.
  - On closing the modal, sensitive token secret is cleared from memory and not shown again.

---

## TC-05-03 — Pending Tokens Strip & Token Revocation (`DEC-004`)
- **Objective:** Review pending unconsumed tokens and execute administrative token revocation on `S-03`.
- **Preconditions:** At least one unconsumed enrollment token exists.
- **Steps:**
  1. Locate the **"Pending Enrollment Tokens"** collapsible strip on `S-03`.
  2. Verify token prefix, label, created timestamp, and 24h expiration countdown are visible.
  3. Click the **"Revoke"** (trash) icon on a test token.
  4. Confirm revocation in dialog.
- **Expected Result:**
  - Token status is updated to `Revoked` in `dbo.DetectorEnrollmentTokens`.
  - Token is immediately removed from the pending tokens strip.

---

## TC-05-04 — Automated Bootstrap Handshake (`POST /api/detectors/handshake`)
- **Objective:** Verify automated zero-drift manifest enrollment, per-detector API key provisioning, and local credential caching using .NET 8 runner.
- **Preconditions:** Valid unconsumed token generated from TC-05-02.
- **Steps:**
  1. Set environment variable: `PRIMA_ENROLLMENT_TOKEN="PRM-TOKEN-..."`.
  2. Launch runner in one-shot mode:
     ```powershell
     dotnet run --project src/backend/PrIMA.Detectors/PrIMA.Detectors.csproj -- --run-once
     ```
  3. Inspect console logs and local working directory for `.prima-credentials.json`.
- **Expected Result:**
  - Runner successfully calls `POST /api/detectors/handshake`.
  - Server provisions unique per-detector API key (`prm_key_...`) and stores SHA-256 hash in `dbo.Detectors.ApiKeyHash`.
  - Enrollment token is marked `Consumed` with detector ID and timestamp.
  - Credentials file `.prima-credentials.json` is created locally.
  - Subsequent executions load cached credentials without re-handshaking.

---

## TC-05-05 — Cadence Drift Detection & Non-Blocking Warning (`BR-PRIMA-010`)
- **Objective:** Verify detection and visual flagging of execution interval divergence > ±50% without blocking ingestion.
- **Preconditions:** Detector configured with `ExpectedCadence = 300s`.
- **Steps:**
  1. Submit two scheduled telemetry runs where actual interval = 120s (divergence > -50%) or 600s (+100%).
  2. Refresh `S-03` (Detector List) and navigate to `S-04` (Detector Detail) for this detector.
- **Expected Result:**
  - Ingestion response returns 200 OK with `warnings` array populated.
  - `dbo.Detectors` stores `LastActualIntervalSeconds` and `CadenceDriftWarning`.
  - Amber **Cadence Drift Warning** chip appears on `S-03` and warning banner renders on `S-04`.
  - Telemetry findings are ingested normally.

---

## TC-05-06 — Pause / Resume Detector (Use Case `E-5`)
- **Objective:** Administrative pause and resume of detector with silence alert suppression.
- **Preconditions:** Active detector viewed on `S-04`.
- **Steps:**
  1. Click **"Pause Detector"** button on `S-04`.
  2. Verify detector status updates to `Paused`.
  3. Wait beyond 2× expected cadence (silence threshold). Confirm NO synthetic `SILENT:{id}` issues are created.
  4. Click **"Resume Detector"**.
- **Expected Result:**
  - Status updates back to `Active`.
  - Silence alerts remain suppressed while paused.

---

## TC-05-07 — Trigger Manual Inspection Run (Use Case `E-7`)
- **Objective:** Dispatch synchronous on-demand probe run from `S-04`.
- **Preconditions:** Webhook-enabled detector viewed on `S-04`.
- **Steps:**
  1. Click **"Trigger Run Now"** button on `S-04` Action Bar.
  2. Observe loading state and completion notification.
- **Expected Result:**
  - Synchronous run executes within 15 seconds.
  - Result appears at the top of recent runs list with trigger badge `"Manual"`.

---

# 10. Group F: Inspection Run History

## TC-06-01 — View Run History Log (Use Case `F-1`, `F-2`)
- **Objective:** Inspect comprehensive inspection run execution logs on Screen `S-06`.
- **Preconditions:** Inspection runs recorded in database.
- **Steps:**
  1. From `S-04`, click **"View All Runs"** or navigate to `/detectors/:id/runs`.
  2. Inspect run entries (timestamp, trigger type, duration ms, outcome).
  3. Expand a run row with outcome `FindingDiscovered`.
- **Expected Result:**
  - Expanded view details findings count, execution duration, raw diagnostics snippet, and direct link to the created issue on `S-02`.

---

# 11. Group G: Notifications & Escalation Awareness

## TC-07-01 — SLA Auto-Escalation Alert (Use Case `G-1`, `BR-PRIMA-004`)
- **Objective:** Verify background dynamic escalation of unacknowledged high-severity issues.
- **Preconditions:** Issue with severity `High` and status `Open` has `CreatedAtUtc` > 30 minutes ago.
- **Steps:**
  1. Allow supervisor worker to run its 60-second cycle (or trigger supervisor pass).
  2. Refresh `S-01 (Issue Dashboard)`.
- **Expected Result:**
  - Prominent alert banner renders on `S-01`: `"⚠ Issues require attention"`.
  - Issue severity is escalated to `Critical` with `EscalationLevel = 1` and reason recorded in activity log.

---

## TC-07-02 — Reopened Issue Detection (Use Case `G-2`, `BR-PRIMA-003`)
- **Objective:** Verify automatic reopening of resolved issue when identical anomaly recurs.
- **Preconditions:** Previously resolved issue exists.
- **Steps:**
  1. Take a resolved issue (e.g., `JRN-202611-ERR01`).
  2. Re-introduce identical anomaly into the `simrs.acc_journal_entries` table.
  3. Run detector probe matching correlation key.
  4. Inspect `S-01`.
- **Expected Result:**
  - Issue status automatically transitions to `Reopened` (`BR-PRIMA-003`).
  - Prior resolution records are superseded.
  - Issue reappears in `S-01` with prominent `Reopened` chip.

---

# 12. Group H: Historical Reliability Reporting & Period-Close Audit

## TC-08-01 — System Health Summary KPI Cards (Use Case `H-1`)
- **Objective:** Inspect period-close operational KPIs on Screen `S-07 (History & Reports)`.
- **Preconditions:** Historical runs and issues exist in selected date range.
- **Steps:**
  1. Click **"History"** in sidebar navigation.
  2. Inspect the 6 period-close KPI cards: Total Issues, Resolution Rate %, MTTA, MTTR, Detector Pass Rate %, Reopened Issues.
  3. Inspect Scope and Severity proportion breakdown bars.
- **Expected Result:**
  - All 6 KPI cards display accurate values formatted in monospace typography.
  - Proportion distributions render using pure Tailwind CSS bars without external charting libraries.

---

## TC-08-02 — Issue Archive & Period-Close Audit (Use Case `H-2`)
- **Objective:** Filter historical ledger by status and date range and perform deep audit inspection.
- **Preconditions:** Historical resolved and dismissed issues exist.
- **Steps:**
  1. On `S-07`, filter by Status: `"Resolved"` and `"Dismissed"`.
  2. Select preset: `"This Month"`.
  3. Click a resolved issue row.
- **Expected Result:**
  - Opens `S-02` in read-only audit mode with `← Back to History` navigation.
  - Displays full audit trail (triaging officer, remediation applied, verification outcome, resolution date).
  - Clicking `← Back to History` preserves previous filter and preset selections.

---

# 13. Group I: Pilot SIMRS Detector Suite & Non-Intrusive Safety Guardrails

## TC-09-01 — Pilot SIMRS 6-Detector Suite Execution against Sandbox
- **Objective:** Verify accurate anomaly discovery across all 6 production pilot detectors running against `simrs.*` sandbox tables.
- **Preconditions:** Seeded sandbox anomalies exist.
- **Steps:**
  1. Execute each pilot probe via `PrIMA.Detectors` runner:
     - `JournalBalanceDetector`
     - `DuplicateReceivableDetector`
     - `UnprocessedBillingPipelineDetector`
     - `EmrSyncDetector`
     - `HfisScheduleSyncDetector`
     - `PrintQueueSpoolerDetector`
  2. Inspect generated findings and created issues on `S-01`.
- **Expected Result:**
  - Exactly 6 distinct issues are created with correct scope mappings:
    - 2 under `DataIntegrity` (Accounting, Billing)
    - 1 under `ProcessIntegrity` (Billing)
    - 2 under `DataSync` (EMR, Outpatient)
    - 1 under `Infrastructure` (PrintSpooler)
  - Correlation keys follow standard format `{detectorId}:{entityId}`.

---

## TC-09-02 — Non-Intrusive Read-Only Safety Enforcements (`BR-PRIMA-007`)
- **Objective:** Verify that `ReadOnlySimrsConnectionFactory` and `SimrsSqlSafetyGuard` enforce non-intrusive safety constraints.
- **Preconditions:** Automated safety test suite `SimrsNonInterferenceSafetyTests.cs`.
- **Steps:**
  1. Run automated safety tests:
     ```powershell
     dotnet test src/backend/PrIMA.Detectors.Tests --filter FullyQualifiedName~SimrsNonInterferenceSafetyTests
     ```
  2. Inspect test assertions:
     - DML statements (`INSERT`, `UPDATE`, `DELETE`, `DROP`, `ALTER`, `TRUNCATE`) are statically rejected.
     - Connection string enforces `ApplicationIntent=ReadOnly`.
     - Session executes `SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED` and `SET LOCK_TIMEOUT 2000`.
     - Command timeout enforced at 5 seconds.
     - Query execution completes in < 500ms without blocking concurrent simulated writes.
- **Expected Result:**
  - 100% of safety tests pass cleanly.

---

## TC-09-03 — Automated Fail-Safe Exception Trapping (`BR-PRIMA-005`)
- **Objective:** Verify runner traps database disconnects or runtime errors, transmitting `Outcome = ExecutionFailed` and creating synthetic `FAIL:{id}` issue.
- **Preconditions:** Temporarily simulate database failure or invalid table name for a probe.
- **Steps:**
  1. Execute probe with unreachable database or faulty query.
  2. Observe runner telemetry output and backend processing.
- **Expected Result:**
  - Runner does NOT crash; exception is caught and logged.
  - Telemetry payload reports `Outcome = ExecutionFailed` with diagnostic error message.
  - Backend creates synthetic `FAIL:{detectorId}` infrastructure issue (`BR-PRIMA-005`).

---

# 14. Group J: Deployment Assets, Retention Pruning & Deep Health Check

## TC-10-01 — Deep Database Health Check Probe (`GET /api/health`)
- **Objective:** Verify deep health check endpoint verifies live SQL Server connectivity.
- **Preconditions:** Backend Web API running.
- **Steps:**
  1. Send request: `GET http://localhost:5005/api/health`.
  2. Inspect HTTP status code and response body.
- **Expected Result:**
  - HTTP `200 OK`.
  - Body: `{ "status": "Healthy", "database": "Connected" }`.
  - If database is stopped, returns `503 Service Unavailable` with `{ "status": "Unhealthy", "database": "Disconnected" }`.

---

## TC-10-02 — Automated 90-Day Retention Pruning (`BR-PRIMA-009`)
- **Objective:** Verify supervisor prunes clean `Passed` runs older than 90 days while preserving findings, occurrences, and issues.
- **Preconditions:** Older `Passed` inspection runs exist (> 90 days old) in `dbo.InspectionRuns`.
- **Steps:**
  1. Execute retention pruning routine in `MonitoringSupervisorWorker`.
  2. Query database for counts before and after pruning.
- **Expected Result:**
  - Old clean `Passed` runs are deleted in batches of 1000.
  - Runs with `FindingDiscovered` or `ExecutionFailed` are NOT deleted.
  - Zero records in `dbo.Occurrences` or `dbo.Issues` are deleted (`BR-PRIMA-009`).

---

## TC-10-03 — On-Premises Production Deployment Assets & Runbooks Validation
- **Objective:** Verify completeness of production deployment scripts, templates, and EDP documentation.
- **Preconditions:** Inspect files in `deploy/` and `docs/operations/`.
- **Steps:**
  1. Verify `deploy/appsettings.Production.json` exists with required configuration schema.
  2. Verify reverse-proxy templates exist (`deploy/nginx/` and `deploy/iis/`).
  3. Verify DBA read-only login script `deploy/sql/provision-simrs-readonly-login.sql` sets up non-intrusive permissions.
  4. Verify Windows Service script `deploy/windows/install-prima-service.ps1` is present.
  5. Verify `docs/operations/PRIMA-EDP-REMEDIATION-SOP.md` provides diagnostic queries and correction steps for all 6 detectors.
  6. Verify `docs/operations/PRIMA-UAT-AND-HYPERCARE-PLAYBOOK.md` contains shift checklists, threshold tuning, and rollback runbooks.
- **Expected Result:**
  - All operational deployment assets and documentation files exist, are syntactically valid, and ready for hospital IT handoff.

---

# 15. End-to-End Hospital Go-Live Staging Simulation Smoke Test

## TC-11-01 — Complete End-to-End Hospital Operational Shift Walkthrough
- **Objective:** Execute the entire hospital EDP operational journey across the integrated solution, validating zero-drift enrollment, anomaly discovery, triage, remediation, verification re-run, resolution, and retrospective reporting.
- **Preconditions:** Backend, Frontend, and SQL Server running in Staging mode. Authenticated as `ahmad.budi`.
- **Steps:**
  1. **Handshake Enrollment:**
     - Open `S-03`. Click **"Quick Enroll"**, generate token, and observe pending strip.
     - Execute `PrIMA.Detectors` with token; confirm handshake completes and key is provisioned.
  2. **Probe Execution & Telemetry:**
     - Run 6 pilot detectors against sandbox; verify 6 active issues discovered.
  3. **Dashboard Triage:**
     - Open `S-01`. Locate `JRN-202611-ERR01` (Unbalanced Journal).
     - Open `S-02`. Click **"Acknowledge"**, then click **"Assign to Me"**.
  4. **Investigation & Remediation:**
     - Inspect financial evidence card (`Debit: 15.000.000`, `Credit: 14.500.000`, `Variance: 500.000`).
     - Click **"Record Remediation"** (`M-02`). Select `DataCorrection`, enter description, and save.
  5. **Verification Gate Enforcement:**
     - Verify **"Resolve Issue"** is disabled.
     - In SQL sandbox, balance the journal line.
     - Click **"Trigger Verification Re-run"** (`D-2`).
     - Webhook probe returns `Passed`; verify `CleanPassed` record unlocks **"Resolve Issue"**.
  6. **Resolution & Archival:**
     - Click **"Resolve Issue"**. Confirm issue moves to `Resolved`.
  7. **Historical Audit & Period Close:**
     - Open `S-07 (History)`. Verify `JRN-202611-ERR01` appears in resolved ledger.
     - Verify KPI cards update with accurate MTTR and resolution rate.
- **Expected Result:**
  - End-to-end journey completes seamlessly with zero errors.
  - Confirms system is fully operational and approved for hospital Go-Live.

---

# 16. Traceability Matrix

| Test Case | Scope / Component | Business Rule | Architecture Decision | Primary Screen / Endpoint |
|---|---|---|---|---|
| **TC-001** | Backend Quality Gate | All (`BR-PRIMA-001`–`010`) | All (`DEC-001`–`009`) | `src/backend/PrIMA.slnx` |
| **TC-002** | Frontend Compliance | `UI-UX Guideline §10` | `DEC-008` | `audit:compliance` |
| **TC-003** | Frontend Build | `UI-UX Guideline` | `DEC-008` | `npm run build` |
| **TC-004** | Contract Sync | OpenAPI Standards | `DEC-004` | `api.generated.ts` |
| **TC-01-01** | Dashboard View | Operational Overview | `DEC-004` | `S-01` |
| **TC-01-02** | Issue Filtering | Search & Filter | `DEC-004` | `S-01` |
| **TC-01-03** | Issue Detail | Context Presentation | `DEC-004` | `S-02` |
| **TC-02-01** | Acknowledge Issue | `BR-PRIMA-004` | `DEC-004` | `S-02` |
| **TC-02-02** | Assign Issue | `BR-PRIMA-008` | `DEC-004` | `S-02` |
| **TC-02-03** | Dismiss Issue | `BR-PRIMA-006` | `DEC-004` | `S-02`, `M-01` |
| **TC-03-01** | Telemetry Evidence | `BR-PRIMA-001`, `007` | `DEC-007` | `S-02` |
| **TC-03-02** | Occurrence Timeline | `BR-PRIMA-009` | `DEC-007` | `S-02` |
| **TC-03-03** | Investigation Notes | `BR-PRIMA-008` | `DEC-004` | `S-02` |
| **TC-04-01** | Record Remediation | `BR-PRIMA-004` | `DEC-004` | `S-02`, `M-02` |
| **TC-04-02** | Webhook Verification | `BR-PRIMA-004` | `DEC-005` | `S-02`, `POST .../trigger-run` |
| **TC-04-03** | Manual Verification | `BR-PRIMA-004` | `DEC-004` | `S-02`, `M-03` |
| **TC-04-04** | Resolve Issue | `BR-PRIMA-004` | `DEC-004` | `S-02` |
| **TC-05-01** | Detector Portfolio | Health State Summary | `DEC-004` | `S-03` |
| **TC-05-02** | Quick Enroll Modal | Enrollment Protocol | `DEC-001`, `DEC-004` | `S-03`, `EnrollmentTokenModal` |
| **TC-05-03** | Pending Tokens Strip | Token Governance | `DEC-001`, `DEC-004` | `S-03`, `PendingTokensStrip` |
| **TC-05-04** | Bootstrap Handshake | Zero-Drift Manifest | `DEC-001`, `DEC-002` | `POST /api/detectors/handshake` |
| **TC-05-05** | Cadence Drift Alert | `BR-PRIMA-010` | `DEC-003` | `S-03`, `S-04` |
| **TC-05-06** | Pause/Resume Detector | `BR-PRIMA-010` | `DEC-009` | `S-04` |
| **TC-05-07** | Manual Probe Run | Probe Execution | `DEC-005` | `S-04`, `POST .../run` |
| **TC-06-01** | Inspection Run Log | Execution Auditing | `DEC-006` | `S-06` |
| **TC-07-01** | SLA Auto-Escalation | `BR-PRIMA-004` | `DEC-010` | `S-01`, Supervisor Worker |
| **TC-07-02** | Reopened Issues | `BR-PRIMA-003` | `DEC-010` | `S-01`, `POST /api/ingestion/...` |
| **TC-08-01** | Period-Close KPIs | Reporting Metrics | `DEC-001`, `DEC-003` | `S-07` |
| **TC-08-02** | Historical Ledger | Period Audit | `DEC-004`, `DEC-007` | `S-07`, `S-02` |
| **TC-09-01** | Pilot SIMRS Probes | `BR-PRIMA-007` | `DEC-006`, `DEC-007` | `PrIMA.Detectors` |
| **TC-09-02** | Non-Intrusive Safety | `BR-PRIMA-007` | `DEC-008` | `ReadOnlySimrsConnectionFactory` |
| **TC-09-03** | Exception Trapping | `BR-PRIMA-005` | `DEC-005` | `PrIMA.Detectors` |
| **TC-10-01** | Deep Health Check | Operational Health | `DEC-009` | `GET /api/health` |
| **TC-10-02** | Retention Pruning | `BR-PRIMA-009` | `DEC-009` | `MonitoringSupervisorWorker` |
| **TC-10-03** | Deployment Assets | Production Readiness | `DEC-009` | `deploy/`, `docs/operations/` |
| **TC-11-01** | End-to-End Walkthrough| Complete Journey | All Decisions | Full System Integration |

---

# 17. Acceptance Gate Criteria & Sign-Off Checklist

### 17.1 Acceptance Gate Criteria
The Stage 5 implementation achieves formal acceptance when:
- [ ] **Automated Quality Gate:** 100% of automated tests pass (TC-001 backend, TC-002 static compliance, TC-003 frontend build, TC-004 contract sync).
- [ ] **Functional & Operational Coverage:** All test cases in Sections 5 through 15 are executed against the staging environment and recorded in `TEST-EXECUTION`.
- [ ] **Zero Unresolved Defects:** Every test case achieves `PASS`. Any defect marked `FAIL` must be resolved through an ISSUE (BUG) and re-verified.
- [ ] **Stakeholder Sign-Off:** All 4 role signatories in Section 17.2 sign off on operational readiness.

### 17.2 Formal UAT & Production Hypercare Sign-Off Sheet

| Role | Name | Title / Organization | Signature | Date |
|---|---|---|---|---|
| **EDP Lead** | ____________________ | Kepala Instalasi SIMRS & EDP | ____________________ | ____________ |
| **Head of IT** | ____________________ | Direktur / Kabid Teknologi Informasi | ____________________ | ____________ |
| **QA / Test Lead** | ____________________ | Quality Assurance Lead | ____________________ | ____________ |
| **Finance Sponsor** | ____________________ | Kepala Bagian Keuangan & Akuntansi | ____________________ | ____________ |

---

# 18. Next Workflow Stage & Owning Role

Upon approval of this TEST-PACKAGE:
1. **Testing Assistance (`ica-tester` / `ics-testing-assistance`):** Guide the human testing team (Hospital EDP Officers & QA Engineers) through test execution, recording results, capturing evidence, and documenting any defects in `TEST-EXECUTION`.
2. **Issue Creation (`ica-issuer`):** If any test fails, record the defect details and hand off to the Issuer to generate formal `BUG` ISSUE artifacts.
3. **Gate Approval (`ica-tester`):** Grant `TEST PASSED` gate once 100% of test cases are `PASS`.
4. **Production Go-Live & Hypercare:** Transition to 2-week production hypercare governed by `docs/operations/PRIMA-UAT-AND-HYPERCARE-PLAYBOOK.md §4`.
