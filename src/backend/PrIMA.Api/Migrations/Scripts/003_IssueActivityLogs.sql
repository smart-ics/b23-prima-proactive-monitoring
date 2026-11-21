-- Migration: 003_IssueActivityLogs.sql
-- Description: Immutable audit trail for issue lifecycle events, notes, remediations, verifications, and dismissals.

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'IssueActivityLogs' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.IssueActivityLogs (
        Id UNIQUEIDENTIFIER PRIMARY KEY,
        IssueId UNIQUEIDENTIFIER NOT NULL FOREIGN KEY REFERENCES dbo.Issues(Id),
        ActivityType NVARCHAR(50) NOT NULL,
        ActorName NVARCHAR(100) NOT NULL,
        TimestampUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        Summary NVARCHAR(500) NOT NULL,
        DetailsJson NVARCHAR(MAX) NULL
    );

    CREATE INDEX IX_IssueActivityLogs_IssueId_Time 
        ON dbo.IssueActivityLogs(IssueId, TimestampUtc DESC);
END;
