SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

IF DB_NAME() <> N'LUMAR_ERP_ES_VALIDATION'
    THROW 51600, N'This Phase SF-3 supplier payment response foundation migration is restricted to LUMAR_ERP_ES_VALIDATION.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.SupplierPayments', N'U') IS NULL
        THROW 51601, N'SupplierPayments is required to build supplier response records.', 1;

    IF OBJECT_ID(N'dbo.Suppliers', N'U') IS NULL
        THROW 51602, N'Suppliers is required to bind supplier ownership to payment response records.', 1;

    IF OBJECT_ID(N'dbo.SupplierPaymentAcknowledgements', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.SupplierPaymentAcknowledgements
        (
            SupplierPaymentAcknowledgementId int IDENTITY(1,1) NOT NULL,
            SupplierPaymentId int NOT NULL,
            SupplierId int NOT NULL,
            Status nvarchar(32) NOT NULL CONSTRAINT DF_SupplierPaymentAcknowledgements_Status DEFAULT N'Acknowledged',
            Notes nvarchar(500) NULL,
            SourceOperationId uniqueidentifier NULL,
            CreatedAt datetime2(3) NOT NULL CONSTRAINT DF_SupplierPaymentAcknowledgements_CreatedAt DEFAULT SYSUTCDATETIME(),
            AcknowledgedAt datetime2(3) NOT NULL CONSTRAINT DF_SupplierPaymentAcknowledgements_AcknowledgedAt DEFAULT SYSUTCDATETIME(),
            CONSTRAINT PK_SupplierPaymentAcknowledgements PRIMARY KEY (SupplierPaymentAcknowledgementId),
            CONSTRAINT FK_SupplierPaymentAcknowledgements_SupplierPayments FOREIGN KEY (SupplierPaymentId) REFERENCES dbo.SupplierPayments(SupplierPaymentId),
            CONSTRAINT FK_SupplierPaymentAcknowledgements_Suppliers FOREIGN KEY (SupplierId) REFERENCES dbo.Suppliers(SupplierId),
            CONSTRAINT CK_SupplierPaymentAcknowledgements_Status CHECK (Status IN (N'Acknowledged'))
        );
    END;

    IF OBJECT_ID(N'dbo.PaymentDisputes', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.PaymentDisputes
        (
            PaymentDisputeId int IDENTITY(1,1) NOT NULL,
            SupplierPaymentId int NOT NULL,
            SupplierId int NOT NULL,
            DisputedAmount decimal(18,2) NULL,
            Status nvarchar(32) NOT NULL CONSTRAINT DF_PaymentDisputes_Status DEFAULT N'Open',
            Reason nvarchar(500) NOT NULL,
            SourceOperationId uniqueidentifier NULL,
            CreatedAt datetime2(3) NOT NULL CONSTRAINT DF_PaymentDisputes_CreatedAt DEFAULT SYSUTCDATETIME(),
            ResolvedAt datetime2(3) NULL,
            CONSTRAINT PK_PaymentDisputes PRIMARY KEY (PaymentDisputeId),
            CONSTRAINT FK_PaymentDisputes_SupplierPayments FOREIGN KEY (SupplierPaymentId) REFERENCES dbo.SupplierPayments(SupplierPaymentId),
            CONSTRAINT FK_PaymentDisputes_Suppliers FOREIGN KEY (SupplierId) REFERENCES dbo.Suppliers(SupplierId),
            CONSTRAINT CK_PaymentDisputes_Status CHECK (Status IN (N'Open', N'Closed', N'Rejected')),
            CONSTRAINT CK_PaymentDisputes_Amount CHECK (DisputedAmount IS NULL OR DisputedAmount >= 0)
        );
    END;

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.SupplierPaymentAcknowledgements') AND name = N'UX_SupplierPaymentAcknowledgements_SupplierPaymentId')
    BEGIN
        CREATE UNIQUE INDEX UX_SupplierPaymentAcknowledgements_SupplierPaymentId
            ON dbo.SupplierPaymentAcknowledgements(SupplierPaymentId)
            WHERE SupplierPaymentId IS NOT NULL;
    END;

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.PaymentDisputes') AND name = N'UX_PaymentDisputes_SupplierPaymentId')
    BEGIN
        CREATE UNIQUE INDEX UX_PaymentDisputes_SupplierPaymentId
            ON dbo.PaymentDisputes(SupplierPaymentId)
            WHERE SupplierPaymentId IS NOT NULL;
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
