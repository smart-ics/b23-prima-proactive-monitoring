# ISSUE

## Metadata

ID: PRIMA-STAGE-3
Type: CHANGE-REQUEST
Status: OPEN
Title: Implement PrIMA Stage 3 — Detector Portfolio, Automated Supervision & Webhook Verification

## Source

Reported By: User
Reported Date: 2026-11-21

## Description

Implement Stage 3 ("Detector Portfolio, Automated Supervision & Webhook Verification") of the PrIMA Development Roadmap (`docs/PRIMA-DEVELOPMENT-ROADMAP.md`).

This change request covers enabling full administrative lifecycle management of the Detector portfolio, activating autonomous background supervision for detector silence, execution failures, and dynamic severity escalation, and connecting on-demand detector webhooks so PrIMA proactively detects monitoring blind spots and automates post-remediation verification.

## Desired Outcome

From a business and operational perspective, completing Stage 3 delivers the "Autonomous Proactive Supervision & Self-Monitoring" milestone:

1. **Detector Portfolio Management (`S-03`, Use Case `E-1`):** EDP officers can view and search the full detector portfolio with health summary metrics (`Healthy`, `Failing`, `Silent`, `Paused`), filter by admin status, health, and monitoring scope, register new detectors, and receive visual indicator alerts in the navigation sidebar when any detector requires attention.
2. **Detector Detail & Execution History (`S-04`, `S-06`, Use Cases `E-2`, `F-1`, `F-2`):** EDP officers can inspect individual detector configuration, 30-day reliability and run statistics (last successful run, last failure, total runs, pass rate percentage), recent inspection runs with detailed outcome and failure reason inspection, linked active issues, and full historical inspection run logs filterable by date range, outcome, and trigger source.
3. **Detector Configuration & Lifecycle Control (`S-05`, Use Cases `E-3`, `E-4`, `E-5`, `E-6`):** Officers can register new detectors, edit existing configuration (preserving immutable identity), pause/resume active monitoring (suppressing silence checks while paused), and irreversibly retire decommissioned detectors with confirmation while preserving historical records.
4. **On-Demand Manual Detector Execution (`S-04`, Use Case `E-7`):** Officers can manually trigger a detector execution on demand via webhook and inspect the resulting inspection run and telemetry outcome.
5. **Autonomous Silence Detection (`BR-PRIMA-010`, `BR-PRIMA-005`):** The system continuously and autonomously detects when an active detector exceeds its expected cadence threshold (`> 2x ExpectedCadenceSeconds`), automatically marks the detector as `Silent`, and synthesizes an `Infrastructure` issue (`SILENT:{DetectorId}`) to prevent silent monitoring failures.
6. **Execution Failure Anomaly Synthesis (`BR-PRIMA-005`):** When telemetry indicates an execution failure (`outcome == "ExecutionFailed"`), the system records the failed inspection run, updates the detector health status to `Failing`, and automatically synthesizes or appends to an `Infrastructure` issue (`FAIL:{DetectorId}`) so EDP is immediately alerted to detector crashes.
7. **Dynamic Severity Escalation (`BR-PRIMA-003`, Use Cases `G-1`, `G-2`):** The system continuously monitors active issues and autonomously elevates severity and increments escalation levels when an open issue remains unacknowledged past its SLA threshold or exceeds recurrence thresholds (`>= 5` occurrences), recording escalation events in the activity log and surfacing visual alert strips and badges on the issue dashboard (`S-01`).
8. **Webhook-Driven Verification Re-run (`S-02`, Use Case `D-2`):** In the Issue Detail Workbench, officers can trigger automated verification re-runs via detector webhooks following remediation, recording an automated verification result (`CleanPassed` or `AnomalyStillPresent`), while gracefully falling back to manual verification guidance when no webhook is configured.

## Current Situation

- Stages 1 and 2 are implemented and operational: PrIMA has a running backend with JWT authentication, telemetry ingestion with deduplication/reopening, active issue dashboard (`S-01`), and complete single-screen operational resolution loop (`S-02`, `M-01`, `M-02`, `M-03`, activity logs, verification gate).
- Detectors currently exist only as initial seeded database rows; there are no UI screens or API endpoints for managing the detector portfolio (`S-03`), viewing detector details and inspection runs (`S-04`, `S-06`), registering/editing detectors (`S-05`), or pausing/retiring detectors.
- The platform currently has no autonomous background supervision: detectors that crash or stop sending heartbeats silently stop monitoring without alerting EDP officers, and unacknowledged or frequently recurring issues do not dynamically escalate.
- Webhook invocation for triggering immediate runs (`E-7`) or automated post-remediation verification (`D-2`) is not yet implemented in the backend or connected on `S-02`.

## Evidence

- `docs/PRIMA-DEVELOPMENT-ROADMAP.md` — Section 1 Delivery Strategy (lines 23, 41) and Section 2 Stage 3 Details (lines 130–174)
- `docs/PRIMA-DOMAIN.md` — Detector lifecycle states, capabilities, and Business Rules `BR-PRIMA-003` (Dynamic Severity Escalation), `BR-PRIMA-005` (Unified Treatment of Detector Health Anomalies), and `BR-PRIMA-010` (Detector Silence Detection Cadence)
- `docs/PRIMA-USE-CASE-DISCOVERY.md` — Use Cases `D-2` (Trigger Verification Re-run), `E-1` to `E-7` (Detector portfolio, detail, register, edit, pause/resume, retire, trigger run), `F-1` to `F-2` (Inspection run log and details), and `G-1` to `G-2` (Escalation and reopened awareness)
- `docs/PRIMA-SCREEN-INVENTORY.md` — Screens `S-03` (Detector List), `S-04` (Detector Detail), `S-05` (Detector Form), and `S-06` (Inspection Run Log)
- `docs/PRIMA-UI-UX-GUIDELINE.md` — Section 4 (Component hierarchies for `S-03`–`S-06`), Section 6 (Navigation bar alert indicators), and Section 7 (Interactive state machine upgrades for `S-01` and `S-02`)
- `docs/PRIMA-ARCHITECTURE.md` — Detector aggregate model, background supervision architecture, and webhook integration patterns
- `docs/issues/IS001-PRIMA-STAGE-1-ISSUE.md` & `docs/issues/IS002-PRIMA-STAGE-2-ISSUE.md` — Completed Stage 1 and Stage 2 baseline issue artifacts

## Notes

- **Scope Boundary:** This ISSUE covers Stage 3 deliverables only. Retrospective historical analytics, period-close KPI reporting (`S-07`), and the v1.0 quality gate belong to Stage 4. Real SIMRS pilot detectors and hospital on-premise rollout belong to Stage 5.
- **Intake Boundary:** In accordance with `ics-issue-creation` and SDLC role separation, this ISSUE artifact documents the formal change request and desired business outcomes without prescribing architecture solutions, implementation details, planning, or testing strategy.
