SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

IF DB_NAME() <> N'LUMAR_ERP_ES_VALIDATION'
    THROW 51700, N'This Phase TF-1 tools issuance/custody/reversal migration is restricted to LUMAR_ERP_ES_VALIDATION.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.ToolIssuances', N'U') IS NOT NULL
       OR OBJECT_ID(N'dbo.ToolIssuanceReturns', N'U') IS NOT NULL
       OR OBJECT_ID(N'dbo.ToolIssuanceReversals', N'U') IS NOT NULL
        THROW 51701, N'One or more tool issuance tables already exist; this phase is not re-runnable.', 1;

    IF COL_LENGTH(N'dbo.AccountingEvents', N'ToolIssuanceId') IS NOT NULL
       OR COL_LENGTH(N'dbo.AccountingEvents', N'ToolIssuanceReversalId') IS NOT NULL
        THROW 51702, N'One or more Phase TF-1 AccountingEvents columns already exist.', 1;

    IF EXISTS (SELECT 1 FROM dbo.AccountingEvents WITH (UPDLOCK, HOLDLOCK) WHERE AccountingEventType IN (14, 15))
        THROW 51703, N'One or more Phase TF-1 accounting event types are already in use.', 1;

    IF OBJECT_ID(N'dbo.usp_PostAccountingEvent', N'P') IS NULL
        THROW 51704, N'The official accounting event writer is missing.', 1;

    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.LedgerAccounts
        WHERE AccountCode IN (N'1102', N'5300')
          AND IsActive = 1
        GROUP BY AccountCode
        HAVING COUNT(*) = 1
    )
        THROW 51705, N'One or more approved Phase TF-1 ledger accounts are missing or duplicated.', 1;

    CREATE TABLE dbo.ToolIssuances
    (
        ToolIssuanceId bigint IDENTITY(1,1) NOT NULL,
        InventoryItemId int NOT NULL,
        IssueType nvarchar(20) NOT NULL,
        Quantity decimal(18,3) NOT NULL,
        OperationalAmount decimal(18,6) NOT NULL,
        PostingAmount decimal(18,2) NOT NULL,
        ReturnedQuantity decimal(18,3) NOT NULL CONSTRAINT DF_ToolIssuances_ReturnedQuantity DEFAULT 0,
        OperationalReason nvarchar(500) NULL,
        BeneficiaryName nvarchar(200) NULL,
        DestinationType nvarchar(100) NULL,
        DestinationName nvarchar(200) NULL,
        LoanReason nvarchar(500) NULL,
        Notes nvarchar(1000) NULL,
        Status nvarchar(20) NOT NULL,
        AccountingEventId bigint NULL,
        InventoryTransactionId int NULL,
        SourceOperationId uniqueidentifier NOT NULL,
        ConfirmedByUserId int NULL,
        CreatedAt datetime2(7) NOT NULL CONSTRAINT DF_ToolIssuances_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_ToolIssuances PRIMARY KEY (ToolIssuanceId),
        CONSTRAINT UQ_ToolIssuances_SourceOperation UNIQUE (SourceOperationId),
        CONSTRAINT CK_ToolIssuances_IssueType CHECK (IssueType IN (N'Operational', N'Custody')),
        CONSTRAINT CK_ToolIssuances_Quantity CHECK (Quantity > 0),
        CONSTRAINT CK_ToolIssuances_Amounts CHECK (OperationalAmount > 0 AND PostingAmount > 0 AND PostingAmount = CONVERT(decimal(18,2), ROUND(OperationalAmount, 2))),
        CONSTRAINT CK_ToolIssuances_ReturnedQuantity CHECK (ReturnedQuantity >= 0 AND ReturnedQuantity <= Quantity),
        CONSTRAINT CK_ToolIssuances_Status CHECK (Status IN (N'Posted', N'Reversed', N'Outstanding', N'PartiallyReturned', N'Returned')),
        CONSTRAINT CK_ToolIssuances_OperationalFields CHECK (
            (IssueType = N'Operational' AND OperationalReason IS NOT NULL AND BeneficiaryName IS NULL AND DestinationType IS NULL AND DestinationName IS NULL AND LoanReason IS NULL)
            OR
            (IssueType = N'Custody' AND OperationalReason IS NULL AND BeneficiaryName IS NOT NULL AND DestinationType IS NOT NULL AND LoanReason IS NOT NULL)
        ),
        CONSTRAINT CK_ToolIssuances_CustodyStatus CHECK (
            (IssueType = N'Custody' AND Status IN (N'Outstanding', N'PartiallyReturned', N'Returned'))
            OR
            (IssueType = N'Operational' AND Status IN (N'Posted', N'Reversed'))
        ),
        CONSTRAINT CK_ToolIssuances_AccountingEvent CHECK (
            (IssueType = N'Operational' AND AccountingEventId IS NOT NULL)
            OR
            (IssueType = N'Custody' AND AccountingEventId IS NULL)
        ),
        CONSTRAINT FK_ToolIssuances_InventoryItem FOREIGN KEY (InventoryItemId) REFERENCES dbo.InventoryItems(InventoryItemID),
        CONSTRAINT FK_ToolIssuances_AccountingEvent FOREIGN KEY (AccountingEventId) REFERENCES dbo.AccountingEvents(AccountingEventId),
        CONSTRAINT FK_ToolIssuances_InventoryTransaction FOREIGN KEY (InventoryTransactionId) REFERENCES dbo.InventoryTransactions(TransactionID)
    );

    CREATE TABLE dbo.ToolIssuanceReturns
    (
        ToolIssuanceReturnId bigint IDENTITY(1,1) NOT NULL,
        ToolIssuanceId bigint NOT NULL,
        ReturnedQuantity decimal(18,3) NOT NULL,
        ReturnNotes nvarchar(1000) NULL,
        InventoryTransactionId int NULL,
        SourceOperationId uniqueidentifier NOT NULL,
        ConfirmedByUserId int NULL,
        CreatedAt datetime2(7) NOT NULL CONSTRAINT DF_ToolIssuanceReturns_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_ToolIssuanceReturns PRIMARY KEY (ToolIssuanceReturnId),
        CONSTRAINT UQ_ToolIssuanceReturns_SourceOperation UNIQUE (SourceOperationId),
        CONSTRAINT CK_ToolIssuanceReturns_Quantity CHECK (ReturnedQuantity > 0),
        CONSTRAINT FK_ToolIssuanceReturns_ToolIssuance FOREIGN KEY (ToolIssuanceId) REFERENCES dbo.ToolIssuances(ToolIssuanceId),
        CONSTRAINT FK_ToolIssuanceReturns_InventoryTransaction FOREIGN KEY (InventoryTransactionId) REFERENCES dbo.InventoryTransactions(TransactionID)
    );

    CREATE TABLE dbo.ToolIssuanceReversals
    (
        ToolIssuanceReversalId bigint IDENTITY(1,1) NOT NULL,
        ToolIssuanceId bigint NOT NULL,
        PostingAmount decimal(18,2) NOT NULL,
        ReversalReason nvarchar(500) NOT NULL,
        Notes nvarchar(1000) NOT NULL,
        AccountingEventId bigint NULL,
        InventoryTransactionId int NULL,
        SourceOperationId uniqueidentifier NOT NULL,
        ReversedBy int NULL,
        CreatedAt datetime2(7) NOT NULL CONSTRAINT DF_ToolIssuanceReversals_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_ToolIssuanceReversals PRIMARY KEY (ToolIssuanceReversalId),
        CONSTRAINT UQ_ToolIssuanceReversals_ToolIssuance UNIQUE (ToolIssuanceId),
        CONSTRAINT UQ_ToolIssuanceReversals_SourceOperation UNIQUE (SourceOperationId),
        CONSTRAINT CK_ToolIssuanceReversals_Amount CHECK (PostingAmount > 0),
        CONSTRAINT FK_ToolIssuanceReversals_ToolIssuance FOREIGN KEY (ToolIssuanceId) REFERENCES dbo.ToolIssuances(ToolIssuanceId),
        CONSTRAINT FK_ToolIssuanceReversals_AccountingEvent FOREIGN KEY (AccountingEventId) REFERENCES dbo.AccountingEvents(AccountingEventId),
        CONSTRAINT FK_ToolIssuanceReversals_InventoryTransaction FOREIGN KEY (InventoryTransactionId) REFERENCES dbo.InventoryTransactions(TransactionID)
    );

    ALTER TABLE dbo.AccountingEvents
        ADD ToolIssuanceId bigint NULL,
            ToolIssuanceReversalId bigint NULL;

    ALTER TABLE dbo.AccountingEvents
        ADD CONSTRAINT FK_AccountingEvents_ToolIssuance
            FOREIGN KEY (ToolIssuanceId) REFERENCES dbo.ToolIssuances(ToolIssuanceId),
            CONSTRAINT FK_AccountingEvents_ToolIssuanceReversal
            FOREIGN KEY (ToolIssuanceReversalId) REFERENCES dbo.ToolIssuanceReversals(ToolIssuanceReversalId);

    EXEC(N'
        ALTER TABLE dbo.AccountingEvents DROP CONSTRAINT CK_AccountingEvents_Type;
        ALTER TABLE dbo.AccountingEvents ADD
            CONSTRAINT CK_AccountingEvents_Type
                CHECK (AccountingEventType BETWEEN 1 AND 15);');

    EXEC(N'
        ALTER TABLE dbo.AccountingEvents DROP CONSTRAINT CK_AccountingEvents_SourceCardinality;
        ALTER TABLE dbo.AccountingEvents ADD
            CONSTRAINT CK_AccountingEvents_SourceCardinality
                CHECK
                (
                       (AccountingEventType IN (1, 2)
                        AND PaymentId IS NOT NULL AND OrderId IS NULL
                        AND MeasurementCardPrintHistoryId IS NULL AND ReadyMadeInventoryProductId IS NULL
                        AND CustomerAdvanceApplicationId IS NULL AND InventoryReceiptPostingId IS NULL
                        AND FabricConsumptionSourceId IS NULL AND ProductionMaterialConsumptionId IS NULL
                        AND ReadyMadeSaleCostPostingId IS NULL AND ImportedReadyMadeInventoryReceiptId IS NULL
                        AND ImportedReadyMadeSaleCostPostingId IS NULL
                        AND ToolIssuanceId IS NULL AND ToolIssuanceReversalId IS NULL)
                    OR (AccountingEventType = 3
                        AND PaymentId IS NULL AND OrderId IS NOT NULL
                        AND MeasurementCardPrintHistoryId IS NULL AND ReadyMadeInventoryProductId IS NULL
                        AND CustomerAdvanceApplicationId IS NULL AND InventoryReceiptPostingId IS NULL
                        AND FabricConsumptionSourceId IS NULL AND ProductionMaterialConsumptionId IS NULL
                        AND ReadyMadeSaleCostPostingId IS NULL AND ImportedReadyMadeInventoryReceiptId IS NULL
                        AND ImportedReadyMadeSaleCostPostingId IS NULL
                        AND ToolIssuanceId IS NULL AND ToolIssuanceReversalId IS NULL)
                    OR (AccountingEventType = 4
                        AND PaymentId IS NULL AND OrderId IS NULL
                        AND MeasurementCardPrintHistoryId IS NOT NULL AND ReadyMadeInventoryProductId IS NULL
                        AND CustomerAdvanceApplicationId IS NULL AND InventoryReceiptPostingId IS NULL
                        AND FabricConsumptionSourceId IS NULL AND ProductionMaterialConsumptionId IS NULL
                        AND ReadyMadeSaleCostPostingId IS NULL AND ImportedReadyMadeInventoryReceiptId IS NULL
                        AND ImportedReadyMadeSaleCostPostingId IS NULL
                        AND ToolIssuanceId IS NULL AND ToolIssuanceReversalId IS NULL)
                    OR (AccountingEventType = 5
                        AND PaymentId IS NULL AND OrderId IS NULL
                        AND MeasurementCardPrintHistoryId IS NULL AND ReadyMadeInventoryProductId IS NOT NULL
                        AND CustomerAdvanceApplicationId IS NULL AND InventoryReceiptPostingId IS NULL
                        AND FabricConsumptionSourceId IS NULL AND ProductionMaterialConsumptionId IS NULL
                        AND ReadyMadeSaleCostPostingId IS NULL AND ImportedReadyMadeInventoryReceiptId IS NULL
                        AND ImportedReadyMadeSaleCostPostingId IS NULL
                        AND ToolIssuanceId IS NULL AND ToolIssuanceReversalId IS NULL)
                    OR (AccountingEventType = 6
                        AND PaymentId IS NULL AND OrderId IS NULL
                        AND MeasurementCardPrintHistoryId IS NULL AND ReadyMadeInventoryProductId IS NULL
                        AND CustomerAdvanceApplicationId IS NOT NULL AND InventoryReceiptPostingId IS NULL
                        AND FabricConsumptionSourceId IS NULL AND ProductionMaterialConsumptionId IS NULL
                        AND ReadyMadeSaleCostPostingId IS NULL AND ImportedReadyMadeInventoryReceiptId IS NULL
                        AND ImportedReadyMadeSaleCostPostingId IS NULL
                        AND ToolIssuanceId IS NULL AND ToolIssuanceReversalId IS NULL)
                    OR (AccountingEventType IN (7, 9)
                        AND PaymentId IS NULL AND OrderId IS NULL
                        AND MeasurementCardPrintHistoryId IS NULL AND ReadyMadeInventoryProductId IS NULL
                        AND CustomerAdvanceApplicationId IS NULL AND InventoryReceiptPostingId IS NOT NULL
                        AND FabricConsumptionSourceId IS NULL AND ProductionMaterialConsumptionId IS NULL
                        AND ReadyMadeSaleCostPostingId IS NULL AND ImportedReadyMadeInventoryReceiptId IS NULL
                        AND ImportedReadyMadeSaleCostPostingId IS NULL
                        AND ToolIssuanceId IS NULL AND ToolIssuanceReversalId IS NULL)
                    OR (AccountingEventType = 8
                        AND PaymentId IS NULL AND OrderId IS NULL
                        AND MeasurementCardPrintHistoryId IS NULL AND ReadyMadeInventoryProductId IS NULL
                        AND CustomerAdvanceApplicationId IS NULL AND InventoryReceiptPostingId IS NULL
                        AND FabricConsumptionSourceId IS NOT NULL AND ProductionMaterialConsumptionId IS NULL
                        AND ReadyMadeSaleCostPostingId IS NULL AND ImportedReadyMadeInventoryReceiptId IS NULL
                        AND ImportedReadyMadeSaleCostPostingId IS NULL
                        AND ToolIssuanceId IS NULL AND ToolIssuanceReversalId IS NULL)
                );');
