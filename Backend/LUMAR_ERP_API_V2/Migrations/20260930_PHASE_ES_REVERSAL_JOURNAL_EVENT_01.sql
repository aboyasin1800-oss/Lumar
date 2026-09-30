SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() NOT IN (N'LUMAR_ERP_TEST', N'LUMAR_ERP_ES_VALIDATION')
    THROW 51950, N'This reversal journal event migration is restricted to approved ES validation databases.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.AccountingEvents', N'U') IS NULL
       OR COL_LENGTH(N'dbo.AccountingEvents', N'SourceOperationId') IS NULL
        THROW 51951, N'Accounting event foundation is required before reversal journal events.', 1;

    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_AccountingEvents_Type' AND parent_object_id = OBJECT_ID(N'dbo.AccountingEvents'))
        THROW 51952, N'Accounting event type constraint was not found.', 1;

    ALTER TABLE dbo.AccountingEvents DROP CONSTRAINT CK_AccountingEvents_Type;
    ALTER TABLE dbo.AccountingEvents ADD CONSTRAINT CK_AccountingEvents_Type CHECK (AccountingEventType BETWEEN 1 AND 13 OR AccountingEventType BETWEEN 20 AND 34);

    IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_AccountingEvents_SourceCardinality' AND parent_object_id = OBJECT_ID(N'dbo.AccountingEvents'))
        ALTER TABLE dbo.AccountingEvents DROP CONSTRAINT CK_AccountingEvents_SourceCardinality;

    ALTER TABLE dbo.AccountingEvents ADD CONSTRAINT CK_AccountingEvents_SourceCardinality CHECK
    (
        (AccountingEventType BETWEEN 1 AND 13)
        OR
        (AccountingEventType BETWEEN 20 AND 32
         AND SourceType IS NOT NULL AND SourceId IS NOT NULL AND SourceOperationId IS NOT NULL AND OriginalAccountingEventId IS NULL)
        OR
        (AccountingEventType=33
         AND SourceType=N'AccountingEvent' AND SourceId IS NOT NULL AND SourceOperationId IS NOT NULL
         AND OriginalAccountingEventId=SourceId AND ReversalReason IS NOT NULL AND ReversedBy IS NOT NULL AND ReversedAt IS NOT NULL)
        OR
        (AccountingEventType=34
         AND SourceType IS NOT NULL AND SourceId IS NOT NULL AND SourceOperationId IS NOT NULL
         AND OriginalAccountingEventId IS NULL)
    );

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
