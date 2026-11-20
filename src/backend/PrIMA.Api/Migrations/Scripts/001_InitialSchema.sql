-- ==============================================================================
-- 001_InitialSchema.sql
-- Baseline database schema for PrIMA (Proactive Incident & Anomaly Monitoring)
-- Realizes all structural schema fixes from Target Architecture §8.1 (GAP-003)
-- ==============================================================================

-- Detectors Portfolio
CREATE TABLE dbo.Detectors (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    Name NVARCHAR(150) NOT NULL,
    Description NVARCHAR(500) NOT NULL,
    Scope NVARCHAR(50) NOT NULL,               -- DataIntegrity, ProcessIntegrity, DataSync, Infrastructure
    TargetModule NVARCHAR(100) NOT NULL,
    ExpectedCadenceSeconds INT NOT NULL,
    WebhookUrl NVARCHAR(500) NULL,
    AdminStatus NVARCHAR(30) NOT NULL,         -- Active, Paused, Retired
    HealthStatus NVARCHAR(30) NOT NULL,        -- Healthy, Silent, Failing
    LastRunAtUtc DATETIME2 NULL,
    LastHealthyHeartbeatUtc DATETIME2 NULL,
    CreatedAtUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_Detectors_Name UNIQUE (Name)
);

-- Inspection Run Telemetry Log
CREATE TABLE dbo.InspectionRuns (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    DetectorId UNIQUEIDENTIFIER NOT NULL FOREIGN KEY REFERENCES dbo.Detectors(Id),
    ExecutionTimestampUtc DATETIME2 NOT NULL,
    TriggerSource NVARCHAR(30) NOT NULL,       -- Scheduled, Manual, EventHook
    Outcome NVARCHAR(30) NOT NULL,             -- Passed, FindingDiscovered, ExecutionFailed
    DurationMs INT NOT NULL,
    FailureReason NVARCHAR(1000) NULL,
    CreatedAtUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_InspectionRuns_DetectorId_Time ON dbo.InspectionRuns(DetectorId, ExecutionTimestampUtc DESC);

-- Issues (Central Anomaly Tracking)
CREATE TABLE dbo.Issues (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    CorrelationKey NVARCHAR(250) NOT NULL,     -- Deterministic deduplication key
    Title NVARCHAR(250) NOT NULL,
    DetectorId UNIQUEIDENTIFIER NOT NULL FOREIGN KEY REFERENCES dbo.Detectors(Id),
    DetectorName NVARCHAR(150) NOT NULL,
    Scope NVARCHAR(50) NOT NULL,               -- DataIntegrity, ProcessIntegrity, DataSync, Infrastructure
    BaseSeverity NVARCHAR(20) NOT NULL,        -- Low, Medium, High, Critical
    CurrentSeverity NVARCHAR(20) NOT NULL,     -- Low, Medium, High, Critical
    SeverityRank TINYINT NOT NULL,             -- Critical=4, High=3, Medium=2, Low=1
    Status NVARCHAR(30) NOT NULL,              -- Open, Acknowledged, Investigating, Resolved, Dismissed, Reopened
    EscalationLevel INT NOT NULL DEFAULT 0,
    OccurrenceCount INT NOT NULL DEFAULT 1,
    CurrentAssignee NVARCHAR(100) NULL,
    CreatedAtUtc DATETIME2 NOT NULL,
    LastOccurrenceAtUtc DATETIME2 NOT NULL,
    AcknowledgedAtUtc DATETIME2 NULL,
    ResolvedAtUtc DATETIME2 NULL,
    DismissedAtUtc DATETIME2 NULL,
    ReopenedAtUtc DATETIME2 NULL
);
CREATE UNIQUE INDEX UX_Issues_CorrelationKey ON dbo.Issues(CorrelationKey);
CREATE INDEX IX_Issues_Status_SeverityRank ON dbo.Issues(Status, SeverityRank DESC, LastOccurrenceAtUtc DESC);

-- Occurrences (Immutable observation snapshots)
CREATE TABLE dbo.Occurrences (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    IssueId UNIQUEIDENTIFIER NOT NULL FOREIGN KEY REFERENCES dbo.Issues(Id),
    InspectionRunId UNIQUEIDENTIFIER NOT NULL FOREIGN KEY REFERENCES dbo.InspectionRuns(Id),
    OccurrenceNumber INT NOT NULL,
    TimestampUtc DATETIME2 NOT NULL,
    Module NVARCHAR(100) NOT NULL,
    SourceTable NVARCHAR(100) NULL,
    RecordKeys NVARCHAR(500) NULL,
    DebitTotal DECIMAL(18,2) NULL,
    CreditTotal DECIMAL(18,2) NULL,
    VarianceDelta DECIMAL(18,2) NULL,
    TraceString NVARCHAR(MAX) NULL
);
CREATE INDEX IX_Occurrences_IssueId ON dbo.Occurrences(IssueId, OccurrenceNumber);
CREATE INDEX IX_Occurrences_InspectionRunId ON dbo.Occurrences(InspectionRunId);

-- Investigation Notes (Append-only)
CREATE TABLE dbo.InvestigationNotes (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    IssueId UNIQUEIDENTIFIER NOT NULL FOREIGN KEY REFERENCES dbo.Issues(Id),
    OfficerName NVARCHAR(100) NOT NULL,
    NoteText NVARCHAR(MAX) NOT NULL,
    CreatedAtUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

-- Remediation Records
CREATE TABLE dbo.RemediationRecords (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    IssueId UNIQUEIDENTIFIER NOT NULL FOREIGN KEY REFERENCES dbo.Issues(Id),
    ActionCategory NVARCHAR(50) NOT NULL,      -- DataCorrection, ServiceRestart, ConfigurationAdjustment, CodeRemediationRequested
    SummaryDescription NVARCHAR(MAX) NOT NULL,
    PerformedBy NVARCHAR(100) NOT NULL,
    TimestampUtc DATETIME2 NOT NULL,
    IsSuperseded BIT NOT NULL DEFAULT 0
);
CREATE INDEX IX_RemediationRecords_IssueId ON dbo.RemediationRecords(IssueId);

-- Verification Records
CREATE TABLE dbo.VerificationRecords (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    IssueId UNIQUEIDENTIFIER NOT NULL FOREIGN KEY REFERENCES dbo.Issues(Id),
    VerificationMethod NVARCHAR(50) NOT NULL,  -- AutomatedReRun, ManualQueryConfirmed
    CleanPassed BIT NOT NULL,
    EvidenceNotes NVARCHAR(MAX) NOT NULL,
    VerifiedBy NVARCHAR(100) NOT NULL,
    TimestampUtc DATETIME2 NOT NULL,
    IsSuperseded BIT NOT NULL DEFAULT 0
);
CREATE INDEX IX_VerificationRecords_IssueId ON dbo.VerificationRecords(IssueId);

-- Dismissal Records
CREATE TABLE dbo.DismissalRecords (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    IssueId UNIQUEIDENTIFIER NOT NULL FOREIGN KEY REFERENCES dbo.Issues(Id),
    DismissalReason NVARCHAR(50) NOT NULL,     -- FalsePositive, AcceptedOperationalException, DuplicateManualReport
    JustificationRationale NVARCHAR(MAX) NOT NULL,
    DismissedBy NVARCHAR(100) NOT NULL,
    TimestampUtc DATETIME2 NOT NULL,
    IsSuperseded BIT NOT NULL DEFAULT 0
);
CREATE INDEX IX_DismissalRecords_IssueId ON dbo.DismissalRecords(IssueId);

-- Seeded EDP Officer Accounts
CREATE TABLE dbo.Officers (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    Username NVARCHAR(50) NOT NULL UNIQUE,
    FullName NVARCHAR(100) NOT NULL,
    PasswordHash NVARCHAR(255) NOT NULL,
    Role NVARCHAR(30) NOT NULL,                -- EdpOfficer, SystemAnalyst, Lead
    IsActive BIT NOT NULL DEFAULT 1
);
