# ISSUE

## Metadata

ID: PRIMA-STAGE-5
Type: CHANGE-REQUEST
Status: OPEN
Title: Implement PrIMA Stage 5 — Detector Handshake Protocol, Pilot SIMRS Detector Suite & Hospital Production Rollout

## Source

Reported By: User
Reported Date: 2026-11-22

## Description

Implement Stage 5 ("Detector Handshake Protocol, Pilot SIMRS Detector Suite & Hospital Production Rollout") of the PrIMA Development Roadmap (`docs/PRIMA-DEVELOPMENT-ROADMAP.md`).

This change request covers delivering the zero-drift Detector Enrollment and Handshake protocol, the standardized Detector Runner SDK/template, an initial production pilot suite of non-intrusive SIMRS Detectors across all four monitoring scopes (`DataIntegrity`, `ProcessIntegrity`, `DataSync`, `Infrastructure`), on-premise security and operational deployment hardening, staging user acceptance testing (UAT), operational SOP documentation, and the transition to live production monitoring with hypercare.

## Desired Outcome

From a business, clinical IT, and operational perspective, completing Stage 5 achieves the "Hospital On-Premise Production Go-Live & Automated Detector Enrollment" milestone:

1. **Zero-Drift Detector Enrollment & Handshake Protocol:**
   - EDP officers can generate short-lived, cryptographically random enrollment tokens from the PrIMA UI (`S-03` / `S-05`) without entering technical detector metadata manually.
   - Detectors perform an automated handshake (`POST /api/detectors/handshake`) on initial boot, submitting runtime manifests (`name`, `targetModule`, `scope`, `expectedCadenceSeconds`, `webhookUrl`, `description`, `version`) to self-register, provision API credentials, and activate in PrIMA without configuration drift.
   - Guardrails enforce unique detector names (`BR-PRIMA-008`), bounded execution cadences (min 30s, max 86400s) to protect silence supervision (`BR-PRIMA-010`), and cadence drift warnings if actual telemetry intervals diverge from registration.

2. **Standardized Reference Detector SDK & Runner Template:**
   - A standardized runner template in `src/tools/detector-runner-template/` providing automatic bootstrap handshake, cached local credentials, structured `TelemetryEvidence` generation, comprehensive execution failure trapping (`BR-PRIMA-005`), and optional webhook endpoints for on-demand inspection runs (`S-04`) and verification re-runs (`S-02` / `D-2`).

3. **Pilot SIMRS Detector Suite (4 Monitoring Scopes):**
   - **Data Integrity:** *Journal Balance Detector* (flags unbalanced debit/credit ledger records) and *Duplicate Receivable Detector* (detects duplicate receivable creation from identical source transactions).
   - **Process Integrity:** *Missing Auto-Journal / Unprocessed Billing Pipeline Detector* (flags completed patient transactions missing expected downstream financial journal records).
   - **Data Synchronization:** *EMR Synchronization Detector* (flags stalled or failed medical record sync jobs) and *SIMRS–HFIS Doctor Schedule Detector* (identifies outpatient schedule discrepancies between local SIMRS and BPJS HFIS).
   - **Infrastructure & Service:** *Remote Print Server Queue Detector* (flags stalled or unresponsive client spooler queues across critical hospital service points).

4. **Non-Intrusive Safety & Deployment Hardening (`BR-PRIMA-007`):**
   - Strictly enforced non-intrusive read-only database connections (dedicated read-only accounts/replicas, mandatory query execution timeouts, zero blocking locks or measurable latency impact on peak SIMRS operations).
   - Hospital on-premise deployment package with TLS/HTTPS reverse-proxy configuration, secure secret storage, health checks, structured logging, database backup and retention routines, and seeded hospital EDP accounts.

5. **Operational Enablement, UAT & Production Hypercare:**
   - Completed staging UAT scenarios with EDP officers, analysts, and product stakeholders validating end-to-end anomaly intake, triage, investigation, verification gates, and historical reporting (`A-1` through `H-2`).
   - Clear EDP Remediation SOP quick-reference documentation mapped to detector action categories and verification procedures.
   - 2-week production hypercare period establishing baseline MTTA/MTTR metrics and tuning detection parameters for pre-close reliability.

## Current Situation

- Stages 1 through 4 are complete: core platform foundation, telemetry ingestion with correlation deduplication, operational workbench (`S-02`) with the mandatory Verification Gate (`BR-PRIMA-004`), detector supervision and webhook re-run capabilities (`S-03`–`S-06`), and retrospective reliability reporting (`S-07`) have been established and tested.
- Detector registration currently requires manual administrative entry in `S-05` before obtaining an API key, introducing risks of configuration drift between detector code and PrIMA server settings.
- The automated handshake and enrollment token endpoints (`POST /api/detectors/enrollment-tokens`, `POST /api/detectors/handshake`) and the corresponding frontend enrollment token UI on `S-03`/`S-05` do not yet exist.
- No reusable edge runner SDK/template exists to standardize detector deployment and lifecycle behavior.
- No production SIMRS detectors are deployed; anomaly detection currently relies on synthetic telemetry simulators rather than live hospital database inspections.
- Non-intrusive query benchmarks and read-only safety constraints (`BR-PRIMA-007`) have not been validated against live SIMRS database workloads.
- Hospital production packaging, reverse-proxy setup, operational runbooks, and EDP remediation SOPs are not yet finalized.

## Evidence

- `docs/PRIMA-DEVELOPMENT-ROADMAP.md` — Section 1 Delivery Strategy (lines 25, 43) and Section 2 Stage 5 Scope & Deliverables (lines 215–264)
- `docs/PrIMA - Product Definition.md` — Section 3 (Scope of Monitoring: Data Integrity, Process Integrity, Data Sync, Infrastructure), Section 8 (Operational Workflow), Section 9 (Non-Functional Requirements & Safety Constraints), Section 10 (Success Criteria)
- `docs/PRIMA-DOMAIN.md` — Business Rule `BR-PRIMA-007` (Non-Interfering Inspection Constraint), `BR-PRIMA-010` (Cadence Drift & Silence Supervision), and all domain entities (`Detectors`, `InspectionRuns`, `Occurrences`, `Remediations`, `Verifications`)
- `docs/PRIMA-USE-CASE-DISCOVERY.md` — End-to-end validation of Use Cases `A-1` through `H-2`, plus Detector Handshake Enrollment
- `docs/PRIMA-SCREEN-INVENTORY.md` — `S-03` (Detector List Enrollment Token Modal), `S-05` (Detector Form Handshake Mode)
- `docs/PRIMA-ARCHITECTURE.md` — Ingestion pipeline, detector lifecycle, authentication models, and security architecture
- `docs/issues/IS001-PRIMA-STAGE-1-ISSUE.md` through `IS004-PRIMA-STAGE-4-ISSUE.md` — Baseline completed stage issue records

## Notes

- **Scope Boundary:** This ISSUE encompasses Stage 5 deliverables: zero-drift handshake enrollment, the reference runner SDK, 6 pilot SIMRS detectors across 4 scopes, production hardening, UAT, and hypercare enablement.
- **Intake Boundary:** In accordance with `ics-issue-creation` and SDLC role separation, this ISSUE artifact documents the formal change request and desired business outcomes without prescribing architecture solutions, implementation details, planning, or testing strategy.
- **Safety Priority:** Execution of pilot detectors must strictly adhere to `BR-PRIMA-007`—zero modifications to SIMRS transactional databases, dedicated read-only credentials, strict query timeouts, and zero locks on production tables.
