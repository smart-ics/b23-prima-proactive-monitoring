-- ==============================================================================
-- 002_SeedInitialUsers.sql
-- Baseline EDP officer accounts seed script (GAP-003, OQ-004)
-- Idempotently seeds default officer accounts if not already present
-- Password for all baseline users: Hospital@2026! (ASP.NET Core Identity PBKDF2)
-- ==============================================================================

IF NOT EXISTS (SELECT 1 FROM dbo.Officers WHERE Username = 'ahmad.budi')
BEGIN
    INSERT INTO dbo.Officers (Id, Username, FullName, PasswordHash, Role, IsActive)
    VALUES (
        '11111111-1111-1111-1111-111111111101',
        'ahmad.budi',
        'Ahmad Budi',
        'AQAAAAIAAYagAAAAEMmdc3uiohYm2mW/zkhAOqopoll7Fo4OMVl1YNuSlCzK07wAumPvcrsQa/0J8Ry5Wg==',
        'EdpOfficer',
        1
    );
END

IF NOT EXISTS (SELECT 1 FROM dbo.Officers WHERE Username = 'siti.rahma')
BEGIN
    INSERT INTO dbo.Officers (Id, Username, FullName, PasswordHash, Role, IsActive)
    VALUES (
        '11111111-1111-1111-1111-111111111102',
        'siti.rahma',
        'Siti Rahma',
        'AQAAAAIAAYagAAAAEMmdc3uiohYm2mW/zkhAOqopoll7Fo4OMVl1YNuSlCzK07wAumPvcrsQa/0J8Ry5Wg==',
        'SystemAnalyst',
        1
    );
END

IF NOT EXISTS (SELECT 1 FROM dbo.Officers WHERE Username = 'edp.lead')
BEGIN
    INSERT INTO dbo.Officers (Id, Username, FullName, PasswordHash, Role, IsActive)
    VALUES (
        '11111111-1111-1111-1111-111111111103',
        'edp.lead',
        'EDP Lead',
        'AQAAAAIAAYagAAAAEMmdc3uiohYm2mW/zkhAOqopoll7Fo4OMVl1YNuSlCzK07wAumPvcrsQa/0J8Ry5Wg==',
        'Lead',
        1
    );
END
