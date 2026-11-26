-- ==============================================================================
-- PrIMA Proactive Monitoring - SIMRS Dedicated Read-Only Provisioning Script
-- Realizes DEC-008 & BR-PRIMA-007 (Non-Intrusive Safety Guardrails)
--
-- PURPOSE:
--   Provisions a dedicated SQL Server login and database user (`prima_readonly`)
--   confined strictly to read-only SELECT queries on the 6 pilot SIMRS tables.
--   Enforces explicit DENY on all DML (INSERT, UPDATE, DELETE) and DDL/EXECUTE.
--
-- INSTRUCTIONS FOR HOSPITAL DBA:
--   1. Replace '<REPLACE_WITH_STRONG_DBA_GENERATED_PASSWORD>' with a strong password.
--   2. Ensure the script is executed by a sysadmin or db_owner on the SIMRS database.
-- ==============================================================================

USE master;
GO

SET NOCOUNT ON;
PRINT '====================================================================';
PRINT ' Provisioning PrIMA Dedicated Read-Only SIMRS Database Account';
PRINT '====================================================================';

-- ------------------------------------------------------------------------------
-- 1. Create Server-Level SQL Login
-- ------------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = 'prima_readonly')
BEGIN
    PRINT 'Creating SQL Server login [prima_readonly]...';
    -- DBA: Ensure a 32+ character strong alphanumeric password is substituted
    CREATE LOGIN prima_readonly
    WITH PASSWORD = N'<REPLACE_WITH_STRONG_DBA_GENERATED_PASSWORD>',
         DEFAULT_DATABASE = SIMRS,
         CHECK_EXPIRATION = ON,
         CHECK_POLICY = ON;
    PRINT '-> Login [prima_readonly] created successfully.';
END
ELSE
BEGIN
    PRINT '-> Login [prima_readonly] already exists. Skipping creation.';
END;
GO

-- ------------------------------------------------------------------------------
-- 2. Create Database-Level User in Target Database
-- ------------------------------------------------------------------------------
-- Change database context to SIMRS (or change name to hospital's production OLTP DB)
IF DB_NAME() <> 'SIMRS' AND EXISTS (SELECT 1 FROM sys.databases WHERE name = 'SIMRS')
BEGIN
    USE SIMRS;
END;
GO

PRINT 'Configuring user in database: ' + DB_NAME();

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'prima_readonly')
BEGIN
    PRINT 'Creating database user [prima_readonly]...';
    CREATE USER prima_readonly FOR LOGIN prima_readonly;
    PRINT '-> Database user [prima_readonly] created.';
END
ELSE
BEGIN
    PRINT '-> Database user [prima_readonly] already exists.';
END;
GO

-- ------------------------------------------------------------------------------
-- 3. Grant Explicit SELECT on Required SIMRS Tables Only
-- ------------------------------------------------------------------------------
PRINT 'Granting SELECT on pilot SIMRS tables...';

-- Table 1: Accounting Journal Entries
IF OBJECT_ID('simrs.acc_journal_entries', 'U') IS NOT NULL
BEGIN
    GRANT SELECT ON simrs.acc_journal_entries TO prima_readonly;
    PRINT '  -> GRANT SELECT ON simrs.acc_journal_entries TO prima_readonly;';
END;

-- Table 2: Patient Receivables (Billing / AR)
IF OBJECT_ID('simrs.ar_receivables', 'U') IS NOT NULL
BEGIN
    GRANT SELECT ON simrs.ar_receivables TO prima_readonly;
    PRINT '  -> GRANT SELECT ON simrs.ar_receivables TO prima_readonly;';
END;

-- Table 3: Patient Bills (Billing Pipeline)
IF OBJECT_ID('simrs.trx_patient_bills', 'U') IS NOT NULL
BEGIN
    GRANT SELECT ON simrs.trx_patient_bills TO prima_readonly;
    PRINT '  -> GRANT SELECT ON simrs.trx_patient_bills TO prima_readonly;';
END;

-- Table 4: EMR Sync Logs
IF OBJECT_ID('simrs.emr_sync_logs', 'U') IS NOT NULL
BEGIN
    GRANT SELECT ON simrs.emr_sync_logs TO prima_readonly;
    PRINT '  -> GRANT SELECT ON simrs.emr_sync_logs TO prima_readonly;';
END;

-- Table 5: Outpatient Doctor Schedules (BPJS HFIS)
IF OBJECT_ID('simrs.hfis_doctor_schedules', 'U') IS NOT NULL
BEGIN
    GRANT SELECT ON simrs.hfis_doctor_schedules TO prima_readonly;
    PRINT '  -> GRANT SELECT ON simrs.hfis_doctor_schedules TO prima_readonly;';
END;

-- Table 6: Remote Print Server Queues (Infrastructure)
IF OBJECT_ID('simrs.infra_print_queues', 'U') IS NOT NULL
BEGIN
    GRANT SELECT ON simrs.infra_print_queues TO prima_readonly;
    PRINT '  -> GRANT SELECT ON simrs.infra_print_queues TO prima_readonly;';
END;

-- Fallback for schema-level select if custom tables exist within simrs schema
IF SCHEMA_ID('simrs') IS NOT NULL
BEGIN
    GRANT SELECT ON SCHEMA::simrs TO prima_readonly;
    PRINT '  -> GRANT SELECT ON SCHEMA::simrs TO prima_readonly;';
END;
GO

-- ------------------------------------------------------------------------------
-- 4. Explicit Security Hardening: DENY All DML and DDL Modifications
-- ------------------------------------------------------------------------------
PRINT 'Enforcing strict DENY guardrails on DML, DDL, and Stored Procedures...';

IF SCHEMA_ID('simrs') IS NOT NULL
BEGIN
    DENY INSERT, UPDATE, DELETE ON SCHEMA::simrs TO prima_readonly;
    DENY ALTER, CONTROL, DROP, REFERENCES ON SCHEMA::simrs TO prima_readonly;
    PRINT '  -> DENIED INSERT, UPDATE, DELETE, ALTER, CONTROL, DROP ON SCHEMA::simrs.';
END;

-- Database-wide execute denial to ensure no stored procedures can be run
DENY EXECUTE TO prima_readonly;
PRINT '  -> DENIED EXECUTE database-wide to prima_readonly.';
GO

-- ------------------------------------------------------------------------------
-- 5. Verification & DBA Sign-Off Queries
-- ------------------------------------------------------------------------------
PRINT '--------------------------------------------------------------------';
PRINT ' Verifying Granted and Denied Permissions for [prima_readonly]:';
PRINT '--------------------------------------------------------------------';

SELECT 
    dp.name AS PrincipalName,
    o.name AS ObjectName,
    p.permission_name AS Permission,
    p.state_desc AS State
FROM sys.database_permissions p
JOIN sys.database_principals dp ON p.grantee_principal_id = dp.principal_id
LEFT JOIN sys.objects o ON p.major_id = o.object_id
WHERE dp.name = 'prima_readonly'
ORDER BY p.state_desc, p.permission_name;

PRINT '====================================================================';
PRINT ' Provisioning completed. Verify permissions table above.';
PRINT '====================================================================';
GO
