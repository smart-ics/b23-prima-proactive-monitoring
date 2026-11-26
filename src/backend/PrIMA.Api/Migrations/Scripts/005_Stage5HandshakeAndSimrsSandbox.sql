-- ==============================================================================
-- Migration: 005_Stage5HandshakeAndSimrsSandbox.sql
-- Purpose: Stage 5 Handshake, Per-Detector Hashed Credentials & SIMRS Sandbox Tables
-- Realizes DEC-001, DEC-006 from IS005 Architecture §8.1 & §8.2
-- ==============================================================================

-- ------------------------------------------------------------------------------
-- 1. Table: dbo.DetectorEnrollmentTokens
-- ------------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DetectorEnrollmentTokens' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.DetectorEnrollmentTokens (
        Id UNIQUEIDENTIFIER PRIMARY KEY,
        TokenPrefix NVARCHAR(20) NOT NULL,
        TokenHash NVARCHAR(128) NOT NULL,
        Label NVARCHAR(100) NULL,
        Status NVARCHAR(30) NOT NULL,              -- Pending, Consumed, Revoked
        ExpiresAtUtc DATETIME2 NOT NULL,
        CreatedByOfficer NVARCHAR(100) NOT NULL,
        CreatedAtUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        ConsumedAtUtc DATETIME2 NULL,
        ConsumedByDetectorId UNIQUEIDENTIFIER NULL CONSTRAINT FK_DetectorEnrollmentTokens_Detectors FOREIGN KEY REFERENCES dbo.Detectors(Id)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DetectorEnrollmentTokens_Status_Expires' AND object_id = OBJECT_ID('dbo.DetectorEnrollmentTokens'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_DetectorEnrollmentTokens_Status_Expires 
    ON dbo.DetectorEnrollmentTokens(Status, ExpiresAtUtc);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_DetectorEnrollmentTokens_TokenHash' AND object_id = OBJECT_ID('dbo.DetectorEnrollmentTokens'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UX_DetectorEnrollmentTokens_TokenHash 
    ON dbo.DetectorEnrollmentTokens(TokenHash);
END;

-- ------------------------------------------------------------------------------
-- 2. Alter Table: dbo.Detectors (Credentials, Versioning & Cadence Drift)
-- ------------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'ApiKeyHash' AND object_id = OBJECT_ID('dbo.Detectors'))
BEGIN
    ALTER TABLE dbo.Detectors ADD ApiKeyHash NVARCHAR(128) NULL;
END;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'Version' AND object_id = OBJECT_ID('dbo.Detectors'))
BEGIN
    ALTER TABLE dbo.Detectors ADD Version NVARCHAR(50) NULL;
END;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'LastActualIntervalSeconds' AND object_id = OBJECT_ID('dbo.Detectors'))
BEGIN
    ALTER TABLE dbo.Detectors ADD LastActualIntervalSeconds INT NULL;
END;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE name = 'CadenceDriftWarning' AND object_id = OBJECT_ID('dbo.Detectors'))
BEGIN
    ALTER TABLE dbo.Detectors ADD CadenceDriftWarning NVARCHAR(500) NULL;
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Detectors_ApiKeyHash' AND object_id = OBJECT_ID('dbo.Detectors'))
BEGIN
    EXEC('CREATE NONCLUSTERED INDEX IX_Detectors_ApiKeyHash ON dbo.Detectors(ApiKeyHash) WHERE ApiKeyHash IS NOT NULL');
END;

-- ------------------------------------------------------------------------------
-- 3. Schema: simrs Sandbox Schema & Realistic Pilot Tables
-- ------------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'simrs')
BEGIN
    EXEC('CREATE SCHEMA simrs');
END;

-- Table: simrs.acc_journal_entries (Accounting)
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'acc_journal_entries' AND schema_id = SCHEMA_ID('simrs'))
BEGIN
    CREATE TABLE simrs.acc_journal_entries (
        id BIGINT IDENTITY(1,1) PRIMARY KEY,
        journal_batch_no NVARCHAR(50) NOT NULL,
        entry_date DATETIME2 NOT NULL,
        account_code NVARCHAR(50) NOT NULL,
        debit DECIMAL(18,2) NOT NULL DEFAULT 0.00,
        credit DECIMAL(18,2) NOT NULL DEFAULT 0.00,
        description NVARCHAR(250) NULL,
        posted_status NVARCHAR(20) NOT NULL DEFAULT 'Posted',
        created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_simrs_acc_journal_batch' AND object_id = OBJECT_ID('simrs.acc_journal_entries'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_simrs_acc_journal_batch 
    ON simrs.acc_journal_entries(journal_batch_no, posted_status);
END;

-- Table: simrs.ar_receivables (Billing / AR)
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ar_receivables' AND schema_id = SCHEMA_ID('simrs'))
BEGIN
    CREATE TABLE simrs.ar_receivables (
        id BIGINT IDENTITY(1,1) PRIMARY KEY,
        source_trx_no NVARCHAR(50) NOT NULL,
        invoice_no NVARCHAR(50) NOT NULL,
        patient_norm NVARCHAR(50) NOT NULL,
        receivable_amount DECIMAL(18,2) NOT NULL,
        status NVARCHAR(20) NOT NULL DEFAULT 'Active',
        created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_simrs_ar_receivables_source' AND object_id = OBJECT_ID('simrs.ar_receivables'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_simrs_ar_receivables_source 
    ON simrs.ar_receivables(source_trx_no, status);
END;

-- Table: simrs.trx_patient_bills (Billing Pipeline)
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'trx_patient_bills' AND schema_id = SCHEMA_ID('simrs'))
BEGIN
    CREATE TABLE simrs.trx_patient_bills (
        id BIGINT IDENTITY(1,1) PRIMARY KEY,
        bill_invoice_no NVARCHAR(50) NOT NULL CONSTRAINT UQ_simrs_trx_patient_bills_invoice UNIQUE,
        patient_norm NVARCHAR(50) NOT NULL,
        bill_amount DECIMAL(18,2) NOT NULL,
        settlement_status NVARCHAR(20) NOT NULL,   -- Settled, Pending
        settled_at DATETIME2 NOT NULL,
        created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_simrs_trx_patient_bills_settled' AND object_id = OBJECT_ID('simrs.trx_patient_bills'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_simrs_trx_patient_bills_settled 
    ON simrs.trx_patient_bills(settlement_status, settled_at);
END;

-- Table: simrs.emr_sync_logs (EMR Integration)
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'emr_sync_logs' AND schema_id = SCHEMA_ID('simrs'))
BEGIN
    CREATE TABLE simrs.emr_sync_logs (
        id BIGINT IDENTITY(1,1) PRIMARY KEY,
        encounter_no NVARCHAR(50) NOT NULL,
        sync_type NVARCHAR(50) NOT NULL,          -- Diagnosis, Prescription, ClinicalNote
        sync_status NVARCHAR(20) NOT NULL,        -- Synced, Pending, Failed
        error_message NVARCHAR(MAX) NULL,
        created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        updated_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_simrs_emr_sync_status' AND object_id = OBJECT_ID('simrs.emr_sync_logs'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_simrs_emr_sync_status 
    ON simrs.emr_sync_logs(sync_status, created_at);
END;

-- Table: simrs.hfis_doctor_schedules (BPJS HFIS Sync)
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'hfis_doctor_schedules' AND schema_id = SCHEMA_ID('simrs'))
BEGIN
    CREATE TABLE simrs.hfis_doctor_schedules (
        id BIGINT IDENTITY(1,1) PRIMARY KEY,
        doctor_schedule_code NVARCHAR(50) NOT NULL CONSTRAINT UQ_simrs_hfis_schedule_code UNIQUE,
        doctor_name NVARCHAR(100) NOT NULL,
        clinic_name NVARCHAR(100) NOT NULL,
        schedule_date DATE NOT NULL,
        simrs_start_time TIME NOT NULL,
        simrs_end_time TIME NOT NULL,
        simrs_quota INT NOT NULL,
        hfis_start_time TIME NOT NULL,
        hfis_end_time TIME NOT NULL,
        hfis_quota INT NOT NULL,
        is_active BIT NOT NULL DEFAULT 1,
        created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_simrs_hfis_schedule' AND object_id = OBJECT_ID('simrs.hfis_doctor_schedules'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_simrs_hfis_schedule 
    ON simrs.hfis_doctor_schedules(schedule_date, is_active);
END;

-- Table: simrs.infra_print_queues (Infrastructure)
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'infra_print_queues' AND schema_id = SCHEMA_ID('simrs'))
BEGIN
    CREATE TABLE simrs.infra_print_queues (
        id BIGINT IDENTITY(1,1) PRIMARY KEY,
        station_queue_name NVARCHAR(100) NOT NULL CONSTRAINT UQ_simrs_infra_print_queue UNIQUE,
        hospital_unit NVARCHAR(100) NOT NULL,
        printer_name NVARCHAR(150) NOT NULL,
        pending_jobs INT NOT NULL DEFAULT 0,
        oldest_job_age_seconds INT NOT NULL DEFAULT 0,
        spooler_status NVARCHAR(30) NOT NULL DEFAULT 'Online', -- Online, Offline, Error
        last_heartbeat_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_simrs_infra_print_status' AND object_id = OBJECT_ID('simrs.infra_print_queues'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_simrs_infra_print_status 
    ON simrs.infra_print_queues(spooler_status, pending_jobs);
END;

-- ------------------------------------------------------------------------------
-- 4. Baseline Clean Records & Representative Anomaly Fixtures
-- ------------------------------------------------------------------------------

-- 1. Journal Entries Fixtures
IF NOT EXISTS (SELECT 1 FROM simrs.acc_journal_entries WHERE journal_batch_no = 'JRN-202611-0001')
BEGIN
    -- Baseline Clean Balanced Batch (Debit = Credit = 500,000.00)
    INSERT INTO simrs.acc_journal_entries (journal_batch_no, entry_date, account_code, debit, credit, description, posted_status)
    VALUES 
        ('JRN-202611-0001', SYSUTCDATETIME(), '1101.01', 500000.00, 0.00, 'Kas Rawat Jalan Kasir A', 'Posted'),
        ('JRN-202611-0001', SYSUTCDATETIME(), '4101.01', 0.00, 500000.00, 'Pendapatan Layanan Dokter Umum', 'Posted');
END;

IF NOT EXISTS (SELECT 1 FROM simrs.acc_journal_entries WHERE journal_batch_no = 'JRN-202611-ERR01')
BEGIN
    -- Representative Anomaly Unbalanced Batch (Debit: 1,250,000.00, Credit: 1,200,000.00, Delta: 50,000.00)
    INSERT INTO simrs.acc_journal_entries (journal_batch_no, entry_date, account_code, debit, credit, description, posted_status)
    VALUES 
        ('JRN-202611-ERR01', SYSUTCDATETIME(), '1101.02', 1250000.00, 0.00, 'Kas Farmasi IGD', 'Posted'),
        ('JRN-202611-ERR01', SYSUTCDATETIME(), '4102.01', 0.00, 1200000.00, 'Pendapatan Penjualan Obat IGD', 'Posted');
END;

-- 2. Patient Receivables Fixtures
IF NOT EXISTS (SELECT 1 FROM simrs.ar_receivables WHERE source_trx_no = 'TRX-AR-202611-001')
BEGIN
    -- Baseline Clean Single Receivable
    INSERT INTO simrs.ar_receivables (source_trx_no, invoice_no, patient_norm, receivable_amount, status)
    VALUES ('TRX-AR-202611-001', 'INV-202611-001', 'RM-001001', 750000.00, 'Active');
END;

IF NOT EXISTS (SELECT 1 FROM simrs.ar_receivables WHERE source_trx_no = 'TRX-AR-202611-DUP01')
BEGIN
    -- Representative Anomaly Duplicate Active Receivables for identical source_trx_no
    INSERT INTO simrs.ar_receivables (source_trx_no, invoice_no, patient_norm, receivable_amount, status)
    VALUES 
        ('TRX-AR-202611-DUP01', 'INV-202611-DUP01A', 'RM-002002', 1500000.00, 'Active'),
        ('TRX-AR-202611-DUP01', 'INV-202611-DUP01B', 'RM-002002', 1500000.00, 'Active');
END;

-- 3. Patient Bills & Matching Journal Fixtures
IF NOT EXISTS (SELECT 1 FROM simrs.trx_patient_bills WHERE bill_invoice_no = 'BILL-202611-001')
BEGIN
    -- Baseline Clean Bill with matching Journal
    INSERT INTO simrs.trx_patient_bills (bill_invoice_no, patient_norm, bill_amount, settlement_status, settled_at)
    VALUES ('BILL-202611-001', 'RM-003001', 350000.00, 'Settled', DATEADD(minute, -30, SYSUTCDATETIME()));

    IF NOT EXISTS (SELECT 1 FROM simrs.acc_journal_entries WHERE journal_batch_no = 'BILL-202611-001')
    BEGIN
        INSERT INTO simrs.acc_journal_entries (journal_batch_no, entry_date, account_code, debit, credit, description, posted_status)
        VALUES 
            ('BILL-202611-001', DATEADD(minute, -30, SYSUTCDATETIME()), '1101.01', 350000.00, 0.00, 'Auto-Journal Bill-001 Settled', 'Posted'),
            ('BILL-202611-001', DATEADD(minute, -30, SYSUTCDATETIME()), '4101.01', 0.00, 350000.00, 'Auto-Journal Bill-001 Pendapatan', 'Posted');
    END;
END;

IF NOT EXISTS (SELECT 1 FROM simrs.trx_patient_bills WHERE bill_invoice_no = 'BILL-202611-NOJRN01')
BEGIN
    -- Representative Anomaly Settled Bill (>15 mins ago) missing auto-journal
    INSERT INTO simrs.trx_patient_bills (bill_invoice_no, patient_norm, bill_amount, settlement_status, settled_at)
    VALUES ('BILL-202611-NOJRN01', 'RM-004002', 890000.00, 'Settled', DATEADD(hour, -2, SYSUTCDATETIME()));
END;

-- 4. EMR Sync Logs Fixtures
IF NOT EXISTS (SELECT 1 FROM simrs.emr_sync_logs WHERE encounter_no = 'ENC-202611-001')
BEGIN
    -- Baseline Clean Synced EMR Log
    INSERT INTO simrs.emr_sync_logs (encounter_no, sync_type, sync_status, error_message)
    VALUES ('ENC-202611-001', 'Prescription', 'Synced', NULL);
END;

IF NOT EXISTS (SELECT 1 FROM simrs.emr_sync_logs WHERE encounter_no = 'ENC-202611-FAIL01')
BEGIN
    -- Representative Anomaly Failed EMR Sync
    INSERT INTO simrs.emr_sync_logs (encounter_no, sync_type, sync_status, error_message)
    VALUES ('ENC-202611-FAIL01', 'Diagnosis', 'Failed', 'SATUSEHAT FHIR Encounter mapping exception: HTTP 502 Bad Gateway');
END;

IF NOT EXISTS (SELECT 1 FROM simrs.emr_sync_logs WHERE encounter_no = 'ENC-202611-STALL01')
BEGIN
    -- Representative Anomaly Stalled EMR Sync (>30 mins pending)
    INSERT INTO simrs.emr_sync_logs (encounter_no, sync_type, sync_status, error_message, created_at, updated_at)
    VALUES ('ENC-202611-STALL01', 'ClinicalNote', 'Pending', NULL, DATEADD(hour, -2, SYSUTCDATETIME()), DATEADD(hour, -2, SYSUTCDATETIME()));
END;

-- 5. Outpatient Doctor Schedules Fixtures
IF NOT EXISTS (SELECT 1 FROM simrs.hfis_doctor_schedules WHERE doctor_schedule_code = 'SCH-INT-001')
BEGIN
    -- Baseline Clean Aligned Schedule
    INSERT INTO simrs.hfis_doctor_schedules (doctor_schedule_code, doctor_name, clinic_name, schedule_date, simrs_start_time, simrs_end_time, simrs_quota, hfis_start_time, hfis_end_time, hfis_quota, is_active)
    VALUES ('SCH-INT-001', 'dr. Budi Santoso, Sp.PD', 'Poli Penyakit Dalam', CAST(SYSUTCDATETIME() AS DATE), '08:00:00', '12:00:00', 30, '08:00:00', '12:00:00', 30, 1);
END;

IF NOT EXISTS (SELECT 1 FROM simrs.hfis_doctor_schedules WHERE doctor_schedule_code = 'SCH-OBG-002')
BEGIN
    -- Representative Anomaly Divergent Schedule (SIMRS start 09:00 vs HFIS start 10:00, quota 25 vs 15)
    INSERT INTO simrs.hfis_doctor_schedules (doctor_schedule_code, doctor_name, clinic_name, schedule_date, simrs_start_time, simrs_end_time, simrs_quota, hfis_start_time, hfis_end_time, hfis_quota, is_active)
    VALUES ('SCH-OBG-002', 'dr. Siti Rahma, Sp.OG', 'Poli Kebidanan & Kandungan', CAST(SYSUTCDATETIME() AS DATE), '09:00:00', '13:00:00', 25, '10:00:00', '13:00:00', 15, 1);
END;

-- 6. Remote Print Server Queues Fixtures
IF NOT EXISTS (SELECT 1 FROM simrs.infra_print_queues WHERE station_queue_name = 'PRN-FARMASI-01')
BEGIN
    -- Baseline Clean Queue (Spooler Online, 0 pending jobs)
    INSERT INTO simrs.infra_print_queues (station_queue_name, hospital_unit, printer_name, pending_jobs, oldest_job_age_seconds, spooler_status)
    VALUES ('PRN-FARMASI-01', 'Instalasi Farmasi Rawat Jalan', 'EPSON TM-T82X Farmasi', 0, 0, 'Online');
END;

IF NOT EXISTS (SELECT 1 FROM simrs.infra_print_queues WHERE station_queue_name = 'PRN-KASIR-02')
BEGIN
    -- Representative Anomaly Stalled Print Queue (>300 seconds oldest job)
    INSERT INTO simrs.infra_print_queues (station_queue_name, hospital_unit, printer_name, pending_jobs, oldest_job_age_seconds, spooler_status)
    VALUES ('PRN-KASIR-02', 'Loket Pembayaran Kasir 2', 'EPSON TM-U220 Kasir', 12, 720, 'Online');
END;

IF NOT EXISTS (SELECT 1 FROM simrs.infra_print_queues WHERE station_queue_name = 'PRN-IGD-01')
BEGIN
    -- Representative Anomaly Offline Spooler
    INSERT INTO simrs.infra_print_queues (station_queue_name, hospital_unit, printer_name, pending_jobs, oldest_job_age_seconds, spooler_status)
    VALUES ('PRN-IGD-01', 'Instalasi Gawat Darurat (IGD)', 'HP LaserJet Pro M404 IGD', 4, 1800, 'Offline');
END;
