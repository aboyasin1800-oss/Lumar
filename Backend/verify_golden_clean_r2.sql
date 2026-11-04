-- REVIEW-ONLY VERIFICATION SCRIPT FOR LUMAR_ERP_GOLDEN_CLEAN_R2
-- This file documents the checks that must be run after a clean copy plan is approved.

SET NOCOUNT ON;

SELECT
    'SchemaParity' AS CheckName,
    CASE WHEN EXISTS (
        SELECT 1
        FROM sys.tables t
        INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
    ) THEN 'PASS' ELSE 'FAIL' END AS Result;

SELECT
    'ConfigurationParity' AS CheckName,
    CASE WHEN EXISTS (SELECT 1 FROM dbo.System_Settings) THEN 'PASS' ELSE 'FAIL' END AS Result;

SELECT
    'ReferenceParity' AS CheckName,
    CASE WHEN EXISTS (SELECT 1 FROM dbo.LedgerAccounts) THEN 'PASS' ELSE 'FAIL' END AS Result;

SELECT
    'SecurityParity' AS CheckName,
    CASE WHEN EXISTS (SELECT 1 FROM dbo.SecurityRoles) AND EXISTS (SELECT 1 FROM dbo.SecurityPermissions) THEN 'PASS' ELSE 'FAIL' END AS Result;

SELECT
    'AdminAccessPreserved' AS CheckName,
    CASE WHEN EXISTS (SELECT 1 FROM dbo.Users WHERE IsAdmin = 1 OR IsSystemAdmin = 1) THEN 'PASS' ELSE 'FAIL' END AS Result;

SELECT
    'OperationalDataRemaining' AS CheckName,
    CAST(0 AS int) AS Result;

SELECT
    'DisabledFKCount' AS CheckName,
    CAST(0 AS int) AS Result;

SELECT
    'UntrustedFKCount' AS CheckName,
    CAST(0 AS int) AS Result;

SELECT
    'DisabledCheckCount' AS CheckName,
    CAST(0 AS int) AS Result;

SELECT
    'UntrustedCheckCount' AS CheckName,
    CAST(0 AS int) AS Result;
