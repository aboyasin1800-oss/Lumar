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
    THROW 52600, N'This fabric-code consumption migration is restricted to approved validation databases.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.FabricConsumptionSources', N'U') IS NULL
       OR OBJECT_ID(N'dbo.InventoryItemFoundation', N'U') IS NULL
       OR OBJECT_ID(N'dbo.AccountingEvents', N'U') IS NULL
       OR OBJECT_ID(N'dbo.AccountingEventDefinitions', N'U') IS NULL
       OR OBJECT_ID(N'dbo.AccountRoleMappings', N'U') IS NULL
        THROW 52601, N'The inventory and accounting foundations are required.', 1;

    IF COL_LENGTH(N'dbo.FabricConsumptionSources', N'ReadyMadeProductionOrderItemId') IS NOT NULL
       OR COL_LENGTH(N'dbo.FabricConsumptionSources', N'ReadyMadeProductionOrderPieceInstanceId') IS NOT NULL
       OR OBJECT_ID(N'dbo.TailoringCostPostings', N'U') IS NOT NULL
       OR EXISTS (SELECT 1 FROM dbo.AccountingEventDefinitions WHERE AccountingEventType = 35 OR EventName = N'TailoringCostOfSales')
        THROW 52602, N'The fabric-code consumption contract already exists or conflicts with this migration.', 1;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.InventoryItems')
          AND name = N'IX_InventoryItems_ItemCode'
          AND is_unique = 1
    )
        THROW 52603, N'InventoryItems.ItemCode must be uniquely constrained before fabric-code consumption is enabled.', 1;

    DECLARE @costOfSalesAccountId int =
    (
        SELECT MIN(LedgerAccountId)
        FROM dbo.LedgerAccounts
        WHERE AccountCode = N'5200' AND IsActive = 1
        HAVING COUNT(*) = 1
    );
    DECLARE @workInProgressAccountId int =
    (
        SELECT MIN(LedgerAccountId)
        FROM dbo.LedgerAccounts
        WHERE AccountCode = N'1130' AND IsActive = 1
        HAVING COUNT(*) = 1
    );

    IF @costOfSalesAccountId IS NULL OR @workInProgressAccountId IS NULL
        THROW 52604, N'The approved 5200 and 1130 ledger accounts must each exist exactly once.', 1;

    IF EXISTS (SELECT 1 FROM dbo.AccountRoleMappings WHERE AccountRole IN (N'CostOfSales', N'WorkInProgress'))
        THROW 52605, N'An accounting role required by this migration already exists.', 1;

    ALTER TABLE dbo.FabricConsumptionSources
        ALTER COLUMN FabricRollId bigint NULL;
    ALTER TABLE dbo.FabricConsumptionSources
        ALTER COLUMN OrderItemId int NULL;

    ALTER TABLE dbo.FabricConsumptionSources ADD
        ReadyMadeProductionOrderItemId int NULL,
        ReadyMadeProductionOrderPieceInstanceId int NULL;

    EXEC(N'
        ALTER TABLE dbo.FabricConsumptionSources ADD
            CONSTRAINT FK_FabricConsumptionSources_ReadyMadeOrderItem
                FOREIGN KEY (ReadyMadeProductionOrderItemId)
                REFERENCES dbo.ReadyMadeProductionOrderItems(ReadyMadeProductionOrderItemId),
            CONSTRAINT FK_FabricConsumptionSources_ReadyMadePiece
                FOREIGN KEY (ReadyMadeProductionOrderPieceInstanceId)
                REFERENCES dbo.ReadyMadeProductionOrderPieceInstances(ReadyMadeProductionOrderPieceInstanceId),
            CONSTRAINT CK_FabricConsumptionSources_SourceKind CHECK
            (
                (OrderItemId IS NOT NULL
                 AND ReadyMadeProductionOrderItemId IS NULL
                 AND ReadyMadeProductionOrderPieceInstanceId IS NULL)
                OR
                (OrderItemId IS NULL
                 AND PieceId IS NULL
                 AND ReadyMadeProductionOrderItemId IS NOT NULL)
            );

        CREATE INDEX IX_FabricConsumptionSources_InventoryItem
            ON dbo.FabricConsumptionSources(InventoryItemId, ConfirmedAt);
        CREATE INDEX IX_FabricConsumptionSources_OrderItem
            ON dbo.FabricConsumptionSources(OrderItemId)
            WHERE OrderItemId IS NOT NULL;
        CREATE INDEX IX_FabricConsumptionSources_ReadyMadeOrderItem
            ON dbo.FabricConsumptionSources(ReadyMadeProductionOrderItemId)
            WHERE ReadyMadeProductionOrderItemId IS NOT NULL;
    ');

    CREATE TABLE dbo.TailoringCostPostings
    (
        TailoringCostPostingId bigint IDENTITY(1,1) NOT NULL,
        OrderId int NOT NULL,
        FabricOperationalAmount decimal(18,6) NOT NULL,
        PostingAmount decimal(18,2) NOT NULL,
        SourceOperationId uniqueidentifier NOT NULL,
        AccountingEventId bigint NULL,
        CreatedAt datetime2(7) NOT NULL
            CONSTRAINT DF_TailoringCostPostings_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_TailoringCostPostings PRIMARY KEY (TailoringCostPostingId),
        CONSTRAINT UQ_TailoringCostPostings_Order UNIQUE (OrderId),
        CONSTRAINT UQ_TailoringCostPostings_SourceOperation UNIQUE (SourceOperationId),
        CONSTRAINT FK_TailoringCostPostings_Order
            FOREIGN KEY (OrderId) REFERENCES dbo.Orders(OrderID),
        CONSTRAINT FK_TailoringCostPostings_AccountingEvent
            FOREIGN KEY (AccountingEventId) REFERENCES dbo.AccountingEvents(AccountingEventId),
        CONSTRAINT CK_TailoringCostPostings_Amounts CHECK
        (
            FabricOperationalAmount > 0
            AND PostingAmount = CONVERT(decimal(18,2), ROUND(FabricOperationalAmount, 2))
            AND PostingAmount > 0
        )
    );

    CREATE UNIQUE INDEX UX_TailoringCostPostings_AccountingEvent
        ON dbo.TailoringCostPostings(AccountingEventId)
        WHERE AccountingEventId IS NOT NULL;

    INSERT dbo.AccountRoleMappings(AccountRole, LedgerAccountId, IsEnabled)
    VALUES
        (N'CostOfSales', @costOfSalesAccountId, 1),
        (N'WorkInProgress', @workInProgressAccountId, 1);

    INSERT dbo.AccountingEventDefinitions
        (AccountingEventType, EventName, SourceType, DebitAccountRole, CreditAccountRole,
         CashDirection, RequiresCashMovement, LiabilityImpact, ExpenseImpact, InventoryImpact,
         IsEnabled, IsBusinessRuntimeEnabled)
    VALUES
        (35, N'TailoringCostOfSales', N'TailoringCostPosting', N'CostOfSales', N'WorkInProgress',
         NULL, 0, 0, 1, 1, 1, 0);

    ALTER TABLE dbo.AccountingEvents DROP CONSTRAINT CK_AccountingEvents_Type;
    ALTER TABLE dbo.AccountingEvents DROP CONSTRAINT CK_AccountingEvents_SourceCardinality;

    ALTER TABLE dbo.AccountingEvents ADD
        CONSTRAINT CK_AccountingEvents_Type CHECK
        (
            AccountingEventType BETWEEN 1 AND 13
            OR AccountingEventType BETWEEN 20 AND 35
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
            (AccountingEventType IN (34, 35)
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
 IF @amount IS NULL OR (@originalType NOT BETWEEN 20 AND 32 AND @originalType NOT IN(5,7,8,9,11,13,35)) OR @originalStatus<>N''Posted'' THROW 51944,N''The original event cannot be reversed.'',1;
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

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
