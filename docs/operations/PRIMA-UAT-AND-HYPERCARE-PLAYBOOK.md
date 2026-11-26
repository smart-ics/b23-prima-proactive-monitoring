# PrIMA Staging UAT & Production Hypercare Playbook

**Document ID:** `PLAYBOOK-PRIMA-UAT-001`  
**Version:** `1.0.0`  
**Target Audience:** Hospital EDP Officers, QA Engineers, IT Operations Leads, Clinical/Financial Application Owners  
**Target Environments:** Hospital Staging / Pre-Production & Production Environments  
**Related Architecture Decisions:** `DEC-004`, `DEC-007`, `DEC-008`, `DEC-009`, `BR-PRIMA-001` through `BR-PRIMA-010`

---

## 1. Document Overview & Objective

This playbook serves two operational purposes for the deployment of **PrIMA (Proactive Incident Monitoring & Analytics)** within the hospital infrastructure:

1. **Part 1 — User Acceptance Testing (UAT) Execution Guide:** A formal, repeatable verification battery covering all product use cases (**`A-1` through `H-2`** plus **Zero-Drift Handshake Enrollment**). It validates that PrIMA functions reliably under realistic operational workflows before receiving Go-Live authorization.
2. **Part 2 — Two-Week Production Hypercare Playbook:** Operational guardrails, shift checklists, metric thresholds, tuning formulas, and incident rollback procedures to ensure smooth post-deployment adoption by hospital EDP officers without disruption to clinical or financial services.

---

## 2. Staging UAT Pre-Requisites & Environment Setup

Before commencing UAT execution, verify that the staging environment satisfies the following baseline criteria:

### 2.1 Environmental Readiness Checklist
- [ ] **Database Migration:** DbUp script `005_Stage5HandshakeAndSimrsSandbox.sql` has executed successfully on staging SQL Server.
- [ ] **Sandbox Fixtures Seeded:** The 6 pilot anomaly records exist in `simrs.*` tables (`JRN-202611-ERR01`, `TRX-AR-202611-DUP01`, `BILL-202611-NOJRN01`, `ENC-202611-FAIL01`, `SCH-OBG-002`, `PRN-KASIR-02`).
- [ ] **Backend Web API:** `PrIMA.Api` is running on port 5000 (or HTTPS 443 via reverse proxy). Health probe `GET /api/health` returns `200 OK` `{ "status": "Healthy", "database": "Connected" }`.
- [ ] **Detector Runner:** `PrIMA.Detectors` is running (or ready to execute in `--run-once` test mode).
- [ ] **Frontend Application:** Vue 3 SPA is accessible via browser on staging URL (e.g., `https://prima-staging.hospital.internal`).
- [ ] **Officer Credentials:** Valid EDP test accounts exist (seeded via `002_SeedInitialUsers.sql` or Active Directory).

---

## 3. Comprehensive UAT Scenario Test Scripts

Each test case below must be executed in sequence and signed off by the test executor.

---

### Group A — Dashboard & Issue Monitoring

#### UAT-A-01: View Issue Dashboard (Use Case `A-1`)
* **Screen:** `S-01 (Issue Dashboard)`
* **Pre-conditions:** Logged into PrIMA. Anomaly telemetry has been submitted by pilot detectors.
* **Test Steps:**
  1. Navigate to `/` or click **"Dashboard"** in sidebar navigation.
  2. Inspect the KPI summary strip at the top of the screen (Open, Investigating, Critical, High counts).
  3. Inspect the virtualized issue table.
* **Expected Outcome:** Active issues are grouped and ordered by priority (Critical/High first, then by age). Clean status indicators and severity badges are displayed. No pagination controls exist; virtual scrolling handles large lists smoothly.
* **Status:** `[ ] PASS  [ ] FAIL` | **Tester:** _______________ | **Date:** _________

#### UAT-A-02: Filter & Search Issues (Use Case `A-2`)
* **Screen:** `S-01 (Issue Dashboard)`
* **Pre-conditions:** Multiple issues exist across different severities, scopes, and modules.
* **Test Steps:**
  1. In the search input, type `"JRN"` or `"Billing"`.
  2. Filter by Severity: Select `"Critical"`.
  3. Filter by Scope: Select `"DataIntegrity"`.
  4. Clear all filters.
* **Expected Outcome:** The issue feed immediately updates without page reloads. Search matches titles and correlation keys. Clearing filters restores the full active issue list.
* **Status:** `[ ] PASS  [ ] FAIL` | **Tester:** _______________ | **Date:** _________

#### UAT-A-03: View Issue Detail (Use Case `A-3`)
* **Screen:** `S-02 (Issue Detail)`
* **Pre-conditions:** At least one active issue displayed on S-01.
* **Test Steps:**
  1. Click an issue row on S-01 (e.g., *Unbalanced Journal Batch JRN-202611-ERR01*).
  2. Observe the page transition to S-02.
* **Expected Outcome:** Full operational context loads: Issue title, unique correlation key (`{detectorId}:{entity}`), severity badge, target entity reference, current status (`Open`), first/last occurrence timestamps, and telemetry evidence block.
* **Status:** `[ ] PASS  [ ] FAIL` | **Tester:** _______________ | **Date:** _________

---

### Group B — Issue Triage & Ownership

#### UAT-B-01: Acknowledge Issue (Use Case `B-1`)
* **Screen:** `S-02 (Issue Detail)`
* **Pre-conditions:** An issue in `Open` status is selected.
* **Test Steps:**
  1. On the action panel, click the **"Acknowledge"** button.
  2. Verify notification feedback (toast message).
* **Expected Outcome:** Issue status transitions from `Open` to `Acknowledged`. Escalation timer stops. Activity log records the event attributed to the authenticated EDP officer.
* **Status:** `[ ] PASS  [ ] FAIL` | **Tester:** _______________ | **Date:** _________

#### UAT-B-02: Assign Issue to Officer (Use Case `B-2`)
* **Screen:** `S-02 (Issue Detail)`
* **Pre-conditions:** Issue is in `Acknowledged` (or `Open`) status.
* **Test Steps:**
  1. Click **"Assign to Me"** (or select officer from dropdown).
  2. Confirm assignment.
* **Expected Outcome:** Issue status transitions to `Investigating`. Assigned officer field updates with current officer name. Activity log appends assignment record.
* **Status:** `[ ] PASS  [ ] FAIL` | **Tester:** _______________ | **Date:** _________

#### UAT-B-03: Dismiss Issue with Justification (Use Case `B-3`, Modal `M-01`)
* **Screen:** `S-02 (Issue Detail)` -> `M-01 (Dismiss Issue Modal)`
* **Pre-conditions:** Issue in `Open` or `Investigating` status.
* **Test Steps:**
  1. Click **"Dismiss Issue"** button. Modal `M-01` opens.
  2. Select Reason: `"PlannedMaintenance"`.
  3. Enter rationale with less than 20 characters (e.g. `"test dismiss"`).
  4. Attempt to click **"Confirm Dismissal"**.
  5. Update rationale with `>= 20 characters` (e.g., `"Routine server maintenance approved by Head of IT for migration"`).
  6. Click **"Confirm Dismissal"**.
* **Expected Outcome:** Submitting with < 20 characters is blocked by validation error (`BR-PRIMA-006`). Submitting valid rationale successfully transitions issue to `Dismissed`. Issue disappears from S-01 and is archived in S-07.
* **Status:** `[ ] PASS  [ ] FAIL` | **Tester:** _______________ | **Date:** _________

---

### Group C — Investigation & Evidence Review

#### UAT-C-01: Review Structured Telemetry Evidence (Use Case `C-1`)
* **Screen:** `S-02 (Issue Detail)`
* **Pre-conditions:** Issue with financial or non-financial evidence selected.
* **Test Steps:**
  1. For `JournalBalanceDetector` issue: Inspect Evidence Card.
  2. Verify fields: `Source Table` (`simrs.acc_journal_entries`), `Record Keys`, `Debit Total`, `Credit Total`, `Variance Delta`.
  3. For `PrintQueueSpoolerDetector` issue: Verify financial fields are absent or `null` per `PRIMA-DOMAIN.md §TelemetryEvidence`.
* **Expected Outcome:** Structured JSON evidence displays cleanly formatted financial figures and trace text without rendering raw unformatted brackets.
* **Status:** `[ ] PASS  [ ] FAIL` | **Tester:** _______________ | **Date:** _________

#### UAT-C-02: Review Occurrence History Timeline (Use Case `C-2`)
* **Screen:** `S-02 (Issue Detail)`
* **Pre-conditions:** Issue has recurred across multiple inspection runs (Occurrence Count > 1).
* **Test Steps:**
  1. Scroll to the **"Occurrences History"** card on S-02.
  2. Expand individual occurrence items.
* **Expected Outcome:** Each occurrence shows its exact ingestion timestamp, run ID link, and snapshot of the finding evidence at that point in time.
* **Status:** `[ ] PASS  [ ] FAIL` | **Tester:** _______________ | **Date:** _________

#### UAT-C-03: Add Investigation Note (Use Case `C-3`)
* **Screen:** `S-02 (Issue Detail)`
* **Pre-conditions:** Issue is in `Investigating` status.
* **Test Steps:**
  1. In the "Investigation Notes" panel, enter note: `"Checked SIMRS accounting UI; batch has unbalanced credit on line 4."`
  2. Click **"Add Note"**.
* **Expected Outcome:** Note appends instantly to the timeline with the author's username and UTC timestamp.
* **Status:** `[ ] PASS  [ ] FAIL` | **Tester:** _______________ | **Date:** _________

---

### Group D — Remediation & Verification Gate

#### UAT-D-01: Record Remediation Action (Use Case `D-1`, Modal `M-02`)
* **Screen:** `S-02 (Issue Detail)` -> `M-02 (Record Remediation Modal)`
* **Pre-conditions:** Issue is `Investigating`.
* **Test Steps:**
  1. Click **"Record Remediation"**. Modal `M-02` opens.
  2. Select Action Category: `"DataCorrection"`.
  3. Enter Description: `"Added balancing credit entry in SIMRS journal correction menu per SOP-PRIMA-EDP-001."`
  4. Click **"Save Remediation"**.
* **Expected Outcome:** Remediation record is saved. Activity log shows remediation entry.
* **Status:** `[ ] PASS  [ ] FAIL` | **Tester:** _______________ | **Date:** _________

#### UAT-D-02: Verification Gate Enforcement & Automated Re-Run (Use Case `D-2`, `D-4`)
* **Screen:** `S-02 (Issue Detail)`
* **Pre-conditions:** Remediation recorded, but verification NOT yet performed.
* **Test Steps:**
  1. Inspect the **"Resolve Issue"** button.
  2. Verify that **"Resolve Issue"** is **DISABLED** (enforcing Verification Gate).
  3. In staging SIMRS database, fix the data anomaly (or execute correction).
  4. Click **"Trigger Verification Re-run"** (`D-2`).
  5. Wait for the synchronous webhook response (within 15 seconds).
* **Expected Outcome:** PrIMA receives `Outcome: Passed` from probe webhook. System automatically creates a `CleanPassed` verification record. The **"Resolve Issue"** button transitions to **ENABLED**.
* **Status:** `[ ] PASS  [ ] FAIL` | **Tester:** _______________ | **Date:** _________

#### UAT-D-03: Manual Verification Fallback (Use Case `D-3`, Modal `M-03`)
* **Screen:** `S-02 (Issue Detail)` -> `M-03 (Manual Verification Modal)`
* **Pre-conditions:** Issue where webhook re-run is paused or unavailable.
* **Test Steps:**
  1. Click **"Record Manual Verification"**.
  2. Enter verification notes: `"Executed Diagnostic SQL 02 in SSMS; confirmed COUNT(*) = 1 on source_trx_no."`
  3. Select Outcome: `"CleanPassed"`.
  4. Click **"Save Verification"**.
* **Expected Outcome:** Manual verification record is appended. **"Resolve Issue"** button unlocks.
* **Status:** `[ ] PASS  [ ] FAIL` | **Tester:** _______________ | **Date:** _________

#### UAT-D-04: Mark Issue Resolved (Use Case `D-4`)
* **Screen:** `S-02 (Issue Detail)`
* **Pre-conditions:** Verification Gate cleared (`CleanPassed` record exists).
* **Test Steps:**
  1. Click **"Resolve Issue"**.
  2. Confirm resolution in prompt dialog.
* **Expected Outcome:** Issue status transitions to `Resolved`. Resolution timestamp recorded. Issue is removed from active S-01 list.
* **Status:** `[ ] PASS  [ ] FAIL` | **Tester:** _______________ | **Date:** _________

---

### Group E — Detector Management & Zero-Drift Handshake Enrollment

#### UAT-E-01: View Detector Portfolio (Use Case `E-1`)
* **Screen:** `S-03 (Detector List)`
* **Pre-conditions:** Detectors registered in system.
* **Test Steps:**
  1. Click **"Detectors"** in sidebar navigation.
  2. Inspect detector cards/rows (all 6 pilot detectors present).
  3. Verify health badges (`Healthy`, `Silent`, `Failing`), cadence, and last run timestamp.
* **Expected Outcome:** Comprehensive inventory displayed. Health indicators accurately reflect runner state.
* **Status:** `[ ] PASS  [ ] FAIL` | **Tester:** _______________ | **Date:** _________

#### UAT-E-02: Quick Enroll Modal & Plaintext Token Display (`DEC-004`)
* **Screen:** `S-03 (Detector List)` -> `EnrollmentTokenModal`
* **Test Steps:**
  1. On S-03 header, click **"Quick Enroll (Handshake)"** button.
  2. In Step 1, enter label: `"Staging Inpatient Runner"`.
  3. Click **"Generate Enrollment Token"**.
  4. In Step 2, inspect the plaintext token monospace block (`PRM-TOKEN-...`).
  5. Click **"Copy"** button on the environment variable code snippet block.
  6. Close the modal and re-open it.
* **Expected Outcome:** Token secret is displayed clearly once. Toast notification confirms clipboard copy. Sensitive secret state is completely cleared on dialog close.
* **Status:** `[ ] PASS  [ ] FAIL` | **Tester:** _______________ | **Date:** _________

#### UAT-E-03: Pending Tokens Strip & Token Revocation (`DEC-004`)
* **Screen:** `S-03 (Detector List)`
* **Test Steps:**
  1. Locate the **"Pending Enrollment Tokens"** collapsible strip on S-03.
  2. Verify the newly generated token prefix, label, and 24h expiration timestamp are listed.
  3. Click the **"Revoke"** (trash) icon on a test token.
  4. Confirm revocation in the dialog.
* **Expected Outcome:** Token is marked `Revoked` in database (`dbo.DetectorEnrollmentTokens`) and removed from the pending list.
* **Status:** `[ ] PASS  [ ] FAIL` | **Tester:** _______________ | **Date:** _________

#### UAT-E-04: Automated Bootstrap Handshake (`POST /api/detectors/handshake`)
* **Component:** `PrIMA.Detectors` Runner Service
* **Pre-conditions:** A valid unconsumed token exists.
* **Test Steps:**
  1. Configure environment variable `PRIMA_ENROLLMENT_TOKEN="PRM-TOKEN-..."` for `PrIMA.Detectors`.
  2. Launch runner: `dotnet run --project src/backend/PrIMA.Detectors/PrIMA.Detectors.csproj -- --run-once`.
  3. Inspect console logs and local directory for `.prima-credentials.json`.
* **Expected Outcome:** Runner automatically negotiates handshake. Web API provisions unique per-detector API key (`ApiKeyHash`). Credentials cached in local file. Token marked `Consumed`. Subsequent run uses cached key.
* **Status:** `[ ] PASS  [ ] FAIL` | **Tester:** _______________ | **Date:** _________

#### UAT-E-05: Cadence Drift Detection & Non-Blocking Warning (`BR-PRIMA-010`)
* **Screen:** `S-03 (Detector List)` & `S-04 (Detector Detail)`
* **Pre-conditions:** Detector configured with `ExpectedCadence = 300s`.
* **Test Steps:**
  1. Submit telemetry runs with actual interval = 120s (divergence > 50%).
  2. Refresh S-03 and navigate to S-04 for that detector.
* **Expected Outcome:** An amber **Cadence Drift Warning** badge appears (`"Cadence Drift: Expected 300s, actual ~120s"`). Telemetry ingestion is NOT blocked.
* **Status:** `[ ] PASS  [ ] FAIL` | **Tester:** _______________ | **Date:** _________

#### UAT-E-06: Pause / Resume Detector (Use Case `E-5`)
* **Screen:** `S-04 (Detector Detail)`
* **Test Steps:**
  1. Click **"Pause Detector"** button.
  2. Verify status changes to `Paused`.
  3. Wait beyond silence alert threshold (3x cadence); confirm NO silent synthetic alerts are created.
  4. Click **"Resume Detector"**.
* **Expected Outcome:** Status returns to `Active`. Silence alerts remain suppressed while paused.
* **Status:** `[ ] PASS  [ ] FAIL` | **Tester:** _______________ | **Date:** _________

#### UAT-E-07: Trigger Manual Inspection Run (Use Case `E-7`)
* **Screen:** `S-04 (Detector Detail)`
* **Test Steps:**
  1. Click **"Trigger Run Now"** button.
  2. Wait for completion indicator.
* **Expected Outcome:** Inspection runs synchronously; result appears at the top of recent runs list with trigger chip `"Manual"`.
* **Status:** `[ ] PASS  [ ] FAIL` | **Tester:** _______________ | **Date:** _________

---

### Group F — Inspection Run History

#### UAT-F-01: View Run History Log (Use Case `F-1`, `F-2`)
* **Screen:** `S-06 (Inspection Run Log)`
* **Test Steps:**
  1. From S-04, click **"View All Runs"**.
  2. Inspect run list items (timestamp, trigger type, duration, outcome).
  3. Click a run with outcome `FindingDiscovered`.
* **Expected Outcome:** Run details expand to show findings produced, execution duration in milliseconds, and direct link to associated issue on S-02.
* **Status:** `[ ] PASS  [ ] FAIL` | **Tester:** _______________ | **Date:** _________

---

### Group G — Notifications & Escalation Awareness

#### UAT-G-01: SLA Auto-Escalation Alert (Use Case `G-1`, `BR-PRIMA-004`)
* **Screen:** `S-01 (Issue Dashboard)`
* **Test Steps:**
  1. Seed an issue with severity `High` and status `Open` with `CreatedAtUtc = DATEADD(minute, -35, GETUTCDATE())`.
  2. Allow supervisor worker to run its 60-second cycle.
  3. Refresh S-01.
* **Expected Outcome:** Alert strip displays prominent escalation warning. Issue severity auto-escalated to `Critical` with escalation reason noted.
* **Status:** `[ ] PASS  [ ] FAIL` | **Tester:** _______________ | **Date:** _________

#### UAT-G-02: Reopened Issue Detection (Use Case `G-2`, `BR-PRIMA-003`)
* **Screen:** `S-01 (Issue Dashboard)`
* **Test Steps:**
  1. Resolve an existing issue.
  2. Re-insert identical anomaly into `simrs.*` tables.
  3. Execute detector run producing finding with identical correlation key.
* **Expected Outcome:** The existing resolved issue is automatically transitioned to `Reopened` (`BR-PRIMA-003`). It reappears in S-01 highlighted with a `Reopened` indicator chip.
* **Status:** `[ ] PASS  [ ] FAIL` | **Tester:** _______________ | **Date:** _________

---

### Group H — Historical Reporting & Health Summary

#### UAT-H-01: System Health Summary KPI Cards (Use Case `H-1`)
* **Screen:** `S-07 (History & Reports)`
* **Test Steps:**
  1. Click **"History"** in sidebar navigation.
  2. Inspect KPI summary metrics: Pass Rate %, Total Resolved Issues, Mean Time to Remediation (MTTR).
* **Expected Outcome:** KPI tiles calculate correctly based on historical runs and resolved issue timelines. Pure CSS proportion bars render correctly.
* **Status:** `[ ] PASS  [ ] FAIL` | **Tester:** _______________ | **Date:** _________

#### UAT-H-02: Issue Archive & Period-Close Audit (Use Case `H-2`)
* **Screen:** `S-07 (History & Reports)`
* **Test Steps:**
  1. Filter by Status: `"Resolved"` and `"Dismissed"`.
  2. Select date range for current month.
  3. Tap an archived issue row.
* **Expected Outcome:** Read-only view of S-02 opens with complete audit trail (who triaged, what remediation was applied, who verified, and when resolved).
* **Status:** `[ ] PASS  [ ] FAIL` | **Tester:** _______________ | **Date:** _________

---

## 4. Production Hypercare Operational Guide (2-Week Program)

The 2-week hypercare period begins immediately following production Go-Live. During hypercare, EDP officers maintain heightened oversight to tune detector thresholds and guarantee non-interference with hospital operations.

### 4.1 Daily EDP Hypercare Shift Checklist

| Shift Phase | Time Window | Key Operational Checkpoints | Signature |
|---|---|---|---|
| **Morning Standup** | `07:00 - 08:00` | 1. Execute health check: `GET /api/health` (verify DB connected).<br/>2. Review active issues on S-01 (triage critical items before 08:00 clinic opening).<br/>3. Verify print queue spoolers (`PRN-*`) in IGD and Cashier are `Online`.<br/>4. Review overnight 90-day retention pruning logs in SQL Server Agent. | ________ |
| **Midday Peak** | `12:00 - 13:00` | 1. Check `UnprocessedBillingPipelineDetector` for stuck cashier bills during outpatient checkout rush.<br/>2. Inspect S-03 for any detector entering `Silent` or `Failing` health state.<br/>3. Review EMR SatuSehat sync logs for API rate limiting. | ________ |
| **Evening Close** | `18:00 - 19:00` | 1. Perform daily accounting reconciliation check (`journal-balance`).<br/>2. Verify all SLA escalations have assigned owners.<br/>3. Sign off daily hypercare log sheet and hand over to night shift. | ________ |

### 4.2 Threshold Tuning Guidelines

During initial production exposure, real-world hospital traffic patterns may require calibration of detector parameters:

1. **Cadence Drift Tolerance (`±50%`):**
   - If detectors run via cron or Windows Task Scheduler under high CPU load, actual intervals may drift. If drift warnings are frequent but benign, adjust Windows Task Scheduler queue priority before altering detector expected cadence.
2. **Silence Detection Multiplier (`3x Expected Cadence`):**
   - For a 300-second detector, PrIMA flags `Silent` after 900 seconds. If network latency between edge clinics and the server room causes sporadic false silence alerts, adjust probe cadence to `600s` via S-05 form rather than disabling silence supervision.
3. **Database Command Timeout (`5 seconds`):**
   - Enforced by `ReadOnlySimrsConnectionFactory` (`BR-PRIMA-007`). If a probe query approaches 3000ms, do NOT raise the timeout. Instead, review SQL Server index fragmentation on SIMRS tables (`acc_journal_entries`, `ar_receivables`) to maintain the `< 500ms` non-intrusive safety standard.

### 4.3 Rollback & Emergency Recovery Procedures

If an unexpected critical incident occurs during hypercare, execute the appropriate rollback procedure below:

#### Procedure 1: Stop and Isolate Detector Runner
If the detector runner causes unexpected CPU/memory contention on an edge server:
```powershell
# Windows Service
Stop-Service -Name "PrIMADetectorRunner" -Force

# Docker / Linux
docker compose -f deploy/docker-compose.prod.yml stop detectors
```

#### Procedure 2: Web API Maintenance Mode & Revert
If the PrIMA API is unresponsive or requires rollback to previous release:
```powershell
# Stop Web Service
Stop-Service -Name "PrIMAWebService" -Force

# Revert IIS Virtual Directory to previous deployment folder
Set-ItemProperty "IIS:\Sites\Default Web Site\prima" -Name physicalPath -Value "C:\inetpub\prima-previous"
iisreset
```

#### Procedure 3: Database Sandbox / Migration Reversion
PrIMA migrations (`005_Stage5HandshakeAndSimrsSandbox.sql`) are strictly additive. To clean up staging sandbox data without touching core hospital schemas:
```sql
-- Remove sandbox test tables if required
DROP TABLE IF EXISTS simrs.infra_print_queues;
DROP TABLE IF EXISTS simrs.hfis_doctor_schedules;
DROP TABLE IF EXISTS simrs.emr_sync_logs;
DROP TABLE IF EXISTS simrs.trx_patient_bills;
DROP TABLE IF EXISTS simrs.ar_receivables;
DROP TABLE IF EXISTS simrs.acc_journal_entries;
```

---

## 5. UAT & Hypercare Sign-Off Sheet

The signatories below confirm that the PrIMA solution has undergone comprehensive User Acceptance Testing in accordance with this playbook, all test scenarios have passed, and the system is approved for production hypercare operation.

| Role | Name | Title / Organization | Signature | Date |
|---|---|---|---|---|
| **EDP Lead** | ____________________ | Kepala Instalasi SIMRS & EDP | ____________________ | ____________ |
| **Head of IT** | ____________________ | Direktur / Kabid Teknologi Informasi | ____________________ | ____________ |
| **QA / Test Lead** | ____________________ | Quality Assurance Lead | ____________________ | ____________ |
| **Finance Sponsor** | ____________________ | Kepala Bagian Keuangan & Akuntansi | ____________________ | ____________ |

---

*Published by PrIMA Core Engineering Team for Hospital Production Operations.*  
*Maintained under `docs/operations/PLAYBOOK-PRIMA-UAT-001`.*
