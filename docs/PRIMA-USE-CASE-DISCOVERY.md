# PrIMA — Use Case Discovery
**Purpose:** Screen inventory source  
**Actor:** EDP / IT Support Officer (single active user)  
**Principle:** Pragmatic, minimalist — no ceremony, no enterprise overhead

---

## Actor Context

There is **one active human actor**: the **EDP Officer** who monitors, triages, investigates, and resolves issues. The *Automated Inspection Agent* (detector runner) is a system actor — it posts telemetry via API, never through the UI. The *System Analyst / PO* reads the same screens; no separate role-based UI required.

---

## Use Case Groups

### Group A — Dashboard & Issue Monitoring
> The daily operational workspace. The officer opens PrIMA and immediately knows what needs attention.

| # | Use Case | Trigger | Key Action |
|---|---|---|---|
| A-1 | **View Issue Dashboard** | Opens app | See all active issues grouped/sorted by urgency; at-a-glance health of detectors |
| A-2 | **Filter & Search Issues** | Needs to find specific issues | Filter by status, severity, scope, detector, date range; free-text search |
| A-3 | **View Issue Detail** | Clicks an issue | See full context: title, detector, occurrences, telemetry evidence, timeline, current status |

---

### Group B — Issue Triage (Open → Acknowledged → Investigating)
> Taking ownership of a new issue.

| # | Use Case | Trigger | Key Action |
|---|---|---|---|
| B-1 | **Acknowledge Issue** | Officer reviews open issue | Mark as Acknowledged; stops escalation timer |
| B-2 | **Assign Issue** | After acknowledging | Assign to self (or another officer); transitions to Investigating |
| B-3 | **Dismiss Issue** | Determined to be false positive | Select dismissal reason + write rationale; transitions to Dismissed |

> [!NOTE] Acknowledge + Assign can be a single gesture ("Take it") for the common case where the officer assigns to themselves immediately.

---

### Group C — Investigation & Evidence Review
> Understanding what went wrong.

| # | Use Case | Trigger | Key Action |
|---|---|---|---|
| C-1 | **Review Telemetry Evidence** | Investigating an issue | Inspect structured evidence snapshot (table, record keys, debit/credit variance, trace) |
| C-2 | **Review Occurrence History** | Investigating recurrence | See all occurrences with timestamps and evidence per occurrence |
| C-3 | **Add Investigation Note** | Officer documents findings | Free-text note attributed to officer + timestamp |

---

### Group D — Remediation & Verification
> Fixing the problem and confirming it's gone.

| # | Use Case | Trigger | Key Action |
|---|---|---|---|
| D-1 | **Record Remediation Action** | Fix has been executed | Select action category (DataCorrection / ServiceRestart / ConfigAdjustment / CodeRemediation) + description |
| D-2 | **Trigger Verification Re-run** | After remediation | Request detector to re-run; system records verification result automatically |
| D-3 | **Record Manual Verification** | Automated re-run not possible | Officer confirms anomaly gone via manual query; records evidence note |
| D-4 | **Mark Issue Resolved** | Verification passed | Transitions to Resolved (only enabled when CleanPassed verification exists) |

> [!IMPORTANT] Resolve button is only unlocked after a successful VerificationRecord exists. The UI enforces the Verification Gate — no bypass.

---

### Group E — Detector Management
> Managing the monitoring portfolio. Low-frequency, low-ceremony.

| # | Use Case | Trigger | Key Action |
|---|---|---|---|
| E-1 | **View Detector List** | Navigates to Detectors | See all detectors with health status (Healthy / Silent / Failing), last run, cadence |
| E-2 | **View Detector Detail** | Clicks a detector | See config, recent inspection runs, health history |
| E-3 | **Register New Detector** | New detector being deployed | Enter name, description, scope, target module, cadence — saves as Active |
| E-4 | **Edit Detector Config** | Cadence/description needs updating | Update cadence, description, scope — not identity |
| E-5 | **Pause / Resume Detector** | Maintenance window | Toggle Active ↔ Paused; suppresses silence alerts |
| E-6 | **Retire Detector** | Detector permanently decommissioned | Soft-retire; historical data remains |
| E-7 | **Trigger Manual Inspection Run** | On-demand check needed | Officer triggers a run; result appears in inspection run history |

---

### Group F — Inspection Run History
> Operational transparency into what detectors are doing.

| # | Use Case | Trigger | Key Action |
|---|---|---|---|
| F-1 | **View Inspection Run Log** | Officer checks detector activity | List of runs per detector: timestamp, trigger type, outcome, duration |
| F-2 | **View Run Detail** | Clicked a specific run | See full outcome, failure reason (if any), findings produced |

---

### Group G — Notifications & Escalation Awareness
> The system tells the officer what changed.

| # | Use Case | Trigger | Key Action |
|---|---|---|---|
| G-1 | **View Escalation Alerts** | Issue severity auto-escalated | Officer sees which issues escalated and why (SLA breach / recurrence threshold) |
| G-2 | **View Reopened Issues** | Previously resolved issue recurred | Officer sees reopened issues prominently; resumes investigation |

> [!NOTE] Notifications are in-app (badge / alert strip on dashboard). No separate notification management screen needed for v1.

---

### Group H — Historical Reporting & Trends
> Retrospective view for quality evaluation. Read-only, no actions.

| # | Use Case | Trigger | Key Action |
|---|---|---|---|
| H-1 | **View System Health Summary** | Periodic review | Detector pass rate, open/resolved counts, MTTR trend — simple KPI tiles |
| H-2 | **View Issue History** | Audit / period close review | All issues (including Resolved/Dismissed) with full timeline; filterable by scope, detector, date |

---

## Use Cases NOT in Scope (Intentionally Excluded)

| Excluded | Reason |
|---|---|
| User / role management | Single-actor app; no login screen complexity needed in v1 |
| Detector execution scheduling config | Cadence is set on the detector; the runner manages its own schedule externally |
| SLA threshold configuration UI | Sensible defaults; not a knob the officer tweaks daily |
| Automated data patching | Out of product scope by design — PrIMA observes, never auto-fixes |
| Bug tracker / feature requests | Explicitly out of domain |

---

## Candidate Screen Count Summary

| Group | Screens (est.) |
|---|---|
| A — Dashboard & Monitoring | 2 (dashboard, issue detail) |
| B — Triage | Inline on issue detail (no separate screen) |
| C — Investigation | Inline on issue detail |
| D — Remediation & Verification | Inline on issue detail |
| E — Detector Management | 3 (list, detail, register/edit form) |
| F — Inspection Run History | 1 (run log, expandable detail) |
| G — Notifications | Inline on dashboard |
| H — Historical Reporting | 1–2 (summary + history list) |
| **Total** | **~8–10 screens** |

> [!TIP] Most of Group B, C, D are panels/sections within the **Issue Detail** screen — keeping the app screen count lean while covering the full issue lifecycle.
