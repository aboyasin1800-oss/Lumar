SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

IF DB_NAME() <> N'LUMAR_ERP_TEST'
    THROW 52370, N'This repair is restricted to LUMAR_ERP_TEST only.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.AccountingEvents')
          AND name = N'CK_AccountingEvents_SourceCardinality')
        THROW 52371, N'CK_AccountingEvents_SourceCardinality is missing from dbo.AccountingEvents.', 1;

    ALTER TABLE dbo.AccountingEvents DROP CONSTRAINT CK_AccountingEvents_SourceCardinality;

    ALTER TABLE dbo.AccountingEvents WITH CHECK
        ADD CONSTRAINT CK_AccountingEvents_SourceCardinality CHECK
        (
            (AccountingEventType BETWEEN 1 AND 13)
            OR
            (AccountingEventType BETWEEN 20 AND 32
             AND SourceType IS NOT NULL AND SourceId IS NOT NULL
             AND SourceOperationId IS NOT NULL AND OriginalAccountingEventId IS NULL)
            OR
            (AccountingEventType = 33
             AND SourceType = N'AccountingEvent' AND SourceId IS NOT NULL
             AND SourceOperationId IS NOT NULL AND OriginalAccountingEventId = SourceId
             AND ReversalReason IS NOT NULL AND ReversedBy IS NOT NULL AND ReversedAt IS NOT NULL)
        );

    DECLARE @postCheck nvarchar(max);
    SELECT @postCheck = definition
    FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.AccountingEvents')
      AND name = N'CK_AccountingEvents_SourceCardinality';

    IF CHARINDEX(N'OriginalAccountingEventId=SourceId', @postCheck) = 0
       OR CHARINDEX(N'SourceType=N''AccountingEvent''', @postCheck) = 0
       OR CHARINDEX(N'ReversalReason IS NOT NULL', @postCheck) = 0
        THROW 52373, N'The updated source-cardinality constraint is missing the official reversal rule for event 33.', 1;

    SELECT @postCheck AS UpdatedConstraintDefinition;
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
