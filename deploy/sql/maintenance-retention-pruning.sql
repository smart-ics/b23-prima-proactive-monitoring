-- ==============================================================================
-- PrIMA Proactive Monitoring - Automated 90-Day Retention Pruning Agent Job
-- Realizes DEC-009 & BR-PRIMA-009 (Storage Hygiene & Diagnostic Preservation)
--
-- PURPOSE:
--   Configures a native SQL Server Agent Job to execute daily off-peak batch
--   pruning of clean 'Passed' inspection runs older than 90 days.
--
-- BUSINESS RULES:
--   - Batch size 1,000 prevents table-level lock escalation and transaction log spikes.
--   - Strictly exempts and permanently preserves all runs linked to dbo.Occurrences,
--     as well as all 'FindingDiscovered' and 'ExecutionFailed' runs.
-- ==============================================================================

USE msdb;
GO

SET NOCOUNT ON;

DECLARE @JobName NVARCHAR(128) = N'PrIMA_Daily_Retention_Pruning';
DECLARE @DatabaseName NVARCHAR(128) = N'PrIMA';
DECLARE @JobId BINARY(16);

PRINT '====================================================================';
PRINT ' Configuring SQL Server Agent Job: ' + @JobName;
PRINT '====================================================================';

-- ------------------------------------------------------------------------------
-- 1. Ensure Job Category Exists
-- ------------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM msdb.dbo.syscategories WHERE name = N'Database Maintenance' AND category_class = 1)
BEGIN
    EXEC msdb.dbo.sp_add_category 
        @class = N'JOB', 
        @type = N'LOCAL', 
        @name = N'Database Maintenance';
END;

-- ------------------------------------------------------------------------------
-- 2. Remove Existing Job If Already Present (Idempotent Recreation)
-- ------------------------------------------------------------------------------
SELECT @JobId = job_id FROM msdb.dbo.sysjobs WHERE name = @JobName;
IF (@JobId IS NOT NULL)
BEGIN
    PRINT 'Existing job found. Removing prior job definition...';
    EXEC msdb.dbo.sp_delete_job @job_id = @JobId, @delete_unused_schedule = 1;
END;

-- ------------------------------------------------------------------------------
-- 3. Create the Maintenance Job
-- ------------------------------------------------------------------------------
PRINT 'Creating job [' + @JobName + ']...';

EXEC msdb.dbo.sp_add_job 
    @job_name = @JobName,
    @enabled = 1,
    @notify_level_eventlog = 2, -- Log on failure to Windows Event Log
    @notify_level_email = 0,
    @notify_level_netsend = 0,
    @notify_level_page = 0,
    @delete_level = 0,
    @description = N'Daily automated batch pruning of clean Passed inspection runs older than 90 days (DEC-009, BR-PRIMA-009).',
    @category_name = N'Database Maintenance',
    @owner_login_name = N'sa',
    @job_id = @JobId OUTPUT;

-- ------------------------------------------------------------------------------
-- 4. Add Step 1: Chunked Batch Pruning of Clean Passed Runs
-- ------------------------------------------------------------------------------
PRINT 'Adding batch retention pruning step...';

DECLARE @CommandSql NVARCHAR(MAX) = N'
SET NOCOUNT ON;

DECLARE @RetentionDays INT = 90;
DECLARE @CutoffUtc DATETIME2 = DATEADD(DAY, -@RetentionDays, SYSUTCDATETIME());
DECLARE @DeletedCount INT = 0;
DECLARE @BatchCount INT = 1;

PRINT ''PrIMA Retention Pruning started at: '' + CONVERT(VARCHAR(30), SYSUTCDATETIME(), 121);
PRINT ''Pruning clean Passed runs older than: '' + CONVERT(VARCHAR(30), @CutoffUtc, 121);

WHILE (@BatchCount > 0)
BEGIN
    DELETE TOP (1000) ir
    FROM dbo.InspectionRuns ir
    WHERE ir.Outcome = ''Passed''
      AND ir.ExecutionTimestampUtc < @CutoffUtc
      AND NOT EXISTS (
          SELECT 1 FROM dbo.Occurrences o WHERE o.InspectionRunId = ir.Id
      );

    SET @BatchCount = @@ROWCOUNT;
    SET @DeletedCount = @DeletedCount + @BatchCount;

    -- 1-second pause between batches to allow transaction log flushes and prevent lock contention
    IF (@BatchCount > 0)
    BEGIN
        WAITFOR DELAY ''00:00:01'';
    END;
END;

PRINT ''Retention pruning completed successfully.'';
PRINT ''Total clean passed runs deleted: '' + CAST(@DeletedCount AS VARCHAR(20));
';

EXEC msdb.dbo.sp_add_jobstep 
    @job_id = @JobId, 
    @step_name = N'Prune_Passed_Runs_Older_Than_90_Days',
    @step_id = 1,
    @cmdexec_success_code = 0,
    @on_success_action = 1, -- Quit with success
    @on_fail_action = 2,    -- Quit with failure
    @retry_attempts = 2,
    @retry_interval = 5,    -- 5 minutes retry
    @os_run_priority = 0,
    @subsystem = N'TSQL',
    @command = @CommandSql,
    @database_name = @DatabaseName,
    @flags = 4;             -- Log step output to step history

-- ------------------------------------------------------------------------------
-- 5. Add Daily Schedule: 02:00:00 AM (Hospital Off-Peak Hours)
-- ------------------------------------------------------------------------------
PRINT 'Attaching daily schedule at 02:00 AM...';

EXEC msdb.dbo.sp_add_jobschedule 
    @job_id = @JobId, 
    @name = N'PrIMA_Daily_OffPeak_0200AM',
    @enabled = 1,
    @freq_type = 4,         -- Daily
    @freq_interval = 1,     -- Every 1 day
    @freq_subday_type = 1,  -- At specified time
    @freq_subday_interval = 0,
    @freq_relative_interval = 0,
    @freq_recurrence_factor = 0,
    @active_start_date = 20260101,
    @active_end_date = 99991231,
    @active_start_time = 020000, -- 02:00:00 AM
    @active_end_time = 235959;

-- ------------------------------------------------------------------------------
-- 6. Attach to Local Server
-- ------------------------------------------------------------------------------
EXEC msdb.dbo.sp_add_jobserver 
    @job_id = @JobId, 
    @server_name = N'(local)';

PRINT '====================================================================';
PRINT ' SQL Server Agent Job [' + @JobName + '] created and scheduled.';
PRINT '====================================================================';
GO
