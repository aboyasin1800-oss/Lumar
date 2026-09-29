SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() NOT IN (N'LUMAR_ERP_TEST', N'LUMAR_ERP_ES_VALIDATION')
    THROW 52330, N'ES-5E is restricted to approved ES validation databases.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.Suppliers', N'U') IS NULL
       OR OBJECT_ID(N'dbo.SupplierInvoices', N'U') IS NULL
       OR OBJECT_ID(N'dbo.SupplierPayments', N'U') IS NULL
       OR OBJECT_ID(N'dbo.SupplierPaymentAllocations', N'U') IS NULL
        THROW 52331, N'ES-5E requires the existing supplier financial tables.', 1;

    IF OBJECT_ID(N'dbo.SupplierFinancialInvoices', N'U') IS NOT NULL
       OR OBJECT_ID(N'dbo.SupplierFinancialPayments', N'U') IS NOT NULL
       OR OBJECT_ID(N'dbo.SupplierFinancialPaymentAllocations', N'U') IS NOT NULL
        THROW 52332, N'ES-5E runtime tables already exist.', 1;

    CREATE TABLE dbo.SupplierFinancialInvoices
    (
        SupplierInvoiceId int NOT NULL,
        SourceOperationId uniqueidentifier NOT NULL,
        AccountingEventId bigint NOT NULL,
        Status nvarchar(20) NOT NULL,
        CreatedBy nvarchar(100) NOT NULL,
        CreatedAt datetime2(7) NOT NULL CONSTRAINT DF_SupplierFinancialInvoices_CreatedAt DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_SupplierFinancialInvoices PRIMARY KEY (SupplierInvoiceId),
        CONSTRAINT UQ_SupplierFinancialInvoices_SourceOperation UNIQUE (SourceOperationId),
        CONSTRAINT FK_SupplierFinancialInvoices_Invoice FOREIGN KEY (SupplierInvoiceId) REFERENCES dbo.SupplierInvoices(SupplierInvoiceId),
        CONSTRAINT FK_SupplierFinancialInvoices_Event FOREIGN KEY (AccountingEventId) REFERENCES dbo.AccountingEvents(AccountingEventId),
        CONSTRAINT CK_SupplierFinancialInvoices_Status CHECK (Status IN (N'Posted', N'Reversed'))
    );

    CREATE TABLE dbo.SupplierFinancialPayments
    (
        SupplierPaymentId int NOT NULL,
        CashAccountId int NOT NULL,
        PaymentKind nvarchar(30) NOT NULL,
        CurrencyCode char(3) NOT NULL,
        SourceOperationId uniqueidentifier NOT NULL,
        AccountingEventId bigint NOT NULL,
        OriginalSupplierPaymentId int NULL,
        ReversalReason nvarchar(500) NULL,
        ReversedBy nvarchar(100) NULL,
        ReversedAt datetime2(7) NULL,
        Status nvarchar(20) NOT NULL,
        CreatedBy nvarchar(100) NOT NULL,
        CreatedAt datetime2(7) NOT NULL CONSTRAINT DF_SupplierFinancialPayments_CreatedAt DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_SupplierFinancialPayments PRIMARY KEY (SupplierPaymentId),
        CONSTRAINT UQ_SupplierFinancialPayments_SourceOperation UNIQUE (SourceOperationId),
        CONSTRAINT FK_SupplierFinancialPayments_Payment FOREIGN KEY (SupplierPaymentId) REFERENCES dbo.SupplierPayments(SupplierPaymentId),
        CONSTRAINT FK_SupplierFinancialPayments_Cash FOREIGN KEY (CashAccountId) REFERENCES dbo.CashAccounts(CashAccountId),
        CONSTRAINT FK_SupplierFinancialPayments_Event FOREIGN KEY (AccountingEventId) REFERENCES dbo.AccountingEvents(AccountingEventId),
        CONSTRAINT FK_SupplierFinancialPayments_Original FOREIGN KEY (OriginalSupplierPaymentId) REFERENCES dbo.SupplierPayments(SupplierPaymentId),
        CONSTRAINT CK_SupplierFinancialPayments_Kind CHECK (PaymentKind IN (N'Immediate', N'Later', N'Advance', N'Reversal')),
        CONSTRAINT CK_SupplierFinancialPayments_Status CHECK (Status IN (N'Posted', N'Reversed')),
        CONSTRAINT CK_SupplierFinancialPayments_Reversal CHECK
        (
            (PaymentKind <> N'Reversal' AND OriginalSupplierPaymentId IS NULL AND ReversalReason IS NULL AND ReversedBy IS NULL AND ReversedAt IS NULL)
            OR
            (PaymentKind = N'Reversal' AND OriginalSupplierPaymentId IS NOT NULL AND ReversalReason IS NOT NULL AND ReversedBy IS NOT NULL AND ReversedAt IS NOT NULL)
        )
    );

    CREATE TABLE dbo.SupplierFinancialPaymentAllocations
    (
        SupplierPaymentAllocationId int NOT NULL,
        SourceOperationId uniqueidentifier NOT NULL,
        AccountingEventId bigint NOT NULL,
        Status nvarchar(20) NOT NULL,
        CreatedBy nvarchar(100) NOT NULL,
        CreatedAt datetime2(7) NOT NULL CONSTRAINT DF_SupplierFinancialPaymentAllocations_CreatedAt DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_SupplierFinancialPaymentAllocations PRIMARY KEY (SupplierPaymentAllocationId),
        CONSTRAINT UQ_SupplierFinancialPaymentAllocations_SourceOperation UNIQUE (SourceOperationId),
        CONSTRAINT FK_SupplierFinancialPaymentAllocations_Allocation FOREIGN KEY (SupplierPaymentAllocationId) REFERENCES dbo.SupplierPaymentAllocations(SupplierPaymentAllocationId),
        CONSTRAINT FK_SupplierFinancialPaymentAllocations_Event FOREIGN KEY (AccountingEventId) REFERENCES dbo.AccountingEvents(AccountingEventId),
        CONSTRAINT CK_SupplierFinancialPaymentAllocations_Status CHECK (Status IN (N'Posted', N'Reversed'))
    );

    CREATE INDEX IX_SupplierFinancialPayments_Status ON dbo.SupplierFinancialPayments(Status, PaymentKind, SupplierPaymentId);
    CREATE UNIQUE INDEX UX_SupplierFinancialPayments_OriginalReversal ON dbo.SupplierFinancialPayments(OriginalSupplierPaymentId) WHERE OriginalSupplierPaymentId IS NOT NULL;
    CREATE INDEX IX_SupplierFinancialPaymentAllocations_Status ON dbo.SupplierFinancialPaymentAllocations(Status, SupplierPaymentAllocationId);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;