SET NOCOUNT ON;

IF DB_NAME() <> N'LUMAR_ERP_ES_VALIDATION'
BEGIN
    THROW 50001, 'Artifact must run only on LUMAR_ERP_ES_VALIDATION.', 1;
END

IF OBJECT_ID(N'dbo.CustomerMessages', N'U') IS NULL
BEGIN
    THROW 50002, 'dbo.CustomerMessages does not exist in LUMAR_ERP_ES_VALIDATION.', 1;
END

IF COL_LENGTH(N'dbo.CustomerMessages', N'IsRetryable') IS NULL
BEGIN
    ALTER TABLE dbo.CustomerMessages
        ADD IsRetryable BIT NOT NULL CONSTRAINT DF_CustomerMessages_IsRetryable DEFAULT (1);
END
ELSE
BEGIN
    DECLARE @existingType sysname;
    DECLARE @isNullable bit;
    DECLARE @defaultConstraintName sysname;
    DECLARE @defaultDefinition nvarchar(max);

    SELECT
        @existingType = TYPE_NAME(c.user_type_id),
        @isNullable = c.is_nullable
    FROM sys.columns c
    JOIN sys.tables t ON t.object_id = c.object_id
    WHERE t.name = N'CustomerMessages'
      AND SCHEMA_NAME(t.schema_id) = N'dbo'
      AND c.name = N'IsRetryable';

    SELECT
        @defaultConstraintName = dc.name,
        @defaultDefinition = dc.definition
    FROM sys.default_constraints dc
    JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
    JOIN sys.tables t ON t.object_id = c.object_id
    WHERE t.name = N'CustomerMessages'
      AND SCHEMA_NAME(t.schema_id) = N'dbo'
      AND c.name = N'IsRetryable';

    IF @existingType <> N'bit' OR @isNullable <> 0
    BEGIN
        THROW 50003, 'Existing dbo.CustomerMessages.IsRetryable has an incompatible contract; FAIL-CLOSED. Manual review required.', 1;
    END

    IF @defaultConstraintName IS NULL OR @defaultDefinition IS NULL
    BEGIN
        THROW 50004, 'Existing dbo.CustomerMessages.IsRetryable exists without the required default constraint; FAIL-CLOSED. Manual review required.', 1;
    END

    IF LOWER(LTRIM(RTRIM(@defaultDefinition))) NOT IN (N'((1))', N'(1)', N'1')
    BEGIN
        THROW 50005, 'Existing dbo.CustomerMessages.IsRetryable default differs from the approved value 1; FAIL-CLOSED.', 1;
    END
END

IF COL_LENGTH(N'dbo.CustomerMessages', N'IsRetryable') IS NULL
BEGIN
    THROW 50006, 'Verification failed: dbo.CustomerMessages.IsRetryable was not created.', 1;
END

IF EXISTS (
    SELECT 1
    FROM sys.columns c
    JOIN sys.tables t ON t.object_id = c.object_id
    WHERE t.name = N'CustomerMessages'
      AND SCHEMA_NAME(t.schema_id) = N'dbo'
      AND c.name = N'IsRetryable'
      AND TYPE_NAME(c.user_type_id) <> N'bit'
)
BEGIN
    THROW 50007, 'Verification failed: dbo.CustomerMessages.IsRetryable is not BIT.', 1;
END

IF EXISTS (
    SELECT 1
    FROM sys.columns c
    JOIN sys.tables t ON t.object_id = c.object_id
    WHERE t.name = N'CustomerMessages'
      AND SCHEMA_NAME(t.schema_id) = N'dbo'
      AND c.name = N'IsRetryable'
      AND c.is_nullable <> 0
)
BEGIN
    THROW 50008, 'Verification failed: dbo.CustomerMessages.IsRetryable is nullable.', 1;
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.default_constraints dc
    JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
    JOIN sys.tables t ON t.object_id = c.object_id
    WHERE t.name = N'CustomerMessages'
      AND SCHEMA_NAME(t.schema_id) = N'dbo'
      AND c.name = N'IsRetryable'
      AND dc.name = N'DF_CustomerMessages_IsRetryable'
      AND LOWER(LTRIM(RTRIM(dc.definition))) IN (N'((1))', N'(1)', N'1')
)
BEGIN
    IF EXISTS (
        SELECT 1
        FROM sys.default_constraints dc
        JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
        JOIN sys.tables t ON t.object_id = c.object_id
        WHERE t.name = N'CustomerMessages'
          AND SCHEMA_NAME(t.schema_id) = N'dbo'
          AND c.name = N'IsRetryable'
    )
    BEGIN
        THROW 50009, 'Verification failed: default constraint name or default value is not the approved contract for dbo.CustomerMessages.IsRetryable.', 1;
    END
END

PRINT 'WHATSAPP_RETRYABILITY_SCHEMA_FOUNDATION: OK';
