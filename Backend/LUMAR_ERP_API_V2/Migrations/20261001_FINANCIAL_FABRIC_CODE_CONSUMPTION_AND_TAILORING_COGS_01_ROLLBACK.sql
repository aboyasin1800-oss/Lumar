SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;

IF DB_NAME() NOT IN (N'LUMAR_ERP_TEST', N'LUMAR_ERP_ES_VALIDATION')
    THROW 52620, N'This rollback is restricted to approved validation databases.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.TailoringCostPostings', N'U') IS NULL
       OR COL_LENGTH(N'dbo.FabricConsumptionSources', N'ReadyMadeProductionOrderItemId') IS NULL
       OR COL_LENGTH(N'dbo.FabricConsumptionSources', N'ReadyMadeProductionOrderPieceInstanceId') IS NULL
       OR NOT EXISTS (SELECT 1 FROM dbo.AccountingEventDefinitions WHERE AccountingEventType = 35 AND EventName = N'TailoringCostOfSales')
        THROW 52621, N'The fabric-code consumption contract is not installed.', 1;

    IF EXISTS (SELECT 1 FROM dbo.TailoringCostPostings)
       OR EXISTS (SELECT 1 FROM dbo.AccountingEvents WHERE AccountingEventType = 35)
       OR EXISTS
          (
              SELECT 1
              FROM dbo.FabricConsumptionSources
              WHERE FabricRollId IS NULL
                 OR OrderItemId IS NULL
                 OR ReadyMadeProductionOrderItemId IS NOT NULL
                 OR ReadyMadeProductionOrderPieceInstanceId IS NOT NULL
          )
        THROW 52622, N'Rollback is blocked because operational data depends on the fabric-code contract.', 1;

    DROP TABLE dbo.TailoringCostPostings;

    DELETE dbo.AccountingEventDefinitions
    WHERE AccountingEventType = 35 AND EventName = N'TailoringCostOfSales';

    DELETE dbo.AccountRoleMappings
    WHERE AccountRole IN (N'CostOfSales', N'WorkInProgress');

    ALTER TABLE dbo.AccountingEvents DROP CONSTRAINT CK_AccountingEvents_Type;
    ALTER TABLE dbo.AccountingEvents DROP CONSTRAINT CK_AccountingEvents_SourceCardinality;

    ALTER TABLE dbo.AccountingEvents ADD
        CONSTRAINT CK_AccountingEvents_Type CHECK
        (
            AccountingEventType BETWEEN 1 AND 13
            OR AccountingEventType BETWEEN 20 AND 34
        ),
        CONSTRAINT CK_AccountingEvents_SourceCardinality CHECK
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
            OR
            (AccountingEventType = 34
             AND SourceType IS NOT NULL AND SourceId IS NOT NULL
             AND SourceOperationId IS NOT NULL AND OriginalAccountingEventId IS NULL)
        );

    EXEC(N'
CREATE OR ALTER PROCEDURE dbo.usp_ReverseFoundationAccountingEvent
 @OriginalAccountingEventId bigint,@SourceOperationId uniqueidentifier,@ReferenceNumber nvarchar(200),@Reason nvarchar(500),@ReversedBy nvarchar(100)
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF @@TRANCOUNT=0 THROW 51942,N''Foundation reversals require a caller-owned transaction.'',1;
 IF @SourceOperationId IS NULL OR @Reason IS NULL OR @ReversedBy IS NULL THROW 51943,N''The reversal is incomplete.'',1;
 DECLARE @amount decimal(18,2),@originalType tinyint,@originalStatus nvarchar(20),@originalSourceType nvarchar(50),@originalSourceId bigint;
 SELECT @amount=PostingAmount,@originalType=AccountingEventType,@originalStatus=Status,@originalSourceType=SourceType,@originalSourceId=SourceId FROM dbo.AccountingEvents WITH(UPDLOCK,HOLDLOCK) WHERE AccountingEventId=@OriginalAccountingEventId;
 IF @amount IS NULL OR (@originalType NOT BETWEEN 20 AND 32 AND @originalType NOT IN(7,9)) OR @originalStatus<>N''Posted'' THROW 51944,N''The original event cannot be reversed.'',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.AccountingEventDefinitions WHERE AccountingEventType=33 AND IsEnabled=1 AND IsBusinessRuntimeEnabled=0) THROW 51945,N''The reversal definition is disabled.'',1;
 IF EXISTS(SELECT 1 FROM dbo.AccountingEvents WHERE OriginalAccountingEventId=@OriginalAccountingEventId) THROW 51946,N''The original event has already been reversed.'',1;
 DECLARE @existing bigint; SELECT @existing=AccountingEventId FROM dbo.AccountingEvents WITH(UPDLOCK,HOLDLOCK) WHERE SourceOperationId=@SourceOperationId AND AccountingEventType=33;
 IF @existing IS NOT NULL BEGIN SELECT @existing AS AccountingEventId,CAST(1 AS bit) AS IsExisting; RETURN; END;
 INSERT dbo.AccountingEvents(AccountingEventType,PostingAmount,SourceType,SourceId,SourceOperationId,OriginalAccountingEventId,ReversalReason,ReversedBy,ReversedAt,Status) VALUES(33,@amount,N''AccountingEvent'',@OriginalAccountingEventId,@SourceOperationId,@OriginalAccountingEventId,@Reason,@ReversedBy,SYSUTCDATETIME(),N''Posted''); DECLARE @eventId bigint=SCOPE_IDENTITY();
 INSERT dbo.FinancialTransactions(ReferenceNumber,TransactionType,Amount,Description,CreatedAt,AccountingEventId) VALUES(@ReferenceNumber,N''Reversal'',@amount,@Reason,SYSUTCDATETIME(),@eventId);
 INSERT dbo.JournalEntries(ReferenceNumber,Description,EntryDate,CreatedAt,AccountingEventId) VALUES(@ReferenceNumber,@Reason,SYSUTCDATETIME(),SYSUTCDATETIME(),@eventId); DECLARE @journalId int=SCOPE_IDENTITY();
 INSERT dbo.JournalEntryLines(JournalEntryId,LedgerAccountId,DebitAmount,CreditAmount,Description) SELECT @journalId,LedgerAccountId,CreditAmount,DebitAmount,@Reason FROM dbo.JournalEntryLines WHERE JournalEntryId=(SELECT JournalEntryId FROM dbo.JournalEntries WHERE AccountingEventId=@OriginalAccountingEventId);
 DECLARE @originalCashId bigint,@cashAccountId int,@cashDirection tinyint,@recipientType nvarchar(50),@recipientId bigint;
 SELECT @originalCashId=CashMovementId,@cashAccountId=CashAccountId,@cashDirection=CashDirection,@recipientType=RecipientType,@recipientId=RecipientId FROM dbo.CashMovements WITH(UPDLOCK,HOLDLOCK) WHERE AccountingEventId=@OriginalAccountingEventId;
 DECLARE @reversalCashDirection tinyint=CASE WHEN @cashDirection=1 THEN 2 ELSE 1 END;
 IF @originalCashId IS NOT NULL EXEC dbo.usp_PostFoundationCashMovement @AccountingEventId=@eventId,@CashAccountId=@cashAccountId,@CashDirection=@reversalCashDirection,@Amount=@amount,@CurrencyCode=N''YER'',@ReferenceNumber=@ReferenceNumber,@RecipientType=@recipientType,@RecipientId=@recipientId,@SourceType=N''AccountingEvent'',@SourceId=@OriginalAccountingEventId,@SourceOperationId=@SourceOperationId,@CreatedBy=@ReversedBy,@OriginalCashMovementId=@originalCashId;
 UPDATE dbo.AccountingEvents SET Status=N''Reversed'' WHERE AccountingEventId=@OriginalAccountingEventId;
 UPDATE dbo.CashMovements SET Status=N''Reversed'' WHERE CashMovementId=@originalCashId;
 SELECT @eventId AS AccountingEventId,CAST(0 AS bit) AS IsExisting;
END;');

    DROP INDEX IX_FabricConsumptionSources_ReadyMadeOrderItem ON dbo.FabricConsumptionSources;
    DROP INDEX IX_FabricConsumptionSources_OrderItem ON dbo.FabricConsumptionSources;
    DROP INDEX IX_FabricConsumptionSources_InventoryItem ON dbo.FabricConsumptionSources;

    ALTER TABLE dbo.FabricConsumptionSources DROP CONSTRAINT CK_FabricConsumptionSources_SourceKind;
    ALTER TABLE dbo.FabricConsumptionSources DROP CONSTRAINT FK_FabricConsumptionSources_ReadyMadePiece;
    ALTER TABLE dbo.FabricConsumptionSources DROP CONSTRAINT FK_FabricConsumptionSources_ReadyMadeOrderItem;
    ALTER TABLE dbo.FabricConsumptionSources DROP COLUMN
        ReadyMadeProductionOrderPieceInstanceId,
        ReadyMadeProductionOrderItemId;

    ALTER TABLE dbo.FabricConsumptionSources ALTER COLUMN OrderItemId int NOT NULL;
    ALTER TABLE dbo.FabricConsumptionSources ALTER COLUMN FabricRollId bigint NOT NULL;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
