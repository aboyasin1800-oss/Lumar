SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() NOT IN (N'LUMAR_ERP_TEST', N'LUMAR_ERP')
    THROW 51500, N'This migration is restricted to LUMAR_ERP_TEST and LUMAR_ERP.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.ReadyMadeSaleCostPostings', N'U') IS NOT NULL
       OR OBJECT_ID(N'dbo.ImportedReadyMadeInventoryReceipts', N'U') IS NOT NULL
       OR OBJECT_ID(N'dbo.ImportedReadyMadeSaleCostPostings', N'U') IS NOT NULL
        THROW 51501, N'The ready-made and imported accounting sources already exist.', 1;

    IF COL_LENGTH(N'dbo.AccountingEvents', N'ReadyMadeSaleCostPostingId') IS NOT NULL
       OR COL_LENGTH(N'dbo.AccountingEvents', N'ImportedReadyMadeInventoryReceiptId') IS NOT NULL
       OR COL_LENGTH(N'dbo.AccountingEvents', N'ImportedReadyMadeSaleCostPostingId') IS NOT NULL
       OR COL_LENGTH(N'dbo.InventoryTransactions', N'ReadyMadeInventoryProductId') IS NOT NULL
       OR COL_LENGTH(N'dbo.InventoryTransactions', N'ImportedReadyMadeInventoryReceiptId') IS NOT NULL
       OR COL_LENGTH(N'dbo.InventoryTransactions', N'ImportedReadyMadeSaleCostPostingId') IS NOT NULL
        THROW 51502, N'One or more Gate 2 columns already exist.', 1;

    IF EXISTS (SELECT 1 FROM dbo.LedgerAccounts WITH (UPDLOCK, HOLDLOCK) WHERE AccountCode = N'1103')
        THROW 51503, N'Ledger account 1103 is already in use.', 1;

    IF EXISTS (SELECT 1 FROM dbo.AccountingEvents WITH (UPDLOCK, HOLDLOCK) WHERE AccountingEventType IN (11, 12, 13))
        THROW 51504, N'One or more Gate 2 accounting event types are already in use.', 1;

    IF OBJECT_ID(N'dbo.usp_PostAccountingEvent', N'P') IS NULL
        THROW 51505, N'The official accounting event writer is missing.', 1;

    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.LedgerAccounts
        WHERE AccountCode IN (N'2100', N'1110', N'1130', N'5200')
          AND IsActive = 1
        GROUP BY AccountCode
        HAVING COUNT(*) = 1
    )
        THROW 51506, N'One or more approved Gate 2 ledger accounts are missing or duplicated.', 1;

    INSERT INTO dbo.LedgerAccounts (AccountCode, AccountName, AccountType, IsActive, CreatedAt)
    VALUES (N'1103', N'Imported Ready-Made Inventory', N'Asset', 1, SYSUTCDATETIME());

    CREATE TABLE dbo.ReadyMadeSaleCostPostings
    (
        ReadyMadeSaleCostPostingId bigint IDENTITY(1,1) NOT NULL,
        OrderId int NOT NULL,
        OrderItemId int NOT NULL,
        ReadyMadeInventoryProductId int NOT NULL,
        Quantity decimal(18,6) NOT NULL,
        OfficialUnitCost decimal(18,6) NOT NULL,
        OperationalAmount decimal(18,6) NOT NULL,
        PostingAmount decimal(18,2) NOT NULL,
        SourceOperationId uniqueidentifier NOT NULL,
        AccountingEventId bigint NULL,
        InventoryTransactionId int NULL,
        CreatedAt datetime2(7) NOT NULL
            CONSTRAINT DF_ReadyMadeSaleCostPostings_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_ReadyMadeSaleCostPostings PRIMARY KEY (ReadyMadeSaleCostPostingId),
        CONSTRAINT UQ_ReadyMadeSaleCostPostings_SourceOperation UNIQUE (SourceOperationId),
        CONSTRAINT UQ_ReadyMadeSaleCostPostings_OrderItemProduct UNIQUE (OrderItemId, ReadyMadeInventoryProductId),
        CONSTRAINT FK_ReadyMadeSaleCostPostings_Order
            FOREIGN KEY (OrderId) REFERENCES dbo.Orders(OrderID),
        CONSTRAINT FK_ReadyMadeSaleCostPostings_OrderItem
            FOREIGN KEY (OrderItemId) REFERENCES dbo.OrderItems(OrderItemID),
        CONSTRAINT FK_ReadyMadeSaleCostPostings_Product
            FOREIGN KEY (ReadyMadeInventoryProductId) REFERENCES dbo.ReadyMadeInventoryProducts(ReadyMadeInventoryProductId),
        CONSTRAINT CK_ReadyMadeSaleCostPostings_Amounts CHECK
        (
            Quantity = CONVERT(decimal(18,6), 1)
            AND OfficialUnitCost > 0
            AND OperationalAmount = CONVERT(decimal(18,6), Quantity * OfficialUnitCost)
            AND PostingAmount = CONVERT(decimal(18,2), ROUND(OperationalAmount, 2))
            AND PostingAmount > 0
        )
    );

    CREATE TABLE dbo.ImportedReadyMadeInventoryReceipts
    (
        ImportedReadyMadeInventoryReceiptId bigint IDENTITY(1,1) NOT NULL,
        ImportedReadyMadeProductId int NOT NULL,
        QuantityReceived decimal(18,6) NOT NULL,
        OfficialUnitCost decimal(18,6) NOT NULL,
        OperationalAmount decimal(18,6) NOT NULL,
        PostingAmount decimal(18,2) NOT NULL,
        OpposingLedgerAccountId int NOT NULL,
        SourceOperationId uniqueidentifier NOT NULL,
        AccountingEventId bigint NULL,
        InventoryTransactionId int NULL,
        CreatedAt datetime2(7) NOT NULL
            CONSTRAINT DF_ImportedReadyMadeInventoryReceipts_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_ImportedReadyMadeInventoryReceipts PRIMARY KEY (ImportedReadyMadeInventoryReceiptId),
        CONSTRAINT UQ_ImportedReadyMadeInventoryReceipts_SourceOperation UNIQUE (SourceOperationId),
        CONSTRAINT FK_ImportedReadyMadeInventoryReceipts_Product
            FOREIGN KEY (ImportedReadyMadeProductId) REFERENCES dbo.ImportedReadyMadeProducts(ImportedReadyMadeProductId),
        CONSTRAINT FK_ImportedReadyMadeInventoryReceipts_OpposingAccount
            FOREIGN KEY (OpposingLedgerAccountId) REFERENCES dbo.LedgerAccounts(LedgerAccountId),
        CONSTRAINT CK_ImportedReadyMadeInventoryReceipts_Amounts CHECK
        (
            QuantityReceived > 0
            AND OfficialUnitCost > 0
            AND OperationalAmount = CONVERT(decimal(18,6), QuantityReceived * OfficialUnitCost)
            AND PostingAmount = CONVERT(decimal(18,2), ROUND(OperationalAmount, 2))
            AND PostingAmount > 0
        )
    );

    CREATE TABLE dbo.ImportedReadyMadeSaleCostPostings
    (
        ImportedReadyMadeSaleCostPostingId bigint IDENTITY(1,1) NOT NULL,
        OrderId int NOT NULL,
        OrderItemId int NOT NULL,
        ImportedReadyMadeProductId int NOT NULL,
        QuantitySold decimal(18,6) NOT NULL,
        OfficialUnitCost decimal(18,6) NOT NULL,
        OperationalAmount decimal(18,6) NOT NULL,
        PostingAmount decimal(18,2) NOT NULL,
        SourceOperationId uniqueidentifier NOT NULL,
        AccountingEventId bigint NULL,
        InventoryTransactionId int NULL,
        CreatedAt datetime2(7) NOT NULL
            CONSTRAINT DF_ImportedReadyMadeSaleCostPostings_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_ImportedReadyMadeSaleCostPostings PRIMARY KEY (ImportedReadyMadeSaleCostPostingId),
        CONSTRAINT UQ_ImportedReadyMadeSaleCostPostings_SourceOperation UNIQUE (SourceOperationId),
        CONSTRAINT UQ_ImportedReadyMadeSaleCostPostings_OrderItemProduct UNIQUE (OrderItemId, ImportedReadyMadeProductId),
        CONSTRAINT FK_ImportedReadyMadeSaleCostPostings_Order
            FOREIGN KEY (OrderId) REFERENCES dbo.Orders(OrderID),
        CONSTRAINT FK_ImportedReadyMadeSaleCostPostings_OrderItem
            FOREIGN KEY (OrderItemId) REFERENCES dbo.OrderItems(OrderItemID),
        CONSTRAINT FK_ImportedReadyMadeSaleCostPostings_Product
            FOREIGN KEY (ImportedReadyMadeProductId) REFERENCES dbo.ImportedReadyMadeProducts(ImportedReadyMadeProductId),
        CONSTRAINT CK_ImportedReadyMadeSaleCostPostings_Amounts CHECK
        (
            QuantitySold > 0
            AND OfficialUnitCost > 0
            AND OperationalAmount = CONVERT(decimal(18,6), QuantitySold * OfficialUnitCost)
            AND PostingAmount = CONVERT(decimal(18,2), ROUND(OperationalAmount, 2))
            AND PostingAmount > 0
        )
    );

    ALTER TABLE dbo.AccountingEvents
        ADD ReadyMadeSaleCostPostingId bigint NULL,
            ImportedReadyMadeInventoryReceiptId bigint NULL,
            ImportedReadyMadeSaleCostPostingId bigint NULL;

    ALTER TABLE dbo.InventoryTransactions
        ADD ReadyMadeInventoryProductId int NULL,
            ImportedReadyMadeInventoryReceiptId bigint NULL,
            ImportedReadyMadeSaleCostPostingId bigint NULL;

    ALTER TABLE dbo.AccountingEvents
        ADD CONSTRAINT FK_AccountingEvents_ReadyMadeSaleCostPosting
            FOREIGN KEY (ReadyMadeSaleCostPostingId)
            REFERENCES dbo.ReadyMadeSaleCostPostings(ReadyMadeSaleCostPostingId),
            CONSTRAINT FK_AccountingEvents_ImportedReadyMadeInventoryReceipt
            FOREIGN KEY (ImportedReadyMadeInventoryReceiptId)
            REFERENCES dbo.ImportedReadyMadeInventoryReceipts(ImportedReadyMadeInventoryReceiptId),
            CONSTRAINT FK_AccountingEvents_ImportedReadyMadeSaleCostPosting
            FOREIGN KEY (ImportedReadyMadeSaleCostPostingId)
            REFERENCES dbo.ImportedReadyMadeSaleCostPostings(ImportedReadyMadeSaleCostPostingId);

    ALTER TABLE dbo.InventoryTransactions
        ADD CONSTRAINT FK_InventoryTransactions_ReadyMadeInventoryProduct
            FOREIGN KEY (ReadyMadeInventoryProductId)
            REFERENCES dbo.ReadyMadeInventoryProducts(ReadyMadeInventoryProductId),
            CONSTRAINT FK_InventoryTransactions_ImportedReadyMadeInventoryReceipt
            FOREIGN KEY (ImportedReadyMadeInventoryReceiptId)
            REFERENCES dbo.ImportedReadyMadeInventoryReceipts(ImportedReadyMadeInventoryReceiptId),
            CONSTRAINT FK_InventoryTransactions_ImportedReadyMadeSaleCostPosting
            FOREIGN KEY (ImportedReadyMadeSaleCostPostingId)
            REFERENCES dbo.ImportedReadyMadeSaleCostPostings(ImportedReadyMadeSaleCostPostingId);

    ALTER TABLE dbo.ReadyMadeSaleCostPostings
        ADD CONSTRAINT FK_ReadyMadeSaleCostPostings_AccountingEvent
            FOREIGN KEY (AccountingEventId) REFERENCES dbo.AccountingEvents(AccountingEventId),
            CONSTRAINT FK_ReadyMadeSaleCostPostings_InventoryTransaction
            FOREIGN KEY (InventoryTransactionId) REFERENCES dbo.InventoryTransactions(TransactionID);

    ALTER TABLE dbo.ImportedReadyMadeInventoryReceipts
        ADD CONSTRAINT FK_ImportedReadyMadeInventoryReceipts_AccountingEvent
            FOREIGN KEY (AccountingEventId) REFERENCES dbo.AccountingEvents(AccountingEventId),
            CONSTRAINT FK_ImportedReadyMadeInventoryReceipts_InventoryTransaction
            FOREIGN KEY (InventoryTransactionId) REFERENCES dbo.InventoryTransactions(TransactionID);

    ALTER TABLE dbo.ImportedReadyMadeSaleCostPostings
        ADD CONSTRAINT FK_ImportedReadyMadeSaleCostPostings_AccountingEvent
            FOREIGN KEY (AccountingEventId) REFERENCES dbo.AccountingEvents(AccountingEventId),
            CONSTRAINT FK_ImportedReadyMadeSaleCostPostings_InventoryTransaction
            FOREIGN KEY (InventoryTransactionId) REFERENCES dbo.InventoryTransactions(TransactionID);

    EXEC(N'
        ALTER TABLE dbo.AccountingEvents DROP CONSTRAINT CK_AccountingEvents_Type;
        ALTER TABLE dbo.AccountingEvents DROP CONSTRAINT CK_AccountingEvents_SourceCardinality;
        ALTER TABLE dbo.AccountingEvents ADD
            CONSTRAINT CK_AccountingEvents_Type
                CHECK (AccountingEventType BETWEEN 1 AND 13),
            CONSTRAINT CK_AccountingEvents_SourceCardinality
                CHECK
                (
                       (AccountingEventType IN (1, 2)
                        AND PaymentId IS NOT NULL AND OrderId IS NULL
                        AND MeasurementCardPrintHistoryId IS NULL AND ReadyMadeInventoryProductId IS NULL
                        AND CustomerAdvanceApplicationId IS NULL AND InventoryReceiptPostingId IS NULL
                        AND FabricConsumptionSourceId IS NULL AND ProductionMaterialConsumptionId IS NULL
                        AND ReadyMadeSaleCostPostingId IS NULL AND ImportedReadyMadeInventoryReceiptId IS NULL
                        AND ImportedReadyMadeSaleCostPostingId IS NULL)
                    OR (AccountingEventType = 3
                        AND PaymentId IS NULL AND OrderId IS NOT NULL
                        AND MeasurementCardPrintHistoryId IS NULL AND ReadyMadeInventoryProductId IS NULL
                        AND CustomerAdvanceApplicationId IS NULL AND InventoryReceiptPostingId IS NULL
                        AND FabricConsumptionSourceId IS NULL AND ProductionMaterialConsumptionId IS NULL
                        AND ReadyMadeSaleCostPostingId IS NULL AND ImportedReadyMadeInventoryReceiptId IS NULL
                        AND ImportedReadyMadeSaleCostPostingId IS NULL)
                    OR (AccountingEventType = 4
                        AND PaymentId IS NULL AND OrderId IS NULL
                        AND MeasurementCardPrintHistoryId IS NOT NULL AND ReadyMadeInventoryProductId IS NULL
                        AND CustomerAdvanceApplicationId IS NULL AND InventoryReceiptPostingId IS NULL
                        AND FabricConsumptionSourceId IS NULL AND ProductionMaterialConsumptionId IS NULL
                        AND ReadyMadeSaleCostPostingId IS NULL AND ImportedReadyMadeInventoryReceiptId IS NULL
                        AND ImportedReadyMadeSaleCostPostingId IS NULL)
                    OR (AccountingEventType = 5
                        AND PaymentId IS NULL AND OrderId IS NULL
                        AND MeasurementCardPrintHistoryId IS NULL AND ReadyMadeInventoryProductId IS NOT NULL
                        AND CustomerAdvanceApplicationId IS NULL AND InventoryReceiptPostingId IS NULL
                        AND FabricConsumptionSourceId IS NULL AND ProductionMaterialConsumptionId IS NULL
                        AND ReadyMadeSaleCostPostingId IS NULL AND ImportedReadyMadeInventoryReceiptId IS NULL
                        AND ImportedReadyMadeSaleCostPostingId IS NULL)
                    OR (AccountingEventType = 6
                        AND PaymentId IS NULL AND OrderId IS NULL
                        AND MeasurementCardPrintHistoryId IS NULL AND ReadyMadeInventoryProductId IS NULL
                        AND CustomerAdvanceApplicationId IS NOT NULL AND InventoryReceiptPostingId IS NULL
                        AND FabricConsumptionSourceId IS NULL AND ProductionMaterialConsumptionId IS NULL
                        AND ReadyMadeSaleCostPostingId IS NULL AND ImportedReadyMadeInventoryReceiptId IS NULL
                        AND ImportedReadyMadeSaleCostPostingId IS NULL)
                    OR (AccountingEventType IN (7, 9)
                        AND PaymentId IS NULL AND OrderId IS NULL
                        AND MeasurementCardPrintHistoryId IS NULL AND ReadyMadeInventoryProductId IS NULL
                        AND CustomerAdvanceApplicationId IS NULL AND InventoryReceiptPostingId IS NOT NULL
                        AND FabricConsumptionSourceId IS NULL AND ProductionMaterialConsumptionId IS NULL
                        AND ReadyMadeSaleCostPostingId IS NULL AND ImportedReadyMadeInventoryReceiptId IS NULL
                        AND ImportedReadyMadeSaleCostPostingId IS NULL)
                    OR (AccountingEventType = 8
                        AND PaymentId IS NULL AND OrderId IS NULL
                        AND MeasurementCardPrintHistoryId IS NULL AND ReadyMadeInventoryProductId IS NULL
                        AND CustomerAdvanceApplicationId IS NULL AND InventoryReceiptPostingId IS NULL
                        AND FabricConsumptionSourceId IS NOT NULL AND ProductionMaterialConsumptionId IS NULL
                        AND ReadyMadeSaleCostPostingId IS NULL AND ImportedReadyMadeInventoryReceiptId IS NULL
                        AND ImportedReadyMadeSaleCostPostingId IS NULL)
                    OR (AccountingEventType = 10
                        AND PaymentId IS NULL AND OrderId IS NULL
                        AND MeasurementCardPrintHistoryId IS NULL AND ReadyMadeInventoryProductId IS NULL
                        AND CustomerAdvanceApplicationId IS NULL AND InventoryReceiptPostingId IS NULL
                        AND FabricConsumptionSourceId IS NULL AND ProductionMaterialConsumptionId IS NOT NULL
                        AND ReadyMadeSaleCostPostingId IS NULL AND ImportedReadyMadeInventoryReceiptId IS NULL
                        AND ImportedReadyMadeSaleCostPostingId IS NULL)
                    OR (AccountingEventType = 11
                        AND PaymentId IS NULL AND OrderId IS NULL
                        AND MeasurementCardPrintHistoryId IS NULL AND ReadyMadeInventoryProductId IS NULL
                        AND CustomerAdvanceApplicationId IS NULL AND InventoryReceiptPostingId IS NULL
                        AND FabricConsumptionSourceId IS NULL AND ProductionMaterialConsumptionId IS NULL
                        AND ReadyMadeSaleCostPostingId IS NOT NULL AND ImportedReadyMadeInventoryReceiptId IS NULL
                        AND ImportedReadyMadeSaleCostPostingId IS NULL)
                    OR (AccountingEventType = 12
                        AND PaymentId IS NULL AND OrderId IS NULL
                        AND MeasurementCardPrintHistoryId IS NULL AND ReadyMadeInventoryProductId IS NULL
                        AND CustomerAdvanceApplicationId IS NULL AND InventoryReceiptPostingId IS NULL
                        AND FabricConsumptionSourceId IS NULL AND ProductionMaterialConsumptionId IS NULL
                        AND ReadyMadeSaleCostPostingId IS NULL AND ImportedReadyMadeInventoryReceiptId IS NOT NULL
                        AND ImportedReadyMadeSaleCostPostingId IS NULL)
                    OR (AccountingEventType = 13
                        AND PaymentId IS NULL AND OrderId IS NULL
                        AND MeasurementCardPrintHistoryId IS NULL AND ReadyMadeInventoryProductId IS NULL
                        AND CustomerAdvanceApplicationId IS NULL AND InventoryReceiptPostingId IS NULL
                        AND FabricConsumptionSourceId IS NULL AND ProductionMaterialConsumptionId IS NULL
                        AND ReadyMadeSaleCostPostingId IS NULL AND ImportedReadyMadeInventoryReceiptId IS NULL
                        AND ImportedReadyMadeSaleCostPostingId IS NOT NULL)
                );

        CREATE UNIQUE INDEX UX_ReadyMadeSaleCostPostings_AccountingEvent
            ON dbo.ReadyMadeSaleCostPostings(AccountingEventId)
            WHERE AccountingEventId IS NOT NULL;
        CREATE UNIQUE INDEX UX_ReadyMadeSaleCostPostings_InventoryTransaction
            ON dbo.ReadyMadeSaleCostPostings(InventoryTransactionId)
            WHERE InventoryTransactionId IS NOT NULL;
        CREATE UNIQUE INDEX UX_ImportedReadyMadeInventoryReceipts_AccountingEvent
            ON dbo.ImportedReadyMadeInventoryReceipts(AccountingEventId)
            WHERE AccountingEventId IS NOT NULL;
        CREATE UNIQUE INDEX UX_ImportedReadyMadeInventoryReceipts_InventoryTransaction
            ON dbo.ImportedReadyMadeInventoryReceipts(InventoryTransactionId)
            WHERE InventoryTransactionId IS NOT NULL;
        CREATE UNIQUE INDEX UX_ImportedReadyMadeSaleCostPostings_AccountingEvent
            ON dbo.ImportedReadyMadeSaleCostPostings(AccountingEventId)
            WHERE AccountingEventId IS NOT NULL;
        CREATE UNIQUE INDEX UX_ImportedReadyMadeSaleCostPostings_InventoryTransaction
            ON dbo.ImportedReadyMadeSaleCostPostings(InventoryTransactionId)
            WHERE InventoryTransactionId IS NOT NULL;
        CREATE UNIQUE INDEX UX_InventoryTransactions_ReadyMadeInventoryProduct_Receipt
            ON dbo.InventoryTransactions(ReadyMadeInventoryProductId)
            WHERE ReadyMadeInventoryProductId IS NOT NULL;
        CREATE UNIQUE INDEX UX_InventoryTransactions_ImportedReceipt
            ON dbo.InventoryTransactions(ImportedReadyMadeInventoryReceiptId)
            WHERE ImportedReadyMadeInventoryReceiptId IS NOT NULL;
        CREATE UNIQUE INDEX UX_InventoryTransactions_ImportedSaleCost
            ON dbo.InventoryTransactions(ImportedReadyMadeSaleCostPostingId)
            WHERE ImportedReadyMadeSaleCostPostingId IS NOT NULL;
        CREATE UNIQUE INDEX UX_AccountingEvents_ReadyMadeSaleCostPosting
            ON dbo.AccountingEvents(ReadyMadeSaleCostPostingId)
            WHERE ReadyMadeSaleCostPostingId IS NOT NULL;
        CREATE UNIQUE INDEX UX_AccountingEvents_ImportedReadyMadeInventoryReceipt
            ON dbo.AccountingEvents(ImportedReadyMadeInventoryReceiptId)
            WHERE ImportedReadyMadeInventoryReceiptId IS NOT NULL;
        CREATE UNIQUE INDEX UX_AccountingEvents_ImportedReadyMadeSaleCostPosting
            ON dbo.AccountingEvents(ImportedReadyMadeSaleCostPostingId)
            WHERE ImportedReadyMadeSaleCostPostingId IS NOT NULL;');

    EXEC(N'
CREATE OR ALTER PROCEDURE dbo.usp_PostAccountingEvent
    @AccountingEventType tinyint,
    @PostingAmount decimal(18,2),
    @PaymentId int = NULL,
    @OrderId int = NULL,
    @MeasurementCardPrintHistoryId int = NULL,
    @ReadyMadeInventoryProductId int = NULL,
    @CustomerAdvanceApplicationId bigint = NULL,
    @InventoryReceiptPostingId bigint = NULL,
    @FabricConsumptionSourceId bigint = NULL,
    @ProductionMaterialConsumptionId int = NULL,
    @ReadyMadeSaleCostPostingId bigint = NULL,
    @ImportedReadyMadeInventoryReceiptId bigint = NULL,
    @ImportedReadyMadeSaleCostPostingId bigint = NULL,
    @ReferenceNumber nvarchar(200) = NULL,
    @Description nvarchar(1000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @@TRANCOUNT = 0
        THROW 51510, N''Accounting events require a caller-owned SQL transaction.'', 1;

    IF @AccountingEventType NOT BETWEEN 1 AND 13 OR @PostingAmount <= 0
        THROW 51511, N''Unsupported accounting event type or posting amount.'', 1;

    IF NOT
    (
           (@AccountingEventType IN (1,2) AND @PaymentId IS NOT NULL AND @OrderId IS NULL AND @MeasurementCardPrintHistoryId IS NULL AND @ReadyMadeInventoryProductId IS NULL AND @CustomerAdvanceApplicationId IS NULL AND @InventoryReceiptPostingId IS NULL AND @FabricConsumptionSourceId IS NULL AND @ProductionMaterialConsumptionId IS NULL AND @ReadyMadeSaleCostPostingId IS NULL AND @ImportedReadyMadeInventoryReceiptId IS NULL AND @ImportedReadyMadeSaleCostPostingId IS NULL)
        OR (@AccountingEventType = 3 AND @PaymentId IS NULL AND @OrderId IS NOT NULL AND @MeasurementCardPrintHistoryId IS NULL AND @ReadyMadeInventoryProductId IS NULL AND @CustomerAdvanceApplicationId IS NULL AND @InventoryReceiptPostingId IS NULL AND @FabricConsumptionSourceId IS NULL AND @ProductionMaterialConsumptionId IS NULL AND @ReadyMadeSaleCostPostingId IS NULL AND @ImportedReadyMadeInventoryReceiptId IS NULL AND @ImportedReadyMadeSaleCostPostingId IS NULL)
        OR (@AccountingEventType = 4 AND @PaymentId IS NULL AND @OrderId IS NULL AND @MeasurementCardPrintHistoryId IS NOT NULL AND @ReadyMadeInventoryProductId IS NULL AND @CustomerAdvanceApplicationId IS NULL AND @InventoryReceiptPostingId IS NULL AND @FabricConsumptionSourceId IS NULL AND @ProductionMaterialConsumptionId IS NULL AND @ReadyMadeSaleCostPostingId IS NULL AND @ImportedReadyMadeInventoryReceiptId IS NULL AND @ImportedReadyMadeSaleCostPostingId IS NULL)
        OR (@AccountingEventType = 5 AND @PaymentId IS NULL AND @OrderId IS NULL AND @MeasurementCardPrintHistoryId IS NULL AND @ReadyMadeInventoryProductId IS NOT NULL AND @CustomerAdvanceApplicationId IS NULL AND @InventoryReceiptPostingId IS NULL AND @FabricConsumptionSourceId IS NULL AND @ProductionMaterialConsumptionId IS NULL AND @ReadyMadeSaleCostPostingId IS NULL AND @ImportedReadyMadeInventoryReceiptId IS NULL AND @ImportedReadyMadeSaleCostPostingId IS NULL)
        OR (@AccountingEventType = 6 AND @PaymentId IS NULL AND @OrderId IS NULL AND @MeasurementCardPrintHistoryId IS NULL AND @ReadyMadeInventoryProductId IS NULL AND @CustomerAdvanceApplicationId IS NOT NULL AND @InventoryReceiptPostingId IS NULL AND @FabricConsumptionSourceId IS NULL AND @ProductionMaterialConsumptionId IS NULL AND @ReadyMadeSaleCostPostingId IS NULL AND @ImportedReadyMadeInventoryReceiptId IS NULL AND @ImportedReadyMadeSaleCostPostingId IS NULL)
        OR (@AccountingEventType IN (7,9) AND @PaymentId IS NULL AND @OrderId IS NULL AND @MeasurementCardPrintHistoryId IS NULL AND @ReadyMadeInventoryProductId IS NULL AND @CustomerAdvanceApplicationId IS NULL AND @InventoryReceiptPostingId IS NOT NULL AND @FabricConsumptionSourceId IS NULL AND @ProductionMaterialConsumptionId IS NULL AND @ReadyMadeSaleCostPostingId IS NULL AND @ImportedReadyMadeInventoryReceiptId IS NULL AND @ImportedReadyMadeSaleCostPostingId IS NULL)
        OR (@AccountingEventType = 8 AND @PaymentId IS NULL AND @OrderId IS NULL AND @MeasurementCardPrintHistoryId IS NULL AND @ReadyMadeInventoryProductId IS NULL AND @CustomerAdvanceApplicationId IS NULL AND @InventoryReceiptPostingId IS NULL AND @FabricConsumptionSourceId IS NOT NULL AND @ProductionMaterialConsumptionId IS NULL AND @ReadyMadeSaleCostPostingId IS NULL AND @ImportedReadyMadeInventoryReceiptId IS NULL AND @ImportedReadyMadeSaleCostPostingId IS NULL)
        OR (@AccountingEventType = 10 AND @PaymentId IS NULL AND @OrderId IS NULL AND @MeasurementCardPrintHistoryId IS NULL AND @ReadyMadeInventoryProductId IS NULL AND @CustomerAdvanceApplicationId IS NULL AND @InventoryReceiptPostingId IS NULL AND @FabricConsumptionSourceId IS NULL AND @ProductionMaterialConsumptionId IS NOT NULL AND @ReadyMadeSaleCostPostingId IS NULL AND @ImportedReadyMadeInventoryReceiptId IS NULL AND @ImportedReadyMadeSaleCostPostingId IS NULL)
        OR (@AccountingEventType = 11 AND @PaymentId IS NULL AND @OrderId IS NULL AND @MeasurementCardPrintHistoryId IS NULL AND @ReadyMadeInventoryProductId IS NULL AND @CustomerAdvanceApplicationId IS NULL AND @InventoryReceiptPostingId IS NULL AND @FabricConsumptionSourceId IS NULL AND @ProductionMaterialConsumptionId IS NULL AND @ReadyMadeSaleCostPostingId IS NOT NULL AND @ImportedReadyMadeInventoryReceiptId IS NULL AND @ImportedReadyMadeSaleCostPostingId IS NULL)
        OR (@AccountingEventType = 12 AND @PaymentId IS NULL AND @OrderId IS NULL AND @MeasurementCardPrintHistoryId IS NULL AND @ReadyMadeInventoryProductId IS NULL AND @CustomerAdvanceApplicationId IS NULL AND @InventoryReceiptPostingId IS NULL AND @FabricConsumptionSourceId IS NULL AND @ProductionMaterialConsumptionId IS NULL AND @ReadyMadeSaleCostPostingId IS NULL AND @ImportedReadyMadeInventoryReceiptId IS NOT NULL AND @ImportedReadyMadeSaleCostPostingId IS NULL)
        OR (@AccountingEventType = 13 AND @PaymentId IS NULL AND @OrderId IS NULL AND @MeasurementCardPrintHistoryId IS NULL AND @ReadyMadeInventoryProductId IS NULL AND @CustomerAdvanceApplicationId IS NULL AND @InventoryReceiptPostingId IS NULL AND @FabricConsumptionSourceId IS NULL AND @ProductionMaterialConsumptionId IS NULL AND @ReadyMadeSaleCostPostingId IS NULL AND @ImportedReadyMadeInventoryReceiptId IS NULL AND @ImportedReadyMadeSaleCostPostingId IS NOT NULL)
    )
        THROW 51512, N''The accounting event source does not match its event type.'', 1;

    DECLARE @existingAccountingEventId bigint;
    DECLARE @existingPostingAmount decimal(18,2);
    SELECT TOP (1)
        @existingAccountingEventId = AccountingEventId,
        @existingPostingAmount = PostingAmount
    FROM dbo.AccountingEvents WITH (UPDLOCK, HOLDLOCK)
    WHERE (@AccountingEventType IN (1,2) AND PaymentId = @PaymentId)
       OR (@AccountingEventType = 3 AND OrderId = @OrderId)
       OR (@AccountingEventType = 4 AND MeasurementCardPrintHistoryId = @MeasurementCardPrintHistoryId)
       OR (@AccountingEventType = 5 AND ReadyMadeInventoryProductId = @ReadyMadeInventoryProductId)
       OR (@AccountingEventType = 6 AND CustomerAdvanceApplicationId = @CustomerAdvanceApplicationId)
       OR (@AccountingEventType IN (7,9) AND InventoryReceiptPostingId = @InventoryReceiptPostingId)
       OR (@AccountingEventType = 8 AND FabricConsumptionSourceId = @FabricConsumptionSourceId)
       OR (@AccountingEventType = 10 AND ProductionMaterialConsumptionId = @ProductionMaterialConsumptionId)
       OR (@AccountingEventType = 11 AND ReadyMadeSaleCostPostingId = @ReadyMadeSaleCostPostingId)
       OR (@AccountingEventType = 12 AND ImportedReadyMadeInventoryReceiptId = @ImportedReadyMadeInventoryReceiptId)
       OR (@AccountingEventType = 13 AND ImportedReadyMadeSaleCostPostingId = @ImportedReadyMadeSaleCostPostingId);

    IF @existingAccountingEventId IS NOT NULL
    BEGIN
        IF @existingPostingAmount <> @PostingAmount
            THROW 51513, N''The existing accounting event amount differs from the retry amount.'', 1;

        DECLARE @existingFinancialTransactionId int;
        DECLARE @existingJournalEntryId int;
        SELECT @existingFinancialTransactionId = FinancialTransactionId
        FROM dbo.FinancialTransactions WITH (UPDLOCK, HOLDLOCK)
        WHERE AccountingEventId = @existingAccountingEventId;
        SELECT @existingJournalEntryId = JournalEntryId
        FROM dbo.JournalEntries WITH (UPDLOCK, HOLDLOCK)
        WHERE AccountingEventId = @existingAccountingEventId;
        IF @existingFinancialTransactionId IS NULL OR @existingJournalEntryId IS NULL
            THROW 51514, N''The existing accounting event is incomplete.'', 1;
        SELECT @existingAccountingEventId AS AccountingEventId,
               @existingFinancialTransactionId AS FinancialTransactionId,
               @existingJournalEntryId AS JournalEntryId,
               CAST(1 AS bit) AS IsExisting;
        RETURN;
    END;

    DECLARE @transactionType nvarchar(200);
    DECLARE @debitAccountCode nvarchar(100);
    DECLARE @creditAccountCode nvarchar(100);
    DECLARE @sourcePostingAmount decimal(18,2);
    DECLARE @sourceOperationalAmount decimal(18,6);
    DECLARE @receiptClass tinyint;
    DECLARE @sourceOpposingLedgerAccountId int;

    IF @AccountingEventType IN (7,9)
    BEGIN
        SELECT
            @sourcePostingAmount = rp.PostingAmount,
            @sourceOperationalAmount = rp.OperationalAmount,
            @receiptClass = rp.InventoryClassId,
            @sourceOpposingLedgerAccountId = rp.OpposingLedgerAccountId,
            @creditAccountCode = la.AccountCode
        FROM dbo.InventoryReceiptPostings rp WITH (UPDLOCK, HOLDLOCK)
        INNER JOIN dbo.LedgerAccounts la WITH (UPDLOCK, HOLDLOCK)
            ON la.LedgerAccountId = rp.OpposingLedgerAccountId
           AND la.IsActive = 1
        WHERE rp.InventoryReceiptPostingId = @InventoryReceiptPostingId;

        IF @sourcePostingAmount IS NULL
            THROW 51515, N''The inventory receipt posting source is missing or inactive.'', 1;
        IF @sourcePostingAmount <> @PostingAmount
            THROW 51516, N''The inventory receipt posting amount does not match.'', 1;
        IF CONVERT(decimal(18,2), ROUND(@sourceOperationalAmount, 2)) <> @PostingAmount
            THROW 51517, N''The inventory receipt operational amount does not match.'', 1;
        IF (@AccountingEventType = 7 AND @receiptClass <> 1)
           OR (@AccountingEventType = 9 AND @receiptClass <> 2)
            THROW 51518, N''The inventory receipt class does not match the event type.'', 1;

        SET @debitAccountCode = CASE WHEN @AccountingEventType = 7 THEN N''1101'' ELSE N''1102'' END;
        SET @transactionType = CASE WHEN @AccountingEventType = 7 THEN N''FabricInventoryReceived'' ELSE N''ConsumableInventoryReceived'' END;
    END;
    ELSE IF @AccountingEventType = 8
    BEGIN
        SELECT
            @sourcePostingAmount = PostingAmount,
            @sourceOperationalAmount = OperationalAmount
        FROM dbo.FabricConsumptionSources WITH (UPDLOCK, HOLDLOCK)
        WHERE FabricConsumptionSourceId = @FabricConsumptionSourceId;
        IF @sourcePostingAmount IS NULL
            THROW 51519, N''The fabric consumption source is missing.'', 1;
        IF @sourcePostingAmount <> @PostingAmount
           OR CONVERT(decimal(18,2), ROUND(@sourceOperationalAmount, 2)) <> @PostingAmount
            THROW 51520, N''The fabric consumption amount does not match.'', 1;
        SET @transactionType = N''FabricInventoryConsumed'';
        SET @debitAccountCode = N''1130'';
        SET @creditAccountCode = N''1101'';
    END;
    ELSE IF @AccountingEventType = 10
    BEGIN
        SELECT
            @sourcePostingAmount = PostingAmount,
            @sourceOperationalAmount = OperationalAmount
        FROM dbo.ProductionMaterialConsumptions WITH (UPDLOCK, HOLDLOCK)
        WHERE ProductionMaterialConsumptionId = @ProductionMaterialConsumptionId;
        IF @sourcePostingAmount IS NULL
            THROW 51521, N''The consumable consumption source is missing.'', 1;
        IF @sourcePostingAmount <> @PostingAmount
           OR CONVERT(decimal(18,2), ROUND(@sourceOperationalAmount, 2)) <> @PostingAmount
            THROW 51522, N''The consumable consumption amount does not match.'', 1;
        SET @transactionType = N''ConsumableInventoryConsumed'';
        SET @debitAccountCode = N''5300'';
        SET @creditAccountCode = N''1102'';
    END;
    ELSE IF @AccountingEventType = 12
    BEGIN
        SELECT
            @sourcePostingAmount = r.PostingAmount,
            @sourceOperationalAmount = r.OperationalAmount,
            @sourceOpposingLedgerAccountId = r.OpposingLedgerAccountId,
            @creditAccountCode = la.AccountCode
        FROM dbo.ImportedReadyMadeInventoryReceipts r WITH (UPDLOCK, HOLDLOCK)
        INNER JOIN dbo.LedgerAccounts la WITH (UPDLOCK, HOLDLOCK)
            ON la.LedgerAccountId = r.OpposingLedgerAccountId
           AND la.IsActive = 1
        WHERE r.ImportedReadyMadeInventoryReceiptId = @ImportedReadyMadeInventoryReceiptId;
        IF @sourcePostingAmount IS NULL
            THROW 51523, N''The imported ready-made receipt source is missing or inactive.'', 1;
        IF @sourcePostingAmount <> @PostingAmount
           OR CONVERT(decimal(18,2), ROUND(@sourceOperationalAmount, 2)) <> @PostingAmount
            THROW 51524, N''The imported ready-made receipt amount does not match.'', 1;
        SET @transactionType = N''ImportedReadyMadeInventoryReceived'';
        SET @debitAccountCode = N''1103'';
    END;
    ELSE IF @AccountingEventType = 11
    BEGIN
        SELECT @sourcePostingAmount = PostingAmount, @sourceOperationalAmount = OperationalAmount
        FROM dbo.ReadyMadeSaleCostPostings WITH (UPDLOCK, HOLDLOCK)
        WHERE ReadyMadeSaleCostPostingId = @ReadyMadeSaleCostPostingId;
        IF @sourcePostingAmount IS NULL
            THROW 51525, N''The ready-made sale cost source is missing.'', 1;
        IF @sourcePostingAmount <> @PostingAmount
           OR CONVERT(decimal(18,2), ROUND(@sourceOperationalAmount, 2)) <> @PostingAmount
            THROW 51526, N''The ready-made sale cost amount does not match.'', 1;
        SET @transactionType = N''ReadyMadeCost'';
        SET @debitAccountCode = N''5200'';
        SET @creditAccountCode = N''1110'';
    END;
    ELSE IF @AccountingEventType = 13
    BEGIN
        SELECT @sourcePostingAmount = PostingAmount, @sourceOperationalAmount = OperationalAmount
        FROM dbo.ImportedReadyMadeSaleCostPostings WITH (UPDLOCK, HOLDLOCK)
        WHERE ImportedReadyMadeSaleCostPostingId = @ImportedReadyMadeSaleCostPostingId;
        IF @sourcePostingAmount IS NULL
            THROW 51527, N''The imported ready-made sale cost source is missing.'', 1;
        IF @sourcePostingAmount <> @PostingAmount
           OR CONVERT(decimal(18,2), ROUND(@sourceOperationalAmount, 2)) <> @PostingAmount
            THROW 51528, N''The imported ready-made sale cost amount does not match.'', 1;
        SET @transactionType = N''ImportedReadyMadeCost'';
        SET @debitAccountCode = N''5200'';
        SET @creditAccountCode = N''1103'';
    END;
    ELSE
    BEGIN
        SET @transactionType = CASE @AccountingEventType
            WHEN 1 THEN N''CustomerAdvance''
            WHEN 2 THEN N''CustomerPayment''
            WHEN 3 THEN N''RevenueRecognized''
            WHEN 4 THEN N''RevenueRecognized''
            WHEN 5 THEN N''WipToFinishedGoods''
            WHEN 6 THEN N''CustomerAdvanceApplied''
        END;
        SET @debitAccountCode = CASE @AccountingEventType
            WHEN 1 THEN N''1000''
            WHEN 2 THEN N''1000''
            WHEN 3 THEN N''1200''
            WHEN 4 THEN N''1200''
            WHEN 5 THEN N''1110''
            WHEN 6 THEN N''1160''
        END;
        SET @creditAccountCode = CASE @AccountingEventType
            WHEN 1 THEN N''1160''
            WHEN 2 THEN N''1200''
            WHEN 3 THEN N''4200''
            WHEN 4 THEN N''4200''
            WHEN 5 THEN N''1130''
            WHEN 6 THEN N''1200''
        END;
    END;

    IF @AccountingEventType IN (1,2)
    BEGIN
        DECLARE @paymentAmount decimal(18,2);
        DECLARE @paymentKind nvarchar(100);
        SELECT @paymentAmount = Amount, @paymentKind = PaymentKind
        FROM dbo.Payments WITH (UPDLOCK, HOLDLOCK)
        WHERE PaymentID = @PaymentId;
        IF @paymentAmount IS NULL OR @paymentAmount <> @PostingAmount
            THROW 51529, N''The payment source is missing or has a different amount.'', 1;
        IF (@AccountingEventType = 1 AND @paymentKind <> N''Advance'')
           OR (@AccountingEventType = 2 AND @paymentKind NOT IN (N''DebtCollection'', N''SaleCash'', N''MeasurementCardPieceSale''))
            THROW 51530, N''The payment kind is not supported by this accounting event.'', 1;
    END;

    IF @AccountingEventType = 3
       AND NOT EXISTS (SELECT 1 FROM dbo.Orders WITH (UPDLOCK, HOLDLOCK) WHERE OrderID = @OrderId)
        THROW 51531, N''The revenue order source does not exist.'', 1;

    IF @AccountingEventType = 4
       AND NOT EXISTS
       (
           SELECT 1 FROM dbo.MeasurementCardPrintHistory WITH (UPDLOCK, HOLDLOCK)
           WHERE PrintHistoryId = @MeasurementCardPrintHistoryId
             AND ReprintReasonCode = N''PieceSold''
             AND SaleAmount = @PostingAmount
       )
        THROW 51532, N''The print-sale source is missing or does not match the posting amount.'', 1;

    IF @AccountingEventType = 5
       AND NOT EXISTS
       (
           SELECT 1 FROM dbo.ReadyMadeInventoryProducts WITH (UPDLOCK, HOLDLOCK)
           WHERE ReadyMadeInventoryProductId = @ReadyMadeInventoryProductId
             AND CAST(ActualCost AS decimal(18,2)) = @PostingAmount
       )
        THROW 51533, N''The ready-made inventory source is missing or does not match the posting amount.'', 1;

    IF @AccountingEventType = 11
       AND NOT EXISTS
       (
           SELECT 1
           FROM dbo.ReadyMadeSaleCostPostings s WITH (UPDLOCK, HOLDLOCK)
           INNER JOIN dbo.OrderItems oi WITH (UPDLOCK, HOLDLOCK) ON oi.OrderItemID = s.OrderItemId AND oi.OrderID = s.OrderId
           INNER JOIN dbo.ReadyMadeInventoryProducts rip WITH (UPDLOCK, HOLDLOCK) ON rip.ReadyMadeInventoryProductId = s.ReadyMadeInventoryProductId
           WHERE s.ReadyMadeSaleCostPostingId = @ReadyMadeSaleCostPostingId
             AND oi.ReadyMadeInventoryProductId = s.ReadyMadeInventoryProductId
             AND rip.ActualCost = s.OfficialUnitCost
       )
        THROW 51534, N''The ready-made sale cost source identity is invalid.'', 1;

    IF @AccountingEventType = 12
       AND NOT EXISTS
       (
           SELECT 1 FROM dbo.ImportedReadyMadeInventoryReceipts r WITH (UPDLOCK, HOLDLOCK)
           INNER JOIN dbo.ImportedReadyMadeProducts p WITH (UPDLOCK, HOLDLOCK) ON p.ImportedReadyMadeProductId = r.ImportedReadyMadeProductId
           WHERE r.ImportedReadyMadeInventoryReceiptId = @ImportedReadyMadeInventoryReceiptId
       )
        THROW 51535, N''The imported ready-made receipt source identity is invalid.'', 1;

    IF @AccountingEventType = 13
       AND NOT EXISTS
       (
           SELECT 1
           FROM dbo.ImportedReadyMadeSaleCostPostings s WITH (UPDLOCK, HOLDLOCK)
           INNER JOIN dbo.OrderItems oi WITH (UPDLOCK, HOLDLOCK) ON oi.OrderItemID = s.OrderItemId AND oi.OrderID = s.OrderId
           INNER JOIN dbo.ImportedReadyMadeProducts p WITH (UPDLOCK, HOLDLOCK) ON p.ImportedReadyMadeProductId = s.ImportedReadyMadeProductId
           WHERE s.ImportedReadyMadeSaleCostPostingId = @ImportedReadyMadeSaleCostPostingId
             AND oi.ImportedReadyMadeProductId = s.ImportedReadyMadeProductId
             AND p.PurchasePrice = s.OfficialUnitCost
       )
        THROW 51536, N''The imported ready-made sale cost source identity is invalid.'', 1;

    DECLARE @debitLedgerAccountId int;
    DECLARE @creditLedgerAccountId int;
    SELECT TOP (1) @debitLedgerAccountId = LedgerAccountId
    FROM dbo.LedgerAccounts WITH (UPDLOCK, HOLDLOCK)
    WHERE AccountCode = @debitAccountCode AND IsActive = 1
    ORDER BY LedgerAccountId;
    SELECT TOP (1) @creditLedgerAccountId = LedgerAccountId
    FROM dbo.LedgerAccounts WITH (UPDLOCK, HOLDLOCK)
    WHERE AccountCode = @creditAccountCode AND IsActive = 1
    ORDER BY LedgerAccountId;
    IF @debitLedgerAccountId IS NULL OR @creditLedgerAccountId IS NULL
        THROW 51537, N''A required active ledger account is missing.'', 1;

    INSERT INTO dbo.AccountingEvents
    (
        AccountingEventType,
        PostingAmount,
        PaymentId,
        OrderId,
        MeasurementCardPrintHistoryId,
        ReadyMadeInventoryProductId,
        CustomerAdvanceApplicationId,
        InventoryReceiptPostingId,
        FabricConsumptionSourceId,
        ProductionMaterialConsumptionId,
        ReadyMadeSaleCostPostingId,
        ImportedReadyMadeInventoryReceiptId,
        ImportedReadyMadeSaleCostPostingId
    )
    VALUES
    (
        @AccountingEventType,
        @PostingAmount,
        @PaymentId,
        @OrderId,
        @MeasurementCardPrintHistoryId,
        @ReadyMadeInventoryProductId,
        @CustomerAdvanceApplicationId,
        @InventoryReceiptPostingId,
        @FabricConsumptionSourceId,
        @ProductionMaterialConsumptionId,
        @ReadyMadeSaleCostPostingId,
        @ImportedReadyMadeInventoryReceiptId,
        @ImportedReadyMadeSaleCostPostingId
    );

    DECLARE @accountingEventId bigint = SCOPE_IDENTITY();
    SET @ReferenceNumber = NULLIF(LTRIM(RTRIM(@ReferenceNumber)), N'''');
    SET @ReferenceNumber = COALESCE(@ReferenceNumber, CONCAT(N''AE-'', @accountingEventId));
    SET @Description = NULLIF(LTRIM(RTRIM(@Description)), N'''');
    SET @Description = COALESCE(@Description, CONCAT(N''Accounting event '', @accountingEventId));

    DECLARE @FinancialTransactionOutput TABLE (FinancialTransactionId int NOT NULL);
    DECLARE @JournalEntryOutput TABLE (JournalEntryId int NOT NULL);
    DECLARE @financialTransactionId int;
    INSERT INTO dbo.FinancialTransactions
        (ReferenceNumber, TransactionType, Amount, Description, CreatedAt, AccountingEventId)
    OUTPUT INSERTED.FinancialTransactionId INTO @FinancialTransactionOutput(FinancialTransactionId)
    VALUES
        (@ReferenceNumber, @transactionType, @PostingAmount, @Description, SYSUTCDATETIME(), @accountingEventId);
    SELECT @financialTransactionId = FinancialTransactionId FROM @FinancialTransactionOutput;

    DECLARE @journalEntryId int;
    INSERT INTO dbo.JournalEntries
        (ReferenceNumber, Description, EntryDate, CreatedAt, AccountingEventId)
    OUTPUT INSERTED.JournalEntryId INTO @JournalEntryOutput(JournalEntryId)
    VALUES
        (@ReferenceNumber, @Description, SYSUTCDATETIME(), SYSUTCDATETIME(), @accountingEventId);
    SELECT @journalEntryId = JournalEntryId FROM @JournalEntryOutput;

    INSERT INTO dbo.JournalEntryLines (JournalEntryId, LedgerAccountId, DebitAmount, CreditAmount, Description)
    VALUES
        (@journalEntryId, @debitLedgerAccountId, @PostingAmount, 0, @Description),
        (@journalEntryId, @creditLedgerAccountId, 0, @PostingAmount, @Description);

    IF (SELECT COUNT(*) FROM dbo.JournalEntryLines WHERE JournalEntryId = @journalEntryId) < 2
       OR (SELECT SUM(DebitAmount) FROM dbo.JournalEntryLines WHERE JournalEntryId = @journalEntryId) <> @PostingAmount
       OR (SELECT SUM(CreditAmount) FROM dbo.JournalEntryLines WHERE JournalEntryId = @journalEntryId) <> @PostingAmount
        THROW 51538, N''The accounting journal is not balanced.'', 1;

    IF @AccountingEventType IN (7,9)
        UPDATE dbo.InventoryReceiptPostings SET AccountingEventId = @accountingEventId WHERE InventoryReceiptPostingId = @InventoryReceiptPostingId;
    ELSE IF @AccountingEventType = 8
        UPDATE dbo.FabricConsumptionSources SET AccountingEventId = @accountingEventId WHERE FabricConsumptionSourceId = @FabricConsumptionSourceId;
    ELSE IF @AccountingEventType = 10
        UPDATE dbo.ProductionMaterialConsumptions SET AccountingEventId = @accountingEventId WHERE ProductionMaterialConsumptionId = @ProductionMaterialConsumptionId;
    ELSE IF @AccountingEventType = 11
        UPDATE dbo.ReadyMadeSaleCostPostings SET AccountingEventId = @accountingEventId WHERE ReadyMadeSaleCostPostingId = @ReadyMadeSaleCostPostingId;
    ELSE IF @AccountingEventType = 12
        UPDATE dbo.ImportedReadyMadeInventoryReceipts SET AccountingEventId = @accountingEventId WHERE ImportedReadyMadeInventoryReceiptId = @ImportedReadyMadeInventoryReceiptId;
    ELSE IF @AccountingEventType = 13
        UPDATE dbo.ImportedReadyMadeSaleCostPostings SET AccountingEventId = @accountingEventId WHERE ImportedReadyMadeSaleCostPostingId = @ImportedReadyMadeSaleCostPostingId;

    SELECT @accountingEventId AS AccountingEventId,
           @financialTransactionId AS FinancialTransactionId,
           @journalEntryId AS JournalEntryId,
           CAST(0 AS bit) AS IsExisting;
END;');

    DECLARE @gate2CutoverUtc nvarchar(33) = CONVERT(nvarchar(33), SYSUTCDATETIME(), 126) + N'Z';

    EXEC sys.sp_addextendedproperty
        @name = N'FinancialReadyMadeImportedCostOfSalesGate2CutoverUtc',
        @value = @gate2CutoverUtc,
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'TABLE', @level1name = N'ReadyMadeSaleCostPostings';

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;