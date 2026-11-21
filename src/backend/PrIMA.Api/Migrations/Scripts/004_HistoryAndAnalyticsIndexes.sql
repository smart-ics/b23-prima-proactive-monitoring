-- ==============================================================================
-- Migration: 004_HistoryAndAnalyticsIndexes.sql
-- Purpose: Optimize historical reliability analytics and ledger filtering for Stage 4
-- Realizes covering and filtered indexes from IS004 Architecture §8.1 (DEC-006)
-- ==============================================================================

-- 1. Covering index for period-close detector pass rate queries
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_InspectionRuns_ExecutionTimestampUtc' AND object_id = OBJECT_ID('dbo.InspectionRuns'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_InspectionRuns_ExecutionTimestampUtc
    ON dbo.InspectionRuns (ExecutionTimestampUtc DESC)
    INCLUDE (Outcome);
END;

-- 2. Covering index for issue creation volume and scope/severity breakdown
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Issues_CreatedAtUtc' AND object_id = OBJECT_ID('dbo.Issues'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Issues_CreatedAtUtc
    ON dbo.Issues (CreatedAtUtc DESC)
    INCLUDE (Scope, CurrentSeverity, BaseSeverity, Status);
END;

-- 3. Filtered index for Resolved issues (MTTR & Resolution Rate queries)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Issues_ResolvedAtUtc' AND object_id = OBJECT_ID('dbo.Issues'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Issues_ResolvedAtUtc
    ON dbo.Issues (ResolvedAtUtc DESC)
    INCLUDE (CreatedAtUtc, ReopenedAtUtc, CurrentSeverity, Scope, DetectorId, Status)
    WHERE ResolvedAtUtc IS NOT NULL;
END;

-- 4. Filtered index for Acknowledged issues (MTTA queries)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Issues_AcknowledgedAtUtc' AND object_id = OBJECT_ID('dbo.Issues'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Issues_AcknowledgedAtUtc
    ON dbo.Issues (AcknowledgedAtUtc DESC)
    INCLUDE (CreatedAtUtc)
    WHERE AcknowledgedAtUtc IS NOT NULL;
END;

-- 5. Filtered index for Reopened issues
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Issues_ReopenedAtUtc' AND object_id = OBJECT_ID('dbo.Issues'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Issues_ReopenedAtUtc
    ON dbo.Issues (ReopenedAtUtc DESC)
    INCLUDE (CreatedAtUtc, Status)
    WHERE ReopenedAtUtc IS NOT NULL;
END;

-- 6. Filtered index for Dismissed issues (Resolution Rate denominator)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Issues_DismissedAtUtc' AND object_id = OBJECT_ID('dbo.Issues'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Issues_DismissedAtUtc
    ON dbo.Issues (DismissedAtUtc DESC)
    INCLUDE (CreatedAtUtc, Status)
    WHERE DismissedAtUtc IS NOT NULL;
END;
