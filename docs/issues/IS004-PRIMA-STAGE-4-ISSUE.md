# ISSUE

## Metadata

ID: PRIMA-STAGE-4
Type: CHANGE-REQUEST
Status: OPEN
Title: Implement PrIMA Stage 4 — Historical Reliability Analytics, Reporting & v1.0 Quality Gate

## Source

Reported By: User
Reported Date: 2026-11-21

## Description

Implement Stage 4 ("Historical Reliability Analytics, Reporting & v1.0 Quality Gate") of the PrIMA Development Roadmap (`docs/PRIMA-DEVELOPMENT-ROADMAP.md`).

This change request covers delivering retrospective period-close reporting, operational reliability KPIs, and historical audit exploration (`S-07`), and executing a full-stack quality gate confirming complete adherence to every domain invariant, API contract, and UI/UX guideline before hospital pilot deployment.

## Desired Outcome

From a business and operational perspective, completing Stage 4 delivers the "Feature-Complete PrIMA v1.0 Release Candidate" milestone:

1. **Historical Reliability KPI Reporting (`S-07`, Use Case `H-1`):** EDP officers and management can view period-close operational KPIs for any selected date range including: Total Issues, Resolution Rate %, Average Time to Acknowledge (MTTA), Average Time to Resolve (MTTR), Detector Pass Rate %, and Reopened Issues count, plus Scope and Severity breakdown counts.

2. **Historical Issue Audit Exploration (`S-07`, Use Case `H-2`):** EDP officers can filter and explore the complete historical issue ledger across all statuses (`All`, `Resolved`, `Dismissed`, `Reopened`, and active statuses) with filters for Scope, Detector, Severity, and UTC/WIB date boundaries, routing to read-only `S-02` detail views while preserving `Back to History` origin navigation.

3. **Dashboard-to-History Deep Link (`S-01` → `S-07`):** Clicking the `Resolved Today` summary tile on the Issue Dashboard directly opens `S-07` pre-filtered to `Today + Resolved` for seamless period-close review workflows.

4. **Full-Stack v1.0 Quality Gate:** Complete automated regression verification across all 10 domain business rules (`BR-PRIMA-001` through `BR-PRIMA-010`), API contract compliance (RFC 7807 error handling), UI/UX guideline adherence, and engineering standards (feature-slice isolation, virtualized scrolling, keyboard navigation, responsive drawer, silent network disconnect handling).

## Current Situation

- Stages 1, 2, and 3 are implemented and operational: PrIMA has a running backend with JWT authentication, telemetry ingestion with deduplication/reopening, active issue dashboard (`S-01`), complete single-screen operational resolution loop (`S-02`, `M-01`, `M-02`, `M-03`, activity logs, verification gate), and full detector portfolio management with autonomous supervision (`S-03`–`S-06`, `BR-PRIMA-003`, `BR-PRIMA-005`, `BR-PRIMA-010`, webhook-driven verification `D-2`).
- There are currently no historical analytics capabilities: no KPI aggregation endpoints, no historical issue query API, and no `S-07` History & Reports screen on the frontend.
- The `Resolved Today` summary tile on `S-01` has no deep-link navigation to historical context.
- While individual features from Stages 1–3 have tests, a comprehensive end-to-end automated regression suite verifying all 10 business rules together does not yet exist.
- The UI/UX compliance audit against `PRIMA-UI-UX-GUIDELINE.md §10` (anti-patterns checklist) and engineering standards (virtualized scrolling under 1,000+ rows, keyboard navigation, tablet drawer, silent reconnect indicator) has not been executed.

## Evidence

- `docs/PRIMA-DEVELOPMENT-ROADMAP.md` — Section 1 Delivery Strategy (lines 25, 43) and Section 2 Stage 4 Details (lines 176–212)
- `docs/PRIMA-DOMAIN.md` — All 10 Business Rules `BR-PRIMA-001` through `BR-PRIMA-010` for end-to-end regression verification
- `docs/PRIMA-USE-CASE-DISCOVERY.md` — Use Cases `H-1` (View System Health Summary), `H-2` (View Issue History)
- `docs/PRIMA-SCREEN-INVENTORY.md` — Screen `S-07` (History & Reports)
- `docs/PRIMA-UI-UX-GUIDELINE.md` — Section 5 (`S-07` component hierarchy), Section 10 (AI Agent Implementation Checklist & Anti-Patterns Do Not Do)
- `docs/PRIMA-ARCHITECTURE.md` — History query patterns, CQRS read model for KPIs, and indexing strategy
- `docs/issues/IS001-PRIMA-STAGE-1-ISSUE.md`, `IS002-PRIMA-STAGE-2-ISSUE.md`, `IS003-PRIMA-STAGE-3-ISSUE.md` — Completed Stage 1, 2, and 3 baseline issue artifacts

## Notes

- **Scope Boundary:** This ISSUE covers Stage 4 deliverables only. Real SIMRS pilot detectors, on-premise deployment hardening, UAT, and hospital production rollout belong to Stage 5.
- **Intake Boundary:** In accordance with `ics-issue-creation` and SDLC role separation, this ISSUE artifact documents the formal change request and desired business outcomes without prescribing architecture solutions, implementation details, planning, or testing strategy.
- **Quality Gate Scope:** The v1.0 quality gate in this stage is the final pre-pilot verification — it must pass with zero skipped assertions across all 10 business rules, zero forbidden UI anti-patterns, and zero cross-feature import violations before Stage 5 can begin.