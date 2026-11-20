# PrIMA — Screen Inventory
**Product:** PrIMA (Proactive Inspection Monitoring App)  
**Actor:** EDP / IT Support Officer (single active user)  
**Principle:** Pragmatic · Minimalist · Low cognitive load  
**Source:** [PrIMA Use Case Discovery](./PrIMA-Use-Cases.md)  
**Companion Docs:** [PrIMA UI/UX Guideline](./PRIMA-UI-UX-GUIDELINE.md) · [PrIMA Architecture](./PRIMA-ARCHITECTURE.md)

---

## Navigation Structure

```
Auth Shell (AuthLayout)
└── Officer Login              → S-00  (unauthenticated entry → redirects to S-01)

App Shell (AppLayout — Desktop Left Sidebar, collapsible drawer on tablet)
├── [Left Sidebar]
│   ├── 🏠  Dashboard          → S-01
│   ├── 🔍  Detectors          → S-03
│   └── 📊  History            → S-07
│
├── Issue Detail               → S-02  (pushed from S-01, S-07)
├── Detector Detail            → S-04  (pushed from S-03)
├── Detector Form              → S-05  (pushed from S-03, S-04)
├── Inspection Run Log         → S-06  (pushed from S-04)
│
└── Modals / Overlays
    ├── M-01  Dismiss Issue
    ├── M-02  Record Remediation
    └── M-03  Record Manual Verification
```

> **8 full screens (including S-00 Login) + 3 modals.** The issue lifecycle (triage → investigation → remediation → verification → resolve) lives entirely within S-02 (Issue Detail). No multi-step wizards.

---

## Screen Index

| ID | Screen | Primary Use Cases |
|---|---|---|
| S-00 | Officer Login | Authentication & Officer Attribution |
| S-01 | Issue Dashboard | A-1, A-2, G-1, G-2 |
| S-02 | Issue Detail | A-3, B-1, B-2, B-3, C-1, C-2, C-3, D-1, D-2, D-3, D-4 |
| S-03 | Detector List | E-1 |
| S-04 | Detector Detail | E-2, E-5, E-6, E-7 |
| S-05 | Detector Form | E-3, E-4 |
| S-06 | Inspection Run Log | F-1, F-2 |
| S-07 | History & Reports | H-1, H-2 |
| M-01 | Dismiss Issue (Modal) | B-3 |
| M-02 | Record Remediation (Modal) | D-1 |
| M-03 | Manual Verification (Modal) | D-3 |

---

## S-00 — Officer Login

**Purpose:** Lightweight authentication entry so every acknowledgment, note, remediation, verification, and dismissal is deterministically attributed to an identified EDP officer (`JWT Bearer`).

### Fields
| Field | Type | Required |
|---|---|---|
| Username | Text | ✅ |
| Password | Password (masked) | ✅ |

### Actions
- `Sign In` → authenticates via `/api/auth/login` → stores officer session in `AppLayout` sidebar footer → navigates to `S-01 Issue Dashboard`

---

## S-01 — Issue Dashboard

**Purpose:** The officer's daily home screen. Immediate situational awareness — what needs attention right now.

### Layout Sections

#### 1. Alert Strip *(conditional, top)*
- Shown only when escalated or reopened issues exist
- E.g. `⚠ 2 issues escalated to Critical · 1 issue reopened`
- Tapping navigates to those filtered issues

#### 2. Summary Bar
| Chip | Value |
|---|---|
| 🔴 Critical | count |
| 🟠 High | count |
| 🟡 Open | count |
| 🔁 Reopened | count |
| ✅ Resolved Today | count |

#### 3. Filter Bar
- Status chips: `All · Open · Acknowledged · Investigating · Reopened`
- Scope chips: `All · Data Integrity · Process · Sync · Infrastructure`
- Search field (free-text, matches title / detector name)
- Active filters shown as removable tags

#### 4. Issue Feed *(scrollable list)*
Each row shows:
- **Severity badge** (color dot: Critical / High / Medium / Low)
- **Issue title** (truncated to 2 lines)
- **Status chip** (Open / Acknowledged / Investigating / Reopened)
- **Detector name** · **Scope tag**
- **Occurrence count** (e.g. `×4`) · **Last seen** (relative time, e.g. `3h ago`)
- Escalated indicator `↑` if dynamically escalated

Default sort: severity desc → last occurrence desc.

### Actions
| Action | Result |
|---|---|
| Tap issue row | → S-02 Issue Detail |
| Tap Alert Strip | → S-01 filtered to escalated/reopened |
| Tap Summary chip | → S-01 filtered to that status |

### UX Notes
- No pagination; use virtual scroll. Officer should not miss issues.
- Resolved / Dismissed issues are **not shown here** — they live in S-07.
- Detector health problems appear as issues (scope: Infrastructure) — no separate "detector health" section needed.

---

## S-02 — Issue Detail

**Purpose:** The full operational workspace for a single issue. Covers triage, investigation evidence, remediation recording, and verification — all in one scrollable screen. No separate screens per lifecycle step.

### Layout Sections

#### 1. Header *(sticky)*
- Issue title
- Status chip (color-coded) · Severity badge
- Detector name · Scope tag · Module
- `Occurrence #N` counter
- Context action button (primary action for current status — see Action Bar below)

#### 2. Action Bar *(context-sensitive — changes with lifecycle status)*

| Current Status | Primary Button | Secondary Options |
|---|---|---|
| **Open** | `Take It` (Acknowledge + Assign to self) | `Assign to…` · `Dismiss` |
| **Acknowledged** | `Start Investigating` (self-assign → Investigating) | `Reassign` · `Dismiss` |
| **Investigating** | `Record Remediation` | `Add Note` · `Dismiss` |
| **Investigating** *(after remediation recorded)* | `Trigger Verification Re-run` | `Manual Verification` · `Dismiss` |
| **Investigating** *(after CleanPassed verification)* | `Mark Resolved` ✅ | — |
| **Investigating** *(verification failed)* | `Record Remediation` *(retry)* | `Add Note` · `Dismiss` |
| **Reopened** | `Take It` | `Assign to…` · `Dismiss` |
| **Resolved** | *(read-only — no actions)* | — |
| **Dismissed** | *(read-only — no actions)* | — |

> `Take It` = single tap → Acknowledge + Assign to current officer + transition to Investigating.  
> `Mark Resolved` is **only enabled** when a `CleanPassed` VerificationRecord exists.

#### 3. Occurrences & Timeline *(collapsible)*
- Chronological list of all occurrences
- Each row: Occurrence # · Timestamp · Trigger type · "View Evidence" expand
- Expanded evidence shows: Module · Source Table · Record Keys · Debit / Credit / Variance Delta · Root Cause Trace

#### 4. Latest Telemetry Evidence *(always visible)*
- Structured key-value display of the most recent occurrence's evidence
- Module · Table · Record Keys · Debit / Credit / Variance · Trace string

#### 5. Investigation Notes *(chronological, append-only)*
- List of notes with officer name + timestamp
- `+ Add Note` text input at bottom (free-text, submit on save)

#### 6. Remediation Record *(shown if recorded)*
- Action Category chip · Description · Officer · Timestamp
- If multiple attempts exist, show as a list

#### 7. Verification Record *(shown if exists)*
- Method chip (AutomatedReRun / ManualQuery) · Result chip (CleanPassed 🟢 / AnomalyStillPresent 🔴)
- Officer / System · Timestamp · Evidence note

#### 8. Dismissal Record *(shown if Dismissed)*
- Reason chip · Rationale text · Officer · Timestamp

#### 9. Activity Log *(collapsible, bottom)*
- Immutable audit trail: every status transition, assignment change, escalation event with actor + timestamp

### Navigation
- Back → S-01 (or S-07 if entered from history)
- Detector name (tappable) → S-04 Detector Detail

---

## M-01 — Dismiss Issue *(Modal)*

**Purpose:** Collect mandatory dismissal record before transitioning issue to Dismissed.

### Fields
| Field | Type | Required |
|---|---|---|
| Dismissal Reason | Select: `False Positive · Accepted Operational Exception · Duplicate Manual Report` | ✅ |
| Justification Rationale | Text area (min 20 chars) | ✅ |

### Actions
- `Confirm Dismiss` — validates rationale non-empty → dismisses issue → closes modal → refreshes S-02
- `Cancel`

---

## M-02 — Record Remediation *(Modal)*

**Purpose:** Capture what corrective action was taken before initiating verification.

### Fields
| Field | Type | Required |
|---|---|---|
| Action Category | Select: `Data Correction · Service Restart · Configuration Adjustment · Code Remediation Requested` | ✅ |
| Description | Text area | ✅ |

### Actions
- `Save Remediation` → records RemediationRecord → closes modal → S-02 action bar updates to show verification options
- `Cancel`

---

## M-03 — Manual Verification *(Modal)*

**Purpose:** Officer manually confirms the anomaly is gone when automated re-run is not available.

### Fields
| Field | Type | Required |
|---|---|---|
| Verification Evidence / Notes | Text area (describe the query or check performed) | ✅ |
| Result | Radio: `Anomaly Gone (Clean) · Anomaly Still Present` | ✅ |

### Actions
- `Submit Verification` → records VerificationRecord → closes modal → S-02 reflects result
  - If Clean: `Mark Resolved` button unlocks
  - If Still Present: status remains Investigating, remediation can be retried
- `Cancel`

---

## S-03 — Detector List

**Purpose:** Overview of the full detector portfolio and health status.

### Layout Sections

#### 1. Health Summary Bar
| Chip | Value |
|---|---|
| 🟢 Healthy | count |
| 🔴 Failing | count |
| 🟡 Silent | count |
| ⏸ Paused | count |

#### 2. Filter Bar
- Status chips: `All · Active · Paused · Retired`
- Health chips: `All · Healthy · Silent · Failing`
- Scope chips: `All · Data Integrity · Process · Sync · Infrastructure`

#### 3. Detector List *(scrollable)*
Each row:
- **Health status dot** (green / red / yellow / grey)
- **Detector name**
- **Scope tag** · **Target Module**
- **Admin status** chip (Active / Paused / Retired)
- **Last run** (relative time) · **Cadence** (e.g. `every 30 min`)

### Actions
| Action | Result |
|---|---|
| Tap row | → S-04 Detector Detail |
| `+ Register Detector` FAB | → S-05 Detector Form (create mode) |

---

## S-04 — Detector Detail

**Purpose:** Full context for a single detector — config, health, recent runs, and open issues.

### Layout Sections

#### 1. Header *(sticky)*
- Detector name
- Health status badge (Healthy / Failing / Silent) · Admin status chip (Active / Paused / Retired)
- Scope · Target Module · Cadence

#### 2. Action Bar
| Admin Status | Available Actions |
|---|---|
| **Active** | `Trigger Run` · `Pause` · `Edit` · `Retire` |
| **Paused** | `Resume` · `Edit` · `Retire` |
| **Retired** | *(read-only)* |

> `Retire` requires a confirmation prompt (destructive, irreversible).

#### 3. Stats Row
- Last Successful Run · Last Failure · Total Runs (last 30d) · Pass Rate %

#### 4. Recent Inspection Runs *(last 10, with "View All" link)*
Each row:
- Outcome dot (Passed 🟢 / Failed 🔴 / Finding 🟡)
- Trigger type chip (Scheduled / Manual / EventHook)
- Timestamp · Duration
- Tap to expand: failure reason or finding summary

#### 5. Active Issues *(linked to this detector)*
- Short list of open/investigating issues tied to this detector
- Each row tappable → S-02 Issue Detail

### Navigation
- `View All Runs` → S-06 Inspection Run Log (filtered to this detector)
- `Edit` → S-05 Detector Form (edit mode)
- Back → S-03

---

## S-05 — Detector Form *(Register / Edit)*

**Purpose:** Register a new detector or edit an existing one's configuration.

### Fields
| Field | Type | Required | Notes |
|---|---|---|---|
| Name | Text | ✅ | Unique identifier label |
| Description | Text area | ✅ | What this detector checks |
| Monitoring Scope | Select: `Data Integrity · Process Integrity · Data Synchronization · Infrastructure & Service` | ✅ | |
| Target Module | Text | ✅ | e.g. `Accounting`, `EMR`, `Billing` |
| Expected Cadence | Duration picker (minutes / hours) | ✅ | Used for silence detection threshold |

> **Edit mode:** Name is read-only (identity cannot change). All other fields editable.  
> **Register mode:** Admin Status defaults to `Active` on save — no extra step.

### Actions
- `Save` → validates → saves → back to S-04 (edit) or S-03 (register)
- `Cancel` → discard → back

---

## S-06 — Inspection Run Log

**Purpose:** Full audit log of all inspection runs for a detector. Read-only operational transparency.

### Layout Sections

#### 1. Context Header
- Detector name · Health status · Current cadence
- Date range picker (default: last 7 days)

#### 2. Filter Bar
- Outcome: `All · Passed · Finding Discovered · Failed`
- Trigger: `All · Scheduled · Manual · EventHook`

#### 3. Run List *(scrollable)*
Each row:
- Outcome dot · Timestamp · Trigger type chip · Duration
- If Finding: `Finding →` tappable link to associated Issue in S-02
- If Failed: short failure reason inline

#### 4. Expandable Run Detail *(tap row to expand)*
- Full outcome details
- Failure reason (stack trace summary) if ExecutionFailed
- Finding count + CorrelationKey if FindingDiscovered

### Navigation
- Back → S-04 Detector Detail

---

## S-07 — History & Reports

**Purpose:** Retrospective view for period-close reviews, trend analysis, and audit. Read-only.

### Layout Sections

#### 1. Date Range Picker
- Presets: `Today · This Week · This Month · Last Month · Custom`

#### 2. KPI Tiles *(summary for selected period)*
| Tile | Metric |
|---|---|
| Total Issues | count (new in period) |
| Resolution Rate | % resolved of closed |
| Avg. Time to Acknowledge | hours |
| Avg. Time to Resolve | hours |
| Detector Pass Rate | % of runs that passed |
| Reopened Issues | count |

#### 3. Scope Breakdown *(simple count table)*
- Issues by scope: Data Integrity / Process / Sync / Infrastructure
- Issues by severity: Critical / High / Medium / Low

#### 4. Issue History List *(all statuses, filterable)*
- Filter: Status `All · Resolved · Dismissed · Reopened` · Scope · Detector · Severity
- Each row: Status chip · Title · Detector · Severity · Created · Resolved/Dismissed date
- Tap → S-02 Issue Detail (read-only for Resolved/Dismissed)

---

## UX Constraints & Design Decisions

| Decision | Rationale |
|---|---|
| `Take It` combines Acknowledge + Assign to self | 90% case — reduces taps from 3 to 1 |
| `Mark Resolved` button disabled until CleanPassed verification exists | Enforces BR-PRIMA-004 without an error message |
| Dismiss, Remediation, Manual Verification are modals (not screens) | Low-frequency, focused actions; keeps navigation shallow |
| Resolved/Dismissed issues excluded from S-01 | Dashboard is an action queue — archive doesn't belong here |
| Occurrence evidence is expandable inline (not a separate screen) | Evidence review is part of investigation, not a navigation event |
| Activity Log collapsed by default on S-02 | Reduces visual noise; available when needed for audit |
| No pagination on issue/run feeds | Virtual scroll; officer must not miss active issues |
| Detector health problems surface as Issues | No separate "detector health" section — unified issue lifecycle |
| No configuration screen for SLA thresholds or escalation rules | Sensible defaults; not an officer-managed concern |

---

## Out-of-Scope Screens (v1)

| Screen | Reason Excluded |
|---|---|
| User & Role Management (Admin CRUD) | Officers are seeded via DbUp migration (`002_SeedInitialUsers.sql`); only minimal S-00 Login is included for officer attribution |
| Settings / SLA config | Defaults only in v1; engineering-managed |
| Notification preferences | In-app alert strip is sufficient for v1 |
| Detector scheduling editor | Managed externally by the runner; cadence field on form is enough |
| Analytics deep-dive / detector comparison | H-1/H-2 KPIs sufficient; advanced analytics is v2 |
