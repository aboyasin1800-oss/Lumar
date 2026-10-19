SET NOCOUNT ON;

IF DB_NAME() <> N'LUMAR_ERP_ES_VALIDATION'
BEGIN
    THROW 50010, 'Rollback must run only on LUMAR_ERP_ES_VALIDATION.', 1;
END

IF OBJECT_ID(N'dbo.CustomerMessages', N'U') IS NULL
BEGIN
    THROW 50011, 'dbo.CustomerMessages does not exist in LUMAR_ERP_ES_VALIDATION.', 1;
END

IF COL_LENGTH(N'dbo.CustomerMessages', N'IsRetryable') IS NULL
BEGIN
    PRINT 'WHATSAPP_RETRYABILITY_SCHEMA_FOUNDATION_ROLLBACK: No action needed; column is already absent.';
    RETURN;
END

DECLARE @constraintName sysname;

SELECT @constraintName = dc.name
FROM sys.default_constraints dc
JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
JOIN sys.tables t ON t.object_id = c.object_id
WHERE t.name = N'CustomerMessages'
  AND SCHEMA_NAME(t.schema_id) = N'dbo'
  AND c.name = N'IsRetryable'
  AND dc.name = N'DF_CustomerMessages_IsRetryable';

IF @constraintName IS NULL
BEGIN
    THROW 50012, 'Rollback blocked: DF_CustomerMessages_IsRetryable is not present; artifact ownership cannot be verified.', 1;
END

ALTER TABLE dbo.CustomerMessages DROP CONSTRAINT DF_CustomerMessages_IsRetryable;
ALTER TABLE dbo.CustomerMessages DROP COLUMN IsRetryable;

PRINT 'WHATSAPP_RETRYABILITY_SCHEMA_FOUNDATION_ROLLBACK: OK';
