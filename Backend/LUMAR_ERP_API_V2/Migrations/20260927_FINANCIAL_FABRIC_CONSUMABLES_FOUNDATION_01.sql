SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'LUMAR_ERP_TEST'
    THROW 51400, N'This migration is restricted to LUMAR_ERP_TEST.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.InventoryItemFoundation', N'U') IS NOT NULL
       OR OBJECT_ID(N'dbo.FabricRolls', N'U') IS NOT NULL
       OR OBJECT_ID(N'dbo.InventoryReceiptPostings', N'U') IS NOT NULL
       OR OBJECT_ID(N'dbo.InventoryReceiptLines', N'U') IS NOT NULL
       OR OBJECT_ID(N'dbo.FabricConsumptionSources', N'U') IS NOT NULL
       OR OBJECT_ID(N'dbo.InventoryClassCatalog', N'U') IS NOT NULL
       OR OBJECT_ID(N'dbo.InventoryUnitCatalog', N'U') IS NOT NULL
        THROW 51401, N'The fabric and consumables foundation already exists.', 1;

    IF EXISTS (SELECT 1 FROM dbo.LedgerAccounts WHERE AccountCode IN (N'1101', N'1102', N'5300'))
        THROW 51402, N'One or more approved inventory ledger accounts already exist.', 1;

    IF EXISTS (SELECT 1 FROM dbo.AccountingEvents WHERE AccountingEventType IN (7, 8, 9, 10))
        THROW 51403, N'One or more approved inventory accounting event types are already in use.', 1;

    IF OBJECT_ID(N'dbo.usp_PostAccountingEvent', N'P') IS NULL
        THROW 51404, N'The official accounting event writer is missing.', 1;

    IF COL_LENGTH(N'dbo.InventoryItems', N'OperationalValue') IS NOT NULL
       OR COL_LENGTH(N'dbo.InventoryTransactions', N'AccountingEventId') IS NOT NULL
       OR COL_LENGTH(N'dbo.InventoryTransactions', N'SourceOperationId') IS NOT NULL
       OR COL_LENGTH(N'dbo.AccountingEvents', N'InventoryReceiptPostingId') IS NOT NULL
       OR COL_LENGTH(N'dbo.AccountingEvents', N'FabricConsumptionSourceId') IS NOT NULL
       OR COL_LENGTH(N'dbo.AccountingEvents', N'ProductionMaterialConsumptionId') IS NOT NULL
        THROW 51405, N'One or more foundation columns already exist.', 1;

    IF OBJECT_ID(N'dbo.InventoryFoundationProcedureBackups', N'U') IS NOT NULL
        THROW 51406, N'The procedure backup table already exists.', 1;

    CREATE TABLE dbo.InventoryFoundationProcedureBackups
    (
        BackupId int IDENTITY(1,1) NOT NULL,
        ProcedureName sysname NOT NULL,
        ProcedureDefinition nvarchar(max) NOT NULL,
        CapturedAt datetime2(7) NOT NULL
            CONSTRAINT DF_InventoryFoundationProcedureBackups_CapturedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_InventoryFoundationProcedureBackups PRIMARY KEY (BackupId)
    );

    INSERT INTO dbo.InventoryFoundationProcedureBackups (ProcedureName, ProcedureDefinition)
    SELECT N'dbo.usp_PostAccountingEvent', OBJECT_DEFINITION(OBJECT_ID(N'dbo.usp_PostAccountingEvent'));

    INSERT INTO dbo.LedgerAccounts (AccountCode, AccountName, AccountType, IsActive, CreatedAt)
    VALUES
        (N'1101', N'Fabric Inventory', N'Asset', 1, SYSUTCDATETIME()),
        (N'1102', N'Consumables Inventory', N'Asset', 1, SYSUTCDATETIME()),
        (N'5300', N'Operating Consumables Expense', N'Expense', 1, SYSUTCDATETIME());

    CREATE TABLE dbo.InventoryClassCatalog
    (
        InventoryClassId tinyint NOT NULL,
        Code nvarchar(20) NOT NULL,
        NameAr nvarchar(100) NOT NULL,
        CONSTRAINT PK_InventoryClassCatalog PRIMARY KEY (InventoryClassId),
        CONSTRAINT UQ_InventoryClassCatalog_Code UNIQUE (Code)
    );

    INSERT INTO dbo.InventoryClassCatalog (InventoryClassId, Code, NameAr)
    VALUES
        (1, N'FABRIC', N'قماش'),
        (2, N'CONSUMABLE', N'مادة مستهلكة');

    CREATE TABLE dbo.InventoryUnitCatalog
    (
        UnitId smallint NOT NULL,
        Code nvarchar(20) NOT NULL,
        NameAr nvarchar(100) NOT NULL,
        InchesPerUnit decimal(18,6) NULL,
        CONSTRAINT PK_InventoryUnitCatalog PRIMARY KEY (UnitId),
        CONSTRAINT UQ_InventoryUnitCatalog_Code UNIQUE (Code),
        CONSTRAINT CK_InventoryUnitCatalog_InchesPerUnit CHECK (InchesPerUnit IS NULL OR InchesPerUnit > 0)
    );

    INSERT INTO dbo.InventoryUnitCatalog (UnitId, Code, NameAr, InchesPerUnit)
    VALUES
        (1, N'YARD', N'ياردة', 36.000000),
        (2, N'INCH', N'بوصة', 1.000000),
        (3, N'PIECE', N'قطعة', NULL);

    ALTER TABLE dbo.InventoryItems ALTER COLUMN CurrentQuantity decimal(18,6) NOT NULL;
    ALTER TABLE dbo.InventoryItems ALTER COLUMN AvailableQuantity decimal(18,6) NOT NULL;
    ALTER TABLE dbo.InventoryItems ALTER COLUMN ReservedQuantity decimal(18,6) NOT NULL;

    ALTER TABLE dbo.InventoryTransactions ALTER COLUMN Quantity decimal(18,6) NOT NULL;
    ALTER TABLE dbo.InventoryTransactions ALTER COLUMN TotalCostImpact decimal(18,6) NULL;
    ALTER TABLE dbo.InventoryTransactions ALTER COLUMN UnitCost decimal(18,6) NULL;

    ALTER TABLE dbo.GoodsReceiptItems ALTER COLUMN ReceivedQuantity decimal(18,6) NOT NULL;
    ALTER TABLE dbo.GoodsReceiptItems ALTER COLUMN UnitCost decimal(18,6) NOT NULL;
    ALTER TABLE dbo.GoodsReceiptItems ALTER COLUMN LineTotal decimal(18,6) NOT NULL;

    ALTER TABLE dbo.ProductionMaterialConsumptions ALTER COLUMN ConsumedQuantity decimal(18,6) NOT NULL;
    ALTER TABLE dbo.ProductionMaterialConsumptions ALTER COLUMN UnitCost decimal(18,6) NOT NULL;
    ALTER TABLE dbo.ProductionMaterialConsumptions ALTER COLUMN TotalCost decimal(18,6) NOT NULL;

    ALTER TABLE dbo.InventoryTransactions
        ADD AccountingEventId bigint NULL,
            SourceEntityType tinyint NULL,
            SourceEntityId bigint NULL,
            SourceOperationId uniqueidentifier NULL,
            OperationalCostImpact decimal(18,6) NULL;

    ALTER TABLE dbo.ProductionMaterialConsumptions
        ADD UnitId smallint NULL,
            SourceOperationId uniqueidentifier NULL,
            OperationalAmount decimal(18,6) NULL,
            PostingAmount decimal(18,2) NULL,
            AccountingEventId bigint NULL,
            InventoryTransactionId int NULL;

    CREATE TABLE dbo.InventoryItemFoundation
    (
        InventoryItemId int NOT NULL,
        InventoryClassId tinyint NOT NULL,
        UnitId smallint NOT NULL,
        CurrencyCode char(3) NOT NULL,
        OriginalQuantity decimal(18,6) NOT NULL,
        AvailableQuantity decimal(18,6) NOT NULL,
        ConsumedQuantity decimal(18,6) NOT NULL,
        OfficialUnitCost decimal(18,6) NOT NULL,
        OperationalValue decimal(18,6) NOT NULL,
        CreatedAt datetime2(7) NOT NULL
            CONSTRAINT DF_InventoryItemFoundation_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt datetime2(7) NOT NULL
            CONSTRAINT DF_InventoryItemFoundation_UpdatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_InventoryItemFoundation PRIMARY KEY (InventoryItemId),
        CONSTRAINT FK_InventoryItemFoundation_Item
            FOREIGN KEY (InventoryItemId) REFERENCES dbo.InventoryItems(InventoryItemID),
        CONSTRAINT FK_InventoryItemFoundation_Class
            FOREIGN KEY (InventoryClassId) REFERENCES dbo.InventoryClassCatalog(InventoryClassId),
        CONSTRAINT FK_InventoryItemFoundation_Unit
            FOREIGN KEY (UnitId) REFERENCES dbo.InventoryUnitCatalog(UnitId),
        CONSTRAINT CK_InventoryItemFoundation_Currency CHECK (CurrencyCode = 'YER'),
        CONSTRAINT CK_InventoryItemFoundation_Values CHECK
        (
            OriginalQuantity > 0
            AND AvailableQuantity >= 0
            AND ConsumedQuantity >= 0
            AND OfficialUnitCost > 0
            AND OperationalValue >= 0
        )
    );

    CREATE TABLE dbo.InventoryReceiptPostings
    (
        InventoryReceiptPostingId bigint IDENTITY(1,1) NOT NULL,
        GoodsReceiptItemId int NOT NULL,
        InventoryClassId tinyint NOT NULL,
        AccountingBasisCode tinyint NOT NULL,
        OpposingLedgerAccountId int NOT NULL,
        SourceOperationId uniqueidentifier NOT NULL,
        OperationalAmount decimal(18,6) NOT NULL,
        PostingAmount decimal(18,2) NOT NULL,
        AccountingEventId bigint NULL,
        CreatedAt datetime2(7) NOT NULL
            CONSTRAINT DF_InventoryReceiptPostings_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_InventoryReceiptPostings PRIMARY KEY (InventoryReceiptPostingId),
        CONSTRAINT FK_InventoryReceiptPostings_GoodsReceiptItem
            FOREIGN KEY (GoodsReceiptItemId) REFERENCES dbo.GoodsReceiptItems(GoodsReceiptItemId),
        CONSTRAINT FK_InventoryReceiptPostings_Class
            FOREIGN KEY (InventoryClassId) REFERENCES dbo.InventoryClassCatalog(InventoryClassId),
        CONSTRAINT FK_InventoryReceiptPostings_OpposingAccount
            FOREIGN KEY (OpposingLedgerAccountId) REFERENCES dbo.LedgerAccounts(LedgerAccountId),
        CONSTRAINT CK_InventoryReceiptPostings_Basis CHECK (AccountingBasisCode BETWEEN 1 AND 4),
        CONSTRAINT CK_InventoryReceiptPostings_Amounts CHECK
        (
            OperationalAmount > 0
            AND PostingAmount > 0
            AND PostingAmount = CONVERT(decimal(18,2), ROUND(OperationalAmount, 2))
        )
    );

    CREATE TABLE dbo.InventoryReceiptLines
    (
        InventoryReceiptLineId bigint IDENTITY(1,1) NOT NULL,
        InventoryReceiptPostingId bigint NOT NULL,
        InventoryItemId int NOT NULL,
        FabricRollId bigint NULL,
        ReceivedQuantity decimal(18,6) NOT NULL,
        OfficialUnitCost decimal(18,6) NOT NULL,
        UnitId smallint NOT NULL,
        OperationalAmount AS CONVERT(decimal(18,6), ReceivedQuantity * OfficialUnitCost) PERSISTED,
        InventoryTransactionId int NULL,
        AccountingEventId bigint NULL,
        CreatedAt datetime2(7) NOT NULL
            CONSTRAINT DF_InventoryReceiptLines_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_InventoryReceiptLines PRIMARY KEY (InventoryReceiptLineId),
        CONSTRAINT FK_InventoryReceiptLines_Posting
            FOREIGN KEY (InventoryReceiptPostingId) REFERENCES dbo.InventoryReceiptPostings(InventoryReceiptPostingId),
        CONSTRAINT FK_InventoryReceiptLines_Item
            FOREIGN KEY (InventoryItemId) REFERENCES dbo.InventoryItems(InventoryItemID),
        CONSTRAINT FK_InventoryReceiptLines_Unit
            FOREIGN KEY (UnitId) REFERENCES dbo.InventoryUnitCatalog(UnitId),
        CONSTRAINT CK_InventoryReceiptLines_Values CHECK (ReceivedQuantity > 0 AND OfficialUnitCost > 0)
    );

    CREATE TABLE dbo.FabricRolls
    (
        FabricRollId bigint IDENTITY(1,1) NOT NULL,
        InventoryItemId int NOT NULL,
        RollCode nvarchar(100) NOT NULL,
        FabricTypeCode nvarchar(100) NOT NULL,
        ColorValue nvarchar(100) NULL,
        OriginalQuantity decimal(18,6) NOT NULL,
        AvailableQuantity decimal(18,6) NOT NULL,
        ConsumedQuantity decimal(18,6) NOT NULL,
        UnitId smallint NOT NULL,
        OfficialUnitCost decimal(18,6) NOT NULL,
        CurrencyCode char(3) NOT NULL,
        InventoryReceiptLineId bigint NOT NULL,
        CreatedAt datetime2(7) NOT NULL
            CONSTRAINT DF_FabricRolls_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt datetime2(7) NOT NULL
            CONSTRAINT DF_FabricRolls_UpdatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_FabricRolls PRIMARY KEY (FabricRollId),
        CONSTRAINT UQ_FabricRolls_RollCode UNIQUE (RollCode),
        CONSTRAINT UQ_FabricRolls_ReceiptLine UNIQUE (InventoryReceiptLineId),
        CONSTRAINT FK_FabricRolls_Item FOREIGN KEY (InventoryItemId) REFERENCES dbo.InventoryItems(InventoryItemID),
        CONSTRAINT FK_FabricRolls_Unit FOREIGN KEY (UnitId) REFERENCES dbo.InventoryUnitCatalog(UnitId),
        CONSTRAINT CK_FabricRolls_Currency CHECK (CurrencyCode = 'YER'),
        CONSTRAINT CK_FabricRolls_Values CHECK
        (
            OriginalQuantity > 0
            AND AvailableQuantity >= 0
            AND ConsumedQuantity >= 0
            AND AvailableQuantity + ConsumedQuantity = OriginalQuantity
            AND OfficialUnitCost > 0
        )
    );

    ALTER TABLE dbo.InventoryReceiptLines
        ADD CONSTRAINT FK_InventoryReceiptLines_FabricRoll
            FOREIGN KEY (FabricRollId) REFERENCES dbo.FabricRolls(FabricRollId);

    CREATE TABLE dbo.FabricConsumptionSources
    (
        FabricConsumptionSourceId bigint IDENTITY(1,1) NOT NULL,
        SourceOperationId uniqueidentifier NOT NULL,
        InventoryItemId int NOT NULL,
        FabricRollId bigint NOT NULL,
        OrderItemId int NOT NULL,
        PieceId int NULL,
        ConsumedQuantity decimal(18,6) NOT NULL,
        UnitId smallint NOT NULL,
        OfficialUnitCost decimal(18,6) NOT NULL,
        OperationalAmount decimal(18,6) NOT NULL,
        PostingAmount decimal(18,2) NOT NULL,
        ConfirmedByUserId int NULL,
        ConfirmedAt datetime2(7) NOT NULL,
        InventoryTransactionId int NULL,
        AccountingEventId bigint NULL,
        CreatedAt datetime2(7) NOT NULL
            CONSTRAINT DF_FabricConsumptionSources_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_FabricConsumptionSources PRIMARY KEY (FabricConsumptionSourceId),
        CONSTRAINT UQ_FabricConsumptionSources_SourceOperation UNIQUE (SourceOperationId),
        CONSTRAINT FK_FabricConsumptionSources_Item FOREIGN KEY (InventoryItemId) REFERENCES dbo.InventoryItems(InventoryItemID),
        CONSTRAINT FK_FabricConsumptionSources_Roll FOREIGN KEY (FabricRollId) REFERENCES dbo.FabricRolls(FabricRollId),
        CONSTRAINT FK_FabricConsumptionSources_OrderItem FOREIGN KEY (OrderItemId) REFERENCES dbo.OrderItems(OrderItemID),
        CONSTRAINT FK_FabricConsumptionSources_Piece FOREIGN KEY (PieceId) REFERENCES dbo.Pieces(PieceID),
        CONSTRAINT FK_FabricConsumptionSources_Unit FOREIGN KEY (UnitId) REFERENCES dbo.InventoryUnitCatalog(UnitId),
        CONSTRAINT CK_FabricConsumptionSources_Amounts CHECK
        (
            ConsumedQuantity > 0
            AND OfficialUnitCost > 0
            AND OperationalAmount = CONVERT(decimal(18,6), ConsumedQuantity * OfficialUnitCost)
            AND PostingAmount = CONVERT(decimal(18,2), ROUND(OperationalAmount, 2))
        )
    );

    ALTER TABLE dbo.AccountingEvents
        ADD InventoryReceiptPostingId bigint NULL,
            FabricConsumptionSourceId bigint NULL,
            ProductionMaterialConsumptionId int NULL;

    ALTER TABLE dbo.AccountingEvents
        ADD CONSTRAINT FK_AccountingEvents_InventoryReceiptPosting
            FOREIGN KEY (InventoryReceiptPostingId)
            REFERENCES dbo.InventoryReceiptPostings(InventoryReceiptPostingId),
            CONSTRAINT FK_AccountingEvents_FabricConsumptionSource
            FOREIGN KEY (FabricConsumptionSourceId)
            REFERENCES dbo.FabricConsumptionSources(FabricConsumptionSourceId),
            CONSTRAINT FK_AccountingEvents_ProductionMaterialConsumption
            FOREIGN KEY (ProductionMaterialConsumptionId)
            REFERENCES dbo.ProductionMaterialConsumptions(ProductionMaterialConsumptionId);

    ALTER TABLE dbo.InventoryTransactions
        ADD CONSTRAINT FK_InventoryTransactions_AccountingEvent
            FOREIGN KEY (AccountingEventId) REFERENCES dbo.AccountingEvents(AccountingEventId);

    ALTER TABLE dbo.ProductionMaterialConsumptions
        ADD CONSTRAINT FK_ProductionMaterialConsumptions_Unit
            FOREIGN KEY (UnitId) REFERENCES dbo.InventoryUnitCatalog(UnitId),
            CONSTRAINT FK_ProductionMaterialConsumptions_AccountingEvent
            FOREIGN KEY (AccountingEventId) REFERENCES dbo.AccountingEvents(AccountingEventId),
            CONSTRAINT FK_ProductionMaterialConsumptions_InventoryTransaction
            FOREIGN KEY (InventoryTransactionId) REFERENCES dbo.InventoryTransactions(TransactionID);

    ALTER TABLE dbo.InventoryReceiptPostings
        ADD CONSTRAINT FK_InventoryReceiptPostings_AccountingEvent
            FOREIGN KEY (AccountingEventId) REFERENCES dbo.AccountingEvents(AccountingEventId);

    ALTER TABLE dbo.InventoryReceiptLines
        ADD CONSTRAINT FK_InventoryReceiptLines_InventoryTransaction
            FOREIGN KEY (InventoryTransactionId) REFERENCES dbo.InventoryTransactions(TransactionID),
            CONSTRAINT FK_InventoryReceiptLines_AccountingEvent
            FOREIGN KEY (AccountingEventId) REFERENCES dbo.AccountingEvents(AccountingEventId);

    ALTER TABLE dbo.FabricConsumptionSources
        ADD CONSTRAINT FK_FabricConsumptionSources_InventoryTransaction
            FOREIGN KEY (InventoryTransactionId) REFERENCES dbo.InventoryTransactions(TransactionID),
            CONSTRAINT FK_FabricConsumptionSources_AccountingEvent
            FOREIGN KEY (AccountingEventId) REFERENCES dbo.AccountingEvents(AccountingEventId);

    EXEC(N'
        ALTER TABLE dbo.AccountingEvents DROP CONSTRAINT CK_AccountingEvents_Type;
        ALTER TABLE dbo.AccountingEvents DROP CONSTRAINT CK_AccountingEvents_SourceCardinality;
        ALTER TABLE dbo.AccountingEvents ADD
            CONSTRAINT CK_AccountingEvents_Type
                CHECK (AccountingEventType BETWEEN 1 AND 10),
            CONSTRAINT CK_AccountingEvents_SourceCardinality
                CHECK
                (
                       (AccountingEventType IN (1, 2)
                        AND PaymentId IS NOT NULL AND OrderId IS NULL
                        AND MeasurementCardPrintHistoryId IS NULL AND ReadyMadeInventoryProductId IS NULL
                        AND CustomerAdvanceApplicationId IS NULL AND InventoryReceiptPostingId IS NULL
                        AND FabricConsumptionSourceId IS NULL AND ProductionMaterialConsumptionId IS NULL)
                    OR (AccountingEventType = 3
                        AND PaymentId IS NULL AND OrderId IS NOT NULL
                        AND MeasurementCardPrintHistoryId IS NULL AND ReadyMadeInventoryProductId IS NULL
                        AND CustomerAdvanceApplicationId IS NULL AND InventoryReceiptPostingId IS NULL
                        AND FabricConsumptionSourceId IS NULL AND ProductionMaterialConsumptionId IS NULL)
                    OR (AccountingEventType = 4
                        AND PaymentId IS NULL AND OrderId IS NULL
                        AND MeasurementCardPrintHistoryId IS NOT NULL AND ReadyMadeInventoryProductId IS NULL
                        AND CustomerAdvanceApplicationId IS NULL AND InventoryReceiptPostingId IS NULL
                        AND FabricConsumptionSourceId IS NULL AND ProductionMaterialConsumptionId IS NULL)
                    OR (AccountingEventType = 5
                        AND PaymentId IS NULL AND OrderId IS NULL
                        AND MeasurementCardPrintHistoryId IS NULL AND ReadyMadeInventoryProductId IS NOT NULL
                        AND CustomerAdvanceApplicationId IS NULL AND InventoryReceiptPostingId IS NULL
                        AND FabricConsumptionSourceId IS NULL AND ProductionMaterialConsumptionId IS NULL)
                    OR (AccountingEventType = 6
                        AND PaymentId IS NULL AND OrderId IS NULL
                        AND MeasurementCardPrintHistoryId IS NULL AND ReadyMadeInventoryProductId IS NULL
                        AND CustomerAdvanceApplicationId IS NOT NULL AND InventoryReceiptPostingId IS NULL
                        AND FabricConsumptionSourceId IS NULL AND ProductionMaterialConsumptionId IS NULL)
                    OR (AccountingEventType = 7
                        AND PaymentId IS NULL AND OrderId IS NULL
                        AND MeasurementCardPrintHistoryId IS NULL AND ReadyMadeInventoryProductId IS NULL
                        AND CustomerAdvanceApplicationId IS NULL AND InventoryReceiptPostingId IS NOT NULL
                        AND FabricConsumptionSourceId IS NULL AND ProductionMaterialConsumptionId IS NULL)
                    OR (AccountingEventType = 8
                        AND PaymentId IS NULL AND OrderId IS NULL
                        AND MeasurementCardPrintHistoryId IS NULL AND ReadyMadeInventoryProductId IS NULL
                        AND CustomerAdvanceApplicationId IS NULL AND InventoryReceiptPostingId IS NULL
                        AND FabricConsumptionSourceId IS NOT NULL AND ProductionMaterialConsumptionId IS NULL)
                    OR (AccountingEventType = 9
                        AND PaymentId IS NULL AND OrderId IS NULL
                        AND MeasurementCardPrintHistoryId IS NULL AND ReadyMadeInventoryProductId IS NULL
                        AND CustomerAdvanceApplicationId IS NULL AND InventoryReceiptPostingId IS NOT NULL
                        AND FabricConsumptionSourceId IS NULL AND ProductionMaterialConsumptionId IS NULL)
                    OR (AccountingEventType = 10
                        AND PaymentId IS NULL AND OrderId IS NULL
                        AND MeasurementCardPrintHistoryId IS NULL AND ReadyMadeInventoryProductId IS NULL
                        AND CustomerAdvanceApplicationId IS NULL AND InventoryReceiptPostingId IS NULL
                        AND FabricConsumptionSourceId IS NULL AND ProductionMaterialConsumptionId IS NOT NULL)
                );

        CREATE UNIQUE INDEX UX_InventoryReceiptPostings_SourceOperation
            ON dbo.InventoryReceiptPostings(SourceOperationId);
        CREATE UNIQUE INDEX UX_InventoryReceiptPostings_GoodsReceiptItemClass
            ON dbo.InventoryReceiptPostings(GoodsReceiptItemId, InventoryClassId);
        CREATE INDEX IX_InventoryReceiptPostings_AccountingBasis
            ON dbo.InventoryReceiptPostings(AccountingBasisCode, CreatedAt);
        CREATE UNIQUE INDEX UX_InventoryReceiptLines_Transaction
            ON dbo.InventoryReceiptLines(InventoryTransactionId)
            WHERE InventoryTransactionId IS NOT NULL;
        CREATE UNIQUE INDEX UX_FabricConsumptionSources_AccountingEvent
            ON dbo.FabricConsumptionSources(AccountingEventId)
            WHERE AccountingEventId IS NOT NULL;
        CREATE UNIQUE INDEX UX_FabricConsumptionSources_InventoryTransaction
            ON dbo.FabricConsumptionSources(InventoryTransactionId)
            WHERE InventoryTransactionId IS NOT NULL;
        CREATE UNIQUE INDEX UX_ProductionMaterialConsumptions_SourceOperation
            ON dbo.ProductionMaterialConsumptions(SourceOperationId)
            WHERE SourceOperationId IS NOT NULL;
        CREATE UNIQUE INDEX UX_ProductionMaterialConsumptions_AccountingEvent
            ON dbo.ProductionMaterialConsumptions(AccountingEventId)
            WHERE AccountingEventId IS NOT NULL;
        CREATE UNIQUE INDEX UX_ProductionMaterialConsumptions_InventoryTransaction
            ON dbo.ProductionMaterialConsumptions(InventoryTransactionId)
            WHERE InventoryTransactionId IS NOT NULL;
        CREATE UNIQUE INDEX UX_InventoryTransactions_SourceOperation
            ON dbo.InventoryTransactions(SourceOperationId)
            WHERE SourceOperationId IS NOT NULL;
        CREATE UNIQUE INDEX UX_InventoryTransactions_SourceEntity
            ON dbo.InventoryTransactions(SourceEntityType, SourceEntityId)
            WHERE SourceEntityType IS NOT NULL AND SourceEntityId IS NOT NULL;
        CREATE UNIQUE INDEX UX_AccountingEvents_InventoryReceiptPosting
            ON dbo.AccountingEvents(InventoryReceiptPostingId)
            WHERE InventoryReceiptPostingId IS NOT NULL;
        CREATE UNIQUE INDEX UX_AccountingEvents_FabricConsumptionSource
            ON dbo.AccountingEvents(FabricConsumptionSourceId)
            WHERE FabricConsumptionSourceId IS NOT NULL;
        CREATE UNIQUE INDEX UX_AccountingEvents_ProductionMaterialConsumption
            ON dbo.AccountingEvents(ProductionMaterialConsumptionId)
            WHERE ProductionMaterialConsumptionId IS NOT NULL;');

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
    @ReferenceNumber nvarchar(200) = NULL,
    @Description nvarchar(1000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @@TRANCOUNT = 0
        THROW 51410, N''Accounting events require a caller-owned SQL transaction.'', 1;

    IF @AccountingEventType NOT BETWEEN 1 AND 10 OR @PostingAmount <= 0
        THROW 51411, N''Unsupported accounting event type or posting amount.'', 1;

    IF NOT
    (
           (@AccountingEventType IN (1,2) AND @PaymentId IS NOT NULL AND @OrderId IS NULL AND @MeasurementCardPrintHistoryId IS NULL AND @ReadyMadeInventoryProductId IS NULL AND @CustomerAdvanceApplicationId IS NULL AND @InventoryReceiptPostingId IS NULL AND @FabricConsumptionSourceId IS NULL AND @ProductionMaterialConsumptionId IS NULL)
        OR (@AccountingEventType = 3 AND @PaymentId IS NULL AND @OrderId IS NOT NULL AND @MeasurementCardPrintHistoryId IS NULL AND @ReadyMadeInventoryProductId IS NULL AND @CustomerAdvanceApplicationId IS NULL AND @InventoryReceiptPostingId IS NULL AND @FabricConsumptionSourceId IS NULL AND @ProductionMaterialConsumptionId IS NULL)
        OR (@AccountingEventType = 4 AND @PaymentId IS NULL AND @OrderId IS NULL AND @MeasurementCardPrintHistoryId IS NOT NULL AND @ReadyMadeInventoryProductId IS NULL AND @CustomerAdvanceApplicationId IS NULL AND @InventoryReceiptPostingId IS NULL AND @FabricConsumptionSourceId IS NULL AND @ProductionMaterialConsumptionId IS NULL)
        OR (@AccountingEventType = 5 AND @PaymentId IS NULL AND @OrderId IS NULL AND @MeasurementCardPrintHistoryId IS NULL AND @ReadyMadeInventoryProductId IS NOT NULL AND @CustomerAdvanceApplicationId IS NULL AND @InventoryReceiptPostingId IS NULL AND @FabricConsumptionSourceId IS NULL AND @ProductionMaterialConsumptionId IS NULL)
        OR (@AccountingEventType = 6 AND @PaymentId IS NULL AND @OrderId IS NULL AND @MeasurementCardPrintHistoryId IS NULL AND @ReadyMadeInventoryProductId IS NULL AND @CustomerAdvanceApplicationId IS NOT NULL AND @InventoryReceiptPostingId IS NULL AND @FabricConsumptionSourceId IS NULL AND @ProductionMaterialConsumptionId IS NULL)
        OR (@AccountingEventType IN (7,9) AND @PaymentId IS NULL AND @OrderId IS NULL AND @MeasurementCardPrintHistoryId IS NULL AND @ReadyMadeInventoryProductId IS NULL AND @CustomerAdvanceApplicationId IS NULL AND @InventoryReceiptPostingId IS NOT NULL AND @FabricConsumptionSourceId IS NULL AND @ProductionMaterialConsumptionId IS NULL)
        OR (@AccountingEventType = 8 AND @PaymentId IS NULL AND @OrderId IS NULL AND @MeasurementCardPrintHistoryId IS NULL AND @ReadyMadeInventoryProductId IS NULL AND @CustomerAdvanceApplicationId IS NULL AND @InventoryReceiptPostingId IS NULL AND @FabricConsumptionSourceId IS NOT NULL AND @ProductionMaterialConsumptionId IS NULL)
        OR (@AccountingEventType = 10 AND @PaymentId IS NULL AND @OrderId IS NULL AND @MeasurementCardPrintHistoryId IS NULL AND @ReadyMadeInventoryProductId IS NULL AND @CustomerAdvanceApplicationId IS NULL AND @InventoryReceiptPostingId IS NULL AND @FabricConsumptionSourceId IS NULL AND @ProductionMaterialConsumptionId IS NOT NULL)
    )
        THROW 51412, N''The accounting event source does not match its event type.'', 1;

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
       OR (@AccountingEventType = 10 AND ProductionMaterialConsumptionId = @ProductionMaterialConsumptionId);

    IF @existingAccountingEventId IS NOT NULL
    BEGIN
        IF @existingPostingAmount <> @PostingAmount
            THROW 51413, N''The existing accounting event amount differs from the retry amount.'', 1;

        DECLARE @existingFinancialTransactionId int;
        DECLARE @existingJournalEntryId int;
        SELECT @existingFinancialTransactionId = FinancialTransactionId
        FROM dbo.FinancialTransactions WITH (UPDLOCK, HOLDLOCK)
        WHERE AccountingEventId = @existingAccountingEventId;
        SELECT @existingJournalEntryId = JournalEntryId
        FROM dbo.JournalEntries WITH (UPDLOCK, HOLDLOCK)
        WHERE AccountingEventId = @existingAccountingEventId;
        IF @existingFinancialTransactionId IS NULL OR @existingJournalEntryId IS NULL
            THROW 51414, N''The existing accounting event is incomplete.'', 1;
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
            THROW 51415, N''The inventory receipt posting source is missing or inactive.'', 1;
        IF @sourcePostingAmount <> @PostingAmount
            THROW 51416, N''The inventory receipt posting amount does not match.'', 1;
        IF CONVERT(decimal(18,2), ROUND(@sourceOperationalAmount, 2)) <> @PostingAmount
            THROW 51417, N''The inventory receipt operational amount does not match.'', 1;
        IF (@AccountingEventType = 7 AND @receiptClass <> 1)
           OR (@AccountingEventType = 9 AND @receiptClass <> 2)
            THROW 51418, N''The inventory receipt class does not match the event type.'', 1;

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
            THROW 51419, N''The fabric consumption source is missing.'', 1;
        IF @sourcePostingAmount <> @PostingAmount
           OR CONVERT(decimal(18,2), ROUND(@sourceOperationalAmount, 2)) <> @PostingAmount
            THROW 51420, N''The fabric consumption amount does not match.'', 1;
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
            THROW 51421, N''The consumable consumption source is missing.'', 1;
        IF @sourcePostingAmount <> @PostingAmount
           OR CONVERT(decimal(18,2), ROUND(@sourceOperationalAmount, 2)) <> @PostingAmount
            THROW 51422, N''The consumable consumption amount does not match.'', 1;
        SET @transactionType = N''ConsumableInventoryConsumed'';
        SET @debitAccountCode = N''5300'';
        SET @creditAccountCode = N''1102'';
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
            THROW 51423, N''The payment source is missing or has a different amount.'', 1;
        IF (@AccountingEventType = 1 AND @paymentKind <> N''Advance'')
           OR (@AccountingEventType = 2 AND @paymentKind NOT IN (N''DebtCollection'', N''SaleCash'', N''MeasurementCardPieceSale''))
            THROW 51424, N''The payment kind is not supported by this accounting event.'', 1;
    END;

    IF @AccountingEventType = 3
       AND NOT EXISTS (SELECT 1 FROM dbo.Orders WITH (UPDLOCK, HOLDLOCK) WHERE OrderID = @OrderId)
        THROW 51425, N''The revenue order source does not exist.'', 1;

    IF @AccountingEventType = 4
       AND NOT EXISTS
       (
           SELECT 1 FROM dbo.MeasurementCardPrintHistory WITH (UPDLOCK, HOLDLOCK)
           WHERE PrintHistoryId = @MeasurementCardPrintHistoryId
             AND ReprintReasonCode = N''PieceSold''
             AND SaleAmount = @PostingAmount
       )
        THROW 51426, N''The print-sale source is missing or does not match the posting amount.'', 1;

    IF @AccountingEventType = 5
       AND NOT EXISTS
       (
           SELECT 1 FROM dbo.ReadyMadeInventoryProducts WITH (UPDLOCK, HOLDLOCK)
           WHERE ReadyMadeInventoryProductId = @ReadyMadeInventoryProductId
             AND CAST(ActualCost AS decimal(18,2)) = @PostingAmount
       )
        THROW 51427, N''The ready-made inventory source is missing or does not match the posting amount.'', 1;

    IF @AccountingEventType = 6
    BEGIN
        DECLARE @applicationAmount decimal(18,2);
        DECLARE @applicationPaymentId int;
        DECLARE @applicationOrderId int;
        DECLARE @applicationRevenueEventId bigint;
        DECLARE @advancePaymentKind nvarchar(100);
        DECLARE @revenueEventType tinyint;
        DECLARE @revenueEventOrderId int;
        SELECT
            @applicationAmount = app.AppliedAmount,
            @applicationPaymentId = app.AdvancePaymentId,
            @applicationOrderId = app.OrderId,
            @applicationRevenueEventId = app.RevenueAccountingEventId,
            @advancePaymentKind = p.PaymentKind,
            @revenueEventType = revenue.AccountingEventType,
            @revenueEventOrderId = revenue.OrderId
        FROM dbo.CustomerAdvanceApplications app WITH (UPDLOCK, HOLDLOCK)
        INNER JOIN dbo.Payments p WITH (UPDLOCK, HOLDLOCK) ON p.PaymentID = app.AdvancePaymentId
        INNER JOIN dbo.AccountingEvents revenue WITH (UPDLOCK, HOLDLOCK) ON revenue.AccountingEventId = app.RevenueAccountingEventId
        WHERE app.AdvanceApplicationId = @CustomerAdvanceApplicationId;
        IF @applicationAmount IS NULL OR @applicationAmount <> @PostingAmount
           OR @advancePaymentKind <> N''Advance'' OR @revenueEventType <> 3
           OR @revenueEventOrderId <> @applicationOrderId
            THROW 51428, N''The advance application source is invalid.'', 1;
        IF NOT EXISTS (SELECT 1 FROM dbo.Payments WHERE PaymentID = @applicationPaymentId AND OrderID = @applicationOrderId)
            THROW 51429, N''The advance payment does not belong to the application order.'', 1;
    END;

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
        THROW 51430, N''A required active ledger account is missing.'', 1;

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
        ProductionMaterialConsumptionId
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
        @ProductionMaterialConsumptionId
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
        THROW 51431, N''The accounting journal is not balanced.'', 1;

    IF @AccountingEventType IN (7,9)
        UPDATE dbo.InventoryReceiptPostings
        SET AccountingEventId = @accountingEventId
        WHERE InventoryReceiptPostingId = @InventoryReceiptPostingId;
    ELSE IF @AccountingEventType = 8
        UPDATE dbo.FabricConsumptionSources
        SET AccountingEventId = @accountingEventId
        WHERE FabricConsumptionSourceId = @FabricConsumptionSourceId;
    ELSE IF @AccountingEventType = 10
        UPDATE dbo.ProductionMaterialConsumptions
        SET AccountingEventId = @accountingEventId
        WHERE ProductionMaterialConsumptionId = @ProductionMaterialConsumptionId;

    SELECT @accountingEventId AS AccountingEventId,
           @financialTransactionId AS FinancialTransactionId,
           @journalEntryId AS JournalEntryId,
           CAST(0 AS bit) AS IsExisting;
END;');

    DECLARE @foundationCutoverUtc nvarchar(33) = CONVERT(nvarchar(33), SYSUTCDATETIME(), 126) + N'Z';

    EXEC sys.sp_addextendedproperty
        @name = N'FabricConsumablesFoundationCutoverUtc',
        @value = @foundationCutoverUtc,
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'TABLE', @level1name = N'InventoryItemFoundation';

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
