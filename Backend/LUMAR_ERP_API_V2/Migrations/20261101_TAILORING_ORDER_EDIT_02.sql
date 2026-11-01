SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
SET NOCOUNT ON;
IF DB_NAME() NOT IN (N'LUMAR_ERP_ES_VALIDATION', N'LUMAR_TAILORING_EDIT_TEST_20261101')
    THROW 51000, 'This migration is restricted to the approved tailoring validation databases.', 1;
BEGIN TRY
    BEGIN TRANSACTION;
    IF COL_LENGTH(N'dbo.Orders',N'EditVersion') IS NULL
        ALTER TABLE dbo.Orders ADD EditVersion rowversion;
    IF COL_LENGTH(N'dbo.TailoringPieceSaleValues',N'OriginalBasePoints') IS NULL
        ALTER TABLE dbo.TailoringPieceSaleValues ADD OriginalBasePoints decimal(18,4) NULL,
            BenefitsCaptured bit NOT NULL CONSTRAINT DF_TailoringPieceSaleValues_BenefitsCaptured DEFAULT(0);
    IF OBJECT_ID(N'dbo.TailoringPieceBenefitBalances',N'U') IS NULL
        CREATE TABLE dbo.TailoringPieceBenefitBalances
        (
            PieceId int NOT NULL CONSTRAINT FK_TailoringPieceBenefits_Piece REFERENCES dbo.Pieces(PieceID),
            CustomerId int NOT NULL CONSTRAINT FK_TailoringPieceBenefits_Customer REFERENCES dbo.Customers(CustomerID),
            BenefitKind nvarchar(20) NOT NULL,
            ReferralLevel tinyint NOT NULL,
            OriginalPoints decimal(18,4) NOT NULL,
            CurrentPoints decimal(18,4) NOT NULL,
            OriginalLoyaltyTransactionId bigint NULL CONSTRAINT FK_TailoringPieceBenefits_Loyalty REFERENCES dbo.LoyaltyTransactions(LoyaltyTransactionId),
            OriginalReferralTransactionId bigint NULL CONSTRAINT FK_TailoringPieceBenefits_Referral REFERENCES dbo.ReferralTransactions(ReferralTransactionId),
            CONSTRAINT PK_TailoringPieceBenefitBalances PRIMARY KEY(PieceId,CustomerId,BenefitKind),
            CONSTRAINT CK_TailoringPieceBenefits_Values CHECK(OriginalPoints>=0 AND CurrentPoints>=0 AND
                ((BenefitKind=N'Buyer' AND ReferralLevel=0) OR (BenefitKind=N'Referral' AND ReferralLevel BETWEEN 1 AND 4)))
        );
    IF OBJECT_ID(N'dbo.TailoringOrderEditOperations',N'U') IS NULL
        CREATE TABLE dbo.TailoringOrderEditOperations
        (
            EditOperationId uniqueidentifier NOT NULL CONSTRAINT PK_TailoringOrderEditOperations PRIMARY KEY,
            OrderId int NOT NULL CONSTRAINT FK_TailoringOrderEditOperations_Order REFERENCES dbo.Orders(OrderID),
            RequestHash binary(32) NOT NULL,
            ResultJson nvarchar(max) NULL,
            CreatedBy nvarchar(100) NOT NULL,
            CreatedAt datetime2(7) NOT NULL,
            CONSTRAINT CK_TailoringOrderEditOperations_Json CHECK(ResultJson IS NULL OR ISJSON(ResultJson)=1)
        );
    IF OBJECT_ID(N'dbo.TailoringPieceAddOperations',N'U') IS NULL
        CREATE TABLE dbo.TailoringPieceAddOperations
        (
            AddOperationId uniqueidentifier NOT NULL CONSTRAINT PK_TailoringPieceAddOperations PRIMARY KEY,
            EditOperationId uniqueidentifier NOT NULL CONSTRAINT FK_TailoringPieceAddOperations_Edit REFERENCES dbo.TailoringOrderEditOperations(EditOperationId),
            OrderId int NOT NULL CONSTRAINT FK_TailoringPieceAddOperations_Order REFERENCES dbo.Orders(OrderID),
            PieceId int NOT NULL CONSTRAINT FK_TailoringPieceAddOperations_Piece REFERENCES dbo.Pieces(PieceID),
            RequestHash binary(32) NOT NULL,
            CONSTRAINT UQ_TailoringPieceAddOperations_Piece UNIQUE(PieceId)
        );
    IF OBJECT_ID(N'dbo.TailoringPieceCancellations',N'U') IS NULL
        CREATE TABLE dbo.TailoringPieceCancellations
        (
            PieceId int NOT NULL CONSTRAINT PK_TailoringPieceCancellations PRIMARY KEY
                CONSTRAINT FK_TailoringPieceCancellations_Piece REFERENCES dbo.Pieces(PieceID),
            EditOperationId uniqueidentifier NOT NULL CONSTRAINT FK_TailoringPieceCancellations_Edit REFERENCES dbo.TailoringOrderEditOperations(EditOperationId),
            Reason nvarchar(500) NOT NULL,
            CancelledAt datetime2(7) NOT NULL
        );
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;