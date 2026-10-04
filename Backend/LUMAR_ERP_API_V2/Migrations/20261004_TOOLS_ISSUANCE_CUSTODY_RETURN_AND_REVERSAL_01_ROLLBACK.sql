SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

IF DB_NAME() <> N'LUMAR_ERP_ES_VALIDATION'
    THROW 51700, N'This rollback is restricted to LUMAR_ERP_ES_VALIDATION.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    -- Step 3: منع التراجع إذا وجدت أي بيانات في الجداول الجديدة
    IF EXISTS (SELECT 1 FROM dbo.ToolIssuances)
        THROW 51706, N'ToolIssuances contains data. Rollback blocked to prevent data loss.', 1;
    
    IF EXISTS (SELECT 1 FROM dbo.ToolIssuanceReturns)
        THROW 51707, N'ToolIssuanceReturns contains data. Rollback blocked to prevent data loss.', 1;
    
    IF EXISTS (SELECT 1 FROM dbo.ToolIssuanceReversals)
        THROW 51708, N'ToolIssuanceReversals contains data. Rollback blocked to prevent data loss.', 1;
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

IF DB_NAME() <> N'LUMAR_ERP_ES_VALIDATION'
    THROW 51700, N'This rollback is restricted to LUMAR_ERP_ES_VALIDATION.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    -- Step 3: منع التراجع إذا وجدت أي بيانات في الجداول الجديدة
    IF EXISTS (SELECT 1 FROM dbo.ToolIssuances)
        THROW 51706, N'ToolIssuances contains data. Rollback blocked to prevent data loss.', 1;
    
    IF EXISTS (SELECT 1 FROM dbo.ToolIssuanceReturns)
        THROW 51707, N'ToolIssuanceReturns contains data. Rollback blocked to prevent data loss.', 1;
    
    IF EXISTS (SELECT 1 FROM dbo.ToolIssuanceReversals)
        THROW 51708, N'ToolIssuanceReversals contains data. Rollback blocked to prevent data loss.', 1;