SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
SET NOCOUNT ON;

IF DB_NAME() NOT IN (N'LUMAR_ERP_ES_VALIDATION', N'LUMAR_TAILORING_EDIT_TEST_20261101')
    THROW 51000, 'This migration is restricted to the approved tailoring validation databases.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.TailoringPieceSaleValues', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.TailoringPieceSaleValues
        (
            PieceID int NOT NULL CONSTRAINT PK_TailoringPieceSaleValues PRIMARY KEY,
            OriginalSalePrice decimal(18,2) NOT NULL,
            ApprovedSalePrice decimal(18,2) NOT NULL,
            OriginalDiscountShare decimal(18,2) NOT NULL,
            DiscountShare decimal(18,2) NOT NULL,
            OriginalPricingSnapshot nvarchar(max) NOT NULL,
            ApprovedPricingSnapshot nvarchar(max) NOT NULL,
            CreatedAt datetime2(7) NOT NULL,
            UpdatedAt datetime2(7) NOT NULL,
            CONSTRAINT FK_TailoringPieceSaleValues_Pieces FOREIGN KEY (PieceID) REFERENCES dbo.Pieces(PieceID),
            CONSTRAINT CK_TailoringPieceSaleValues_Amounts CHECK
              (OriginalSalePrice >= 0 AND ApprovedSalePrice >= 0 AND
               OriginalDiscountShare >= 0 AND OriginalDiscountShare <= OriginalSalePrice AND
               DiscountShare >= 0 AND DiscountShare <= ApprovedSalePrice),
            CONSTRAINT CK_TailoringPieceSaleValues_Json CHECK
              (ISJSON(OriginalPricingSnapshot) = 1 AND ISJSON(ApprovedPricingSnapshot) = 1)
        );
    END;

    IF OBJECT_ID(N'dbo.FabricConsumptionPartialReversals', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.FabricConsumptionPartialReversals
        (
            FabricConsumptionPartialReversalId bigint IDENTITY NOT NULL CONSTRAINT PK_FabricConsumptionPartialReversals PRIMARY KEY,
            FabricConsumptionSourceId bigint NOT NULL,
            PieceId int NOT NULL,
            SourceOperationId uniqueidentifier NOT NULL CONSTRAINT UQ_FabricConsumptionPartialReversals_Operation UNIQUE,
            ReturnedQuantity decimal(18,6) NOT NULL,
            OperationalAmount decimal(18,6) NOT NULL,
            PostingAmount decimal(18,2) NOT NULL,
            AccountingEventId bigint NULL,
            InventoryTransactionId int NULL,
            Reason nvarchar(500) NOT NULL,
            ReversedBy nvarchar(100) NOT NULL,
            CreatedAt datetime2(7) NOT NULL,
            CONSTRAINT FK_FabricPartialReversal_Source FOREIGN KEY(FabricConsumptionSourceId) REFERENCES dbo.FabricConsumptionSources(FabricConsumptionSourceId),
            CONSTRAINT FK_FabricPartialReversal_Piece FOREIGN KEY(PieceId) REFERENCES dbo.Pieces(PieceID),
            CONSTRAINT FK_FabricPartialReversal_Event FOREIGN KEY(AccountingEventId) REFERENCES dbo.AccountingEvents(AccountingEventId),
            CONSTRAINT FK_FabricPartialReversal_Movement FOREIGN KEY(InventoryTransactionId) REFERENCES dbo.InventoryTransactions(TransactionID),
            CONSTRAINT CK_FabricPartialReversal_Positive CHECK(ReturnedQuantity>0 AND OperationalAmount>0 AND PostingAmount>0)
        );
        CREATE INDEX IX_FabricConsumptionPartialReversals_Source ON dbo.FabricConsumptionPartialReversals(FabricConsumptionSourceId);
        DROP INDEX UX_AccountingEvents_Original_Reversal ON dbo.AccountingEvents;
        CREATE UNIQUE INDEX UX_AccountingEvents_Original_Reversal ON dbo.AccountingEvents(OriginalAccountingEventId)
            WHERE AccountingEventType=33 AND SourceType<>N'FabricConsumptionPartialReversal';
    END;

    DECLARE @sourceCheck nvarchar(max) = (SELECT definition FROM sys.check_constraints
        WHERE parent_object_id=OBJECT_ID(N'dbo.AccountingEvents') AND name=N'CK_AccountingEvents_SourceCardinality');
    IF @sourceCheck IS NULL THROW 51002, 'The official accounting source constraint is missing.', 1;
    IF CHARINDEX(N'FabricConsumptionPartialReversal', @sourceCheck)=0
    BEGIN
        ALTER TABLE dbo.AccountingEvents DROP CONSTRAINT CK_AccountingEvents_SourceCardinality;
        DECLARE @sourceSql nvarchar(max) = N'ALTER TABLE dbo.AccountingEvents WITH CHECK ADD CONSTRAINT CK_AccountingEvents_SourceCardinality CHECK ('
            + @sourceCheck + N' OR (AccountingEventType=33 AND OriginalAccountingEventId IS NOT NULL
              AND SourceType IN(N''AccountingEvent'',N''FabricConsumptionPartialReversal'')
              AND SourceId IS NOT NULL AND SourceOperationId IS NOT NULL
              AND (SourceType<>N''AccountingEvent'' OR SourceId=OriginalAccountingEventId)
              AND PaymentId IS NULL AND OrderId IS NULL AND MeasurementCardPrintHistoryId IS NULL
              AND ReadyMadeInventoryProductId IS NULL AND CustomerAdvanceApplicationId IS NULL
              AND InventoryReceiptPostingId IS NULL AND FabricConsumptionSourceId IS NULL
              AND ProductionMaterialConsumptionId IS NULL AND ReadyMadeSaleCostPostingId IS NULL
              AND ImportedReadyMadeInventoryReceiptId IS NULL AND ImportedReadyMadeSaleCostPostingId IS NULL
              AND ToolIssuanceId IS NULL AND ToolIssuanceReversalId IS NULL))';
        EXEC sys.sp_executesql @sourceSql;
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;