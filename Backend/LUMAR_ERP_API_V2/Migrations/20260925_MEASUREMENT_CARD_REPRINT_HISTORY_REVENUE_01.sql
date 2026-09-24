SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID('dbo.MeasurementCardPrintHistory', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.MeasurementCardPrintHistory
    (
        PrintHistoryId int IDENTITY(1,1) NOT NULL,
        PrintRequestId uniqueidentifier NOT NULL,
        PrintStatus nvarchar(20) NOT NULL CONSTRAINT DF_MeasurementCardPrintHistory_PrintStatus DEFAULT N'Reserved',
        OrderId int NULL,
        OrderItemId int NULL,
        PieceId int NULL,
        ReadyMadeProductionOrderId int NULL,
        ReadyMadeProductionOrderItemId int NULL,
        ReadyMadePieceId int NULL,
        TrackingCode nvarchar(450) NOT NULL,
        PrintedAtUtc datetime2 NULL,
        PrintedByUserId int NOT NULL,
        PrintedByDisplayName nvarchar(200) NOT NULL,
        CopyNumber int NOT NULL,
        ReprintReasonCode nvarchar(40) NULL,
        DamageReason nvarchar(500) NULL,
        ResponsibleEmployeeId int NULL,
        Notes nvarchar(1000) NULL,
        SaleAmount decimal(18,2) NULL,
        SalePaymentType nvarchar(20) NULL,
        SaleCustomerId int NULL,
        FinancialTransactionReference nvarchar(200) NULL,
        FinancialTransactionId int NULL,
        JournalEntryId int NULL,
        CustomerLedgerEntryId int NULL,
        PaymentId int NULL,
        PaymentReferenceNumber nvarchar(200) NULL,
        PaymentFinancialTransactionId int NULL,
        PaymentJournalEntryId int NULL,
        PaymentCustomerLedgerEntryId int NULL,
        CompletedAtUtc datetime2 NULL,
        FailedAtUtc datetime2 NULL,
        FailureReason nvarchar(500) NULL,
        CreatedAt datetime2 NOT NULL CONSTRAINT DF_MeasurementCardPrintHistory_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_MeasurementCardPrintHistory PRIMARY KEY (PrintHistoryId),
        CONSTRAINT FK_MeasurementCardPrintHistory_Orders FOREIGN KEY (OrderId) REFERENCES dbo.Orders(OrderID),
        CONSTRAINT FK_MeasurementCardPrintHistory_OrderItems FOREIGN KEY (OrderItemId) REFERENCES dbo.OrderItems(OrderItemID),
        CONSTRAINT FK_MeasurementCardPrintHistory_Pieces FOREIGN KEY (PieceId) REFERENCES dbo.Pieces(PieceID),
        CONSTRAINT FK_MeasurementCardPrintHistory_ReadyMadeOrders FOREIGN KEY (ReadyMadeProductionOrderId) REFERENCES dbo.ReadyMadeProductionOrders(ReadyMadeProductionOrderId),
        CONSTRAINT FK_MeasurementCardPrintHistory_ReadyMadeItems FOREIGN KEY (ReadyMadeProductionOrderItemId) REFERENCES dbo.ReadyMadeProductionOrderItems(ReadyMadeProductionOrderItemId),
        CONSTRAINT FK_MeasurementCardPrintHistory_ReadyMadePieces FOREIGN KEY (ReadyMadePieceId) REFERENCES dbo.ReadyMadeProductionOrderPieceInstances(ReadyMadeProductionOrderPieceInstanceId),
        CONSTRAINT FK_MeasurementCardPrintHistory_Users FOREIGN KEY (PrintedByUserId) REFERENCES dbo.Users(UserID),
        CONSTRAINT FK_MeasurementCardPrintHistory_Employees FOREIGN KEY (ResponsibleEmployeeId) REFERENCES dbo.Employees(EmployeeID),
        CONSTRAINT FK_MeasurementCardPrintHistory_SaleCustomers FOREIGN KEY (SaleCustomerId) REFERENCES dbo.Customers(CustomerID),
        CONSTRAINT FK_MeasurementCardPrintHistory_FinancialTransactions FOREIGN KEY (FinancialTransactionId) REFERENCES dbo.FinancialTransactions(FinancialTransactionId),
        CONSTRAINT FK_MeasurementCardPrintHistory_JournalEntries FOREIGN KEY (JournalEntryId) REFERENCES dbo.JournalEntries(JournalEntryId),
        CONSTRAINT FK_MeasurementCardPrintHistory_CustomerLedgerEntries FOREIGN KEY (CustomerLedgerEntryId) REFERENCES dbo.CustomerLedgerEntries(CustomerLedgerEntryId),
        CONSTRAINT FK_MeasurementCardPrintHistory_PaymentFinancialTransactions FOREIGN KEY (PaymentFinancialTransactionId) REFERENCES dbo.FinancialTransactions(FinancialTransactionId),
        CONSTRAINT FK_MeasurementCardPrintHistory_PaymentJournalEntries FOREIGN KEY (PaymentJournalEntryId) REFERENCES dbo.JournalEntries(JournalEntryId),
        CONSTRAINT FK_MeasurementCardPrintHistory_PaymentCustomerLedgerEntries FOREIGN KEY (PaymentCustomerLedgerEntryId) REFERENCES dbo.CustomerLedgerEntries(CustomerLedgerEntryId),
        CONSTRAINT CK_MeasurementCardPrintHistory_PrintStatus CHECK (PrintStatus IN (N'Reserved', N'Completed', N'Failed')),
        CONSTRAINT CK_MeasurementCardPrintHistory_Identity CHECK
        (
            (PieceId IS NOT NULL AND OrderId IS NOT NULL AND OrderItemId IS NOT NULL AND ReadyMadePieceId IS NULL AND ReadyMadeProductionOrderId IS NULL AND ReadyMadeProductionOrderItemId IS NULL)
            OR
            (PieceId IS NULL AND OrderId IS NULL AND OrderItemId IS NULL AND ReadyMadePieceId IS NOT NULL AND ReadyMadeProductionOrderId IS NOT NULL AND ReadyMadeProductionOrderItemId IS NOT NULL)
        ),
        CONSTRAINT CK_MeasurementCardPrintHistory_CopyNumber CHECK (CopyNumber > 0),
        CONSTRAINT CK_MeasurementCardPrintHistory_ReprintReason CHECK
        (
            (CopyNumber = 1 AND ReprintReasonCode IS NULL)
            OR
            (CopyNumber > 1 AND ReprintReasonCode IN (N'DamagedCard', N'LostCard', N'DamagedPiece', N'PieceSold'))
        ),
        CONSTRAINT CK_MeasurementCardPrintHistory_DamageData CHECK
        (
            (ReprintReasonCode = N'DamagedPiece' AND NULLIF(LTRIM(RTRIM(DamageReason)), N'') IS NOT NULL AND ResponsibleEmployeeId IS NOT NULL)
            OR
            (ReprintReasonCode <> N'DamagedPiece' OR ReprintReasonCode IS NULL) AND DamageReason IS NULL AND ResponsibleEmployeeId IS NULL
        ),
        CONSTRAINT CK_MeasurementCardPrintHistory_SaleData CHECK
        (
            (ReprintReasonCode = N'PieceSold' AND SaleAmount > 0 AND SalePaymentType IN (N'Cash', N'Credit') AND SaleCustomerId IS NOT NULL)
            OR
            (ReprintReasonCode <> N'PieceSold' OR ReprintReasonCode IS NULL) AND SaleAmount IS NULL AND SalePaymentType IS NULL AND SaleCustomerId IS NULL
        ),
        CONSTRAINT CK_MeasurementCardPrintHistory_Completion CHECK
        (
            (PrintStatus = N'Completed' AND PrintedAtUtc IS NOT NULL AND CompletedAtUtc IS NOT NULL AND FailedAtUtc IS NULL)
            OR
            (PrintStatus = N'Reserved' AND PrintedAtUtc IS NULL AND CompletedAtUtc IS NULL AND FailedAtUtc IS NULL)
            OR
            (PrintStatus = N'Failed' AND PrintedAtUtc IS NULL AND CompletedAtUtc IS NULL AND FailedAtUtc IS NOT NULL)
        )
    );
END;

IF COL_LENGTH('dbo.Payments', 'PrintHistoryId') IS NULL
BEGIN
    EXEC(N'ALTER TABLE dbo.Payments ADD PrintHistoryId int NULL;');
END;

IF EXISTS
(
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'Payments' AND COLUMN_NAME = 'OrderID' AND IS_NULLABLE = 'NO'
)
BEGIN
    EXEC(N'ALTER TABLE dbo.Payments ALTER COLUMN OrderID int NULL;');
END;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Payments_MeasurementCardPrintHistory')
BEGIN
    EXEC(N'ALTER TABLE dbo.Payments ADD CONSTRAINT FK_Payments_MeasurementCardPrintHistory FOREIGN KEY (PrintHistoryId) REFERENCES dbo.MeasurementCardPrintHistory(PrintHistoryId);');
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_MeasurementCardPrintHistory_PrintRequestId' AND object_id = OBJECT_ID(N'dbo.MeasurementCardPrintHistory'))
    CREATE UNIQUE INDEX UX_MeasurementCardPrintHistory_PrintRequestId ON dbo.MeasurementCardPrintHistory(PrintRequestId);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_MeasurementCardPrintHistory_Piece_CopyNumber' AND object_id = OBJECT_ID(N'dbo.MeasurementCardPrintHistory'))
    CREATE UNIQUE INDEX UX_MeasurementCardPrintHistory_Piece_CopyNumber
        ON dbo.MeasurementCardPrintHistory(PieceId, CopyNumber)
        WHERE PieceId IS NOT NULL AND PrintStatus IN (N'Reserved', N'Completed');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_MeasurementCardPrintHistory_ReadyMadePiece_CopyNumber' AND object_id = OBJECT_ID(N'dbo.MeasurementCardPrintHistory'))
    CREATE UNIQUE INDEX UX_MeasurementCardPrintHistory_ReadyMadePiece_CopyNumber
        ON dbo.MeasurementCardPrintHistory(ReadyMadePieceId, CopyNumber)
        WHERE ReadyMadePieceId IS NOT NULL AND PrintStatus IN (N'Reserved', N'Completed');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_MeasurementCardPrintHistory_FinancialTransactionReference' AND object_id = OBJECT_ID(N'dbo.MeasurementCardPrintHistory'))
    CREATE UNIQUE INDEX UX_MeasurementCardPrintHistory_FinancialTransactionReference
        ON dbo.MeasurementCardPrintHistory(FinancialTransactionReference)
        WHERE FinancialTransactionReference IS NOT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Payments_MeasurementCardPrintHistory' AND object_id = OBJECT_ID(N'dbo.Payments'))
    EXEC(N'CREATE UNIQUE INDEX UX_Payments_MeasurementCardPrintHistory ON dbo.Payments(PrintHistoryId) WHERE PrintHistoryId IS NOT NULL;');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MeasurementCardPrintHistory_Piece' AND object_id = OBJECT_ID(N'dbo.MeasurementCardPrintHistory'))
    CREATE INDEX IX_MeasurementCardPrintHistory_Piece ON dbo.MeasurementCardPrintHistory(PieceId, CreatedAt DESC);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_MeasurementCardPrintHistory_ReadyMadePiece' AND object_id = OBJECT_ID(N'dbo.MeasurementCardPrintHistory'))
    CREATE INDEX IX_MeasurementCardPrintHistory_ReadyMadePiece ON dbo.MeasurementCardPrintHistory(ReadyMadePieceId, CreatedAt DESC);

COMMIT TRANSACTION;
