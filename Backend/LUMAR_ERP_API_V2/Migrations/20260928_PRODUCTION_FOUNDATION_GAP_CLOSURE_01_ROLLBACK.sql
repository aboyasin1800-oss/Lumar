SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() NOT IN (N'LUMAR_ERP_FOUNDATION_GAP_TEST', N'LUMAR_ERP')
    THROW 51700, N'This rollback is restricted to the isolated foundation test database or LUMAR_ERP.', 1;

IF DB_NAME() = N'LUMAR_ERP'
   AND CONVERT(nvarchar(128), SESSION_CONTEXT(N'AllowProductionFoundationGap')) <> N'APPROVED'
    THROW 51701, N'Production rollback requires an explicit approved session context.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.InventoryFoundationProcedureBackups', N'U') IS NULL
        THROW 51702, N'The foundation procedure backup is missing.', 1;

    DECLARE @backupCount int;
    SELECT @backupCount = COUNT(*) FROM dbo.InventoryFoundationProcedureBackups;
    IF @backupCount <> 1
        THROW 51703, N'The foundation procedure backup table does not contain exactly one rollback record.', 1;

    DECLARE @procedureDefinition nvarchar(max);
    DECLARE @procedureHashBefore char(64);
    DECLARE @oldAccountingEventTypeDefinition nvarchar(max);
    DECLARE @oldSourceCardinalityDefinition nvarchar(max);
    SELECT
        @procedureDefinition = ProcedureDefinition,
        @procedureHashBefore = DefinitionHashBefore,
        @oldAccountingEventTypeDefinition = AccountingEventTypeConstraintDefinition,
        @oldSourceCardinalityDefinition = SourceCardinalityConstraintDefinition
    FROM dbo.InventoryFoundationProcedureBackups;

    IF EXISTS (SELECT 1 FROM dbo.InventoryReceiptPostings)
       OR EXISTS (SELECT 1 FROM dbo.InventoryReceiptLines)
       OR EXISTS (SELECT 1 FROM dbo.FabricRolls)
       OR EXISTS (SELECT 1 FROM dbo.FabricConsumptionSources)
       OR EXISTS (SELECT 1 FROM dbo.InventoryItemFoundation)
        THROW 51704, N'Rollback is blocked because foundation source records already exist.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM dbo.AccountingEvents
        WHERE AccountingEventType IN (7, 8, 9, 10)
           OR InventoryReceiptPostingId IS NOT NULL
           OR FabricConsumptionSourceId IS NOT NULL
           OR ProductionMaterialConsumptionId IS NOT NULL
    )
        THROW 51705, N'Rollback is blocked because foundation accounting events already exist.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM dbo.InventoryTransactions
        WHERE AccountingEventId IS NOT NULL
           OR SourceEntityType IS NOT NULL
           OR SourceEntityId IS NOT NULL
           OR SourceOperationId IS NOT NULL
           OR OperationalCostImpact IS NOT NULL
    )
        THROW 51706, N'Rollback is blocked because foundation inventory transaction links already exist.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM dbo.ProductionMaterialConsumptions
        WHERE UnitId IS NOT NULL
           OR SourceOperationId IS NOT NULL
           OR OperationalAmount IS NOT NULL
           OR PostingAmount IS NOT NULL
           OR AccountingEventId IS NOT NULL
           OR InventoryTransactionId IS NOT NULL
    )
        THROW 51707, N'Rollback is blocked because foundation consumption links already exist.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM dbo.FinancialTransactions
        WHERE TransactionType IN (N'FabricInventoryReceived', N'FabricInventoryConsumed', N'ConsumableInventoryReceived', N'ConsumableInventoryConsumed')
    )
       OR EXISTS
       (
           SELECT 1
           FROM dbo.JournalEntryLines jel
           INNER JOIN dbo.LedgerAccounts la ON la.LedgerAccountId = jel.LedgerAccountId
           WHERE la.AccountCode IN (N'1101', N'1102', N'5300')
       )
        THROW 51708, N'Rollback is blocked because foundation ledger accounts may have operational postings.', 1;

    IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.InventoryReceiptPostings') AND name = N'UX_InventoryReceiptPostings_SourceOperation')
        DROP INDEX UX_InventoryReceiptPostings_SourceOperation ON dbo.InventoryReceiptPostings;
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.InventoryReceiptPostings') AND name = N'UX_InventoryReceiptPostings_GoodsReceiptItemClass')
        DROP INDEX UX_InventoryReceiptPostings_GoodsReceiptItemClass ON dbo.InventoryReceiptPostings;
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.InventoryReceiptPostings') AND name = N'IX_InventoryReceiptPostings_AccountingBasis')
        DROP INDEX IX_InventoryReceiptPostings_AccountingBasis ON dbo.InventoryReceiptPostings;
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.InventoryReceiptLines') AND name = N'UX_InventoryReceiptLines_Transaction')
        DROP INDEX UX_InventoryReceiptLines_Transaction ON dbo.InventoryReceiptLines;
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.FabricConsumptionSources') AND name = N'UX_FabricConsumptionSources_AccountingEvent')
        DROP INDEX UX_FabricConsumptionSources_AccountingEvent ON dbo.FabricConsumptionSources;
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.FabricConsumptionSources') AND name = N'UX_FabricConsumptionSources_InventoryTransaction')
        DROP INDEX UX_FabricConsumptionSources_InventoryTransaction ON dbo.FabricConsumptionSources;
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ProductionMaterialConsumptions') AND name = N'UX_ProductionMaterialConsumptions_SourceOperation')
        DROP INDEX UX_ProductionMaterialConsumptions_SourceOperation ON dbo.ProductionMaterialConsumptions;
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ProductionMaterialConsumptions') AND name = N'UX_ProductionMaterialConsumptions_AccountingEvent')
        DROP INDEX UX_ProductionMaterialConsumptions_AccountingEvent ON dbo.ProductionMaterialConsumptions;
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ProductionMaterialConsumptions') AND name = N'UX_ProductionMaterialConsumptions_InventoryTransaction')
        DROP INDEX UX_ProductionMaterialConsumptions_InventoryTransaction ON dbo.ProductionMaterialConsumptions;
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.InventoryTransactions') AND name = N'UX_InventoryTransactions_SourceOperation')
        DROP INDEX UX_InventoryTransactions_SourceOperation ON dbo.InventoryTransactions;
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.InventoryTransactions') AND name = N'UX_InventoryTransactions_SourceEntity')
        DROP INDEX UX_InventoryTransactions_SourceEntity ON dbo.InventoryTransactions;
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.AccountingEvents') AND name = N'UX_AccountingEvents_InventoryReceiptPosting')
        DROP INDEX UX_AccountingEvents_InventoryReceiptPosting ON dbo.AccountingEvents;
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.AccountingEvents') AND name = N'UX_AccountingEvents_FabricConsumptionSource')
        DROP INDEX UX_AccountingEvents_FabricConsumptionSource ON dbo.AccountingEvents;
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.AccountingEvents') AND name = N'UX_AccountingEvents_ProductionMaterialConsumption')
        DROP INDEX UX_AccountingEvents_ProductionMaterialConsumption ON dbo.AccountingEvents;

    ALTER TABLE dbo.AccountingEvents DROP CONSTRAINT FK_AccountingEvents_InventoryReceiptPosting;
    ALTER TABLE dbo.AccountingEvents DROP CONSTRAINT FK_AccountingEvents_FabricConsumptionSource;
    ALTER TABLE dbo.AccountingEvents DROP CONSTRAINT FK_AccountingEvents_ProductionMaterialConsumption;
    ALTER TABLE dbo.InventoryTransactions DROP CONSTRAINT FK_InventoryTransactions_AccountingEvent;
    ALTER TABLE dbo.ProductionMaterialConsumptions DROP CONSTRAINT FK_ProductionMaterialConsumptions_Unit;
    ALTER TABLE dbo.ProductionMaterialConsumptions DROP CONSTRAINT FK_ProductionMaterialConsumptions_AccountingEvent;
    ALTER TABLE dbo.ProductionMaterialConsumptions DROP CONSTRAINT FK_ProductionMaterialConsumptions_InventoryTransaction;
    ALTER TABLE dbo.InventoryReceiptPostings DROP CONSTRAINT FK_InventoryReceiptPostings_AccountingEvent;
    ALTER TABLE dbo.InventoryReceiptLines DROP CONSTRAINT FK_InventoryReceiptLines_FabricRoll;
    ALTER TABLE dbo.InventoryReceiptLines DROP CONSTRAINT FK_InventoryReceiptLines_InventoryTransaction;
    ALTER TABLE dbo.InventoryReceiptLines DROP CONSTRAINT FK_InventoryReceiptLines_AccountingEvent;
    ALTER TABLE dbo.FabricConsumptionSources DROP CONSTRAINT FK_FabricConsumptionSources_InventoryTransaction;
    ALTER TABLE dbo.FabricConsumptionSources DROP CONSTRAINT FK_FabricConsumptionSources_AccountingEvent;

    ALTER TABLE dbo.AccountingEvents DROP CONSTRAINT CK_AccountingEvents_Type;
    ALTER TABLE dbo.AccountingEvents DROP CONSTRAINT CK_AccountingEvents_SourceCardinality;

    IF EXISTS (SELECT 1 FROM sys.extended_properties WHERE name = N'FabricConsumablesFoundationCutoverUtc' AND major_id = OBJECT_ID(N'dbo.InventoryItemFoundation'))
        EXEC sys.sp_dropextendedproperty
            @name = N'FabricConsumablesFoundationCutoverUtc',
            @level0type = N'SCHEMA', @level0name = N'dbo',
            @level1type = N'TABLE', @level1name = N'InventoryItemFoundation';

    DROP TABLE dbo.FabricConsumptionSources;
    DROP TABLE dbo.InventoryReceiptLines;
    DROP TABLE dbo.FabricRolls;
    DROP TABLE dbo.InventoryReceiptPostings;
    DROP TABLE dbo.InventoryItemFoundation;
    DROP TABLE dbo.InventoryUnitCatalog;
    DROP TABLE dbo.InventoryClassCatalog;

    ALTER TABLE dbo.AccountingEvents
        DROP COLUMN InventoryReceiptPostingId, FabricConsumptionSourceId, ProductionMaterialConsumptionId;

    ALTER TABLE dbo.InventoryTransactions
        DROP COLUMN AccountingEventId, SourceEntityType, SourceEntityId, SourceOperationId, OperationalCostImpact;

    ALTER TABLE dbo.ProductionMaterialConsumptions
        DROP COLUMN UnitId, SourceOperationId, OperationalAmount, PostingAmount, AccountingEventId, InventoryTransactionId;

    IF EXISTS (SELECT 1 FROM dbo.InventoryItems WHERE CurrentQuantity <> CONVERT(decimal(18,2), CurrentQuantity) OR AvailableQuantity <> CONVERT(decimal(18,2), AvailableQuantity) OR ReservedQuantity <> CONVERT(decimal(18,2), ReservedQuantity))
        THROW 51709, N'Rollback is blocked because inventory quantities require six-decimal precision.', 1;
    IF EXISTS (SELECT 1 FROM dbo.InventoryTransactions WHERE Quantity <> CONVERT(decimal(18,2), Quantity) OR (TotalCostImpact IS NOT NULL AND TotalCostImpact <> CONVERT(decimal(18,2), TotalCostImpact)) OR (UnitCost IS NOT NULL AND UnitCost <> CONVERT(decimal(18,2), UnitCost)))
        THROW 51710, N'Rollback is blocked because inventory transaction values require six-decimal precision.', 1;
    IF EXISTS (SELECT 1 FROM dbo.GoodsReceiptItems WHERE ReceivedQuantity <> CONVERT(decimal(18,2), ReceivedQuantity) OR UnitCost <> CONVERT(decimal(18,2), UnitCost) OR LineTotal <> CONVERT(decimal(18,2), LineTotal))
        THROW 51711, N'Rollback is blocked because goods receipt values require six-decimal precision.', 1;
    IF EXISTS (SELECT 1 FROM dbo.ProductionMaterialConsumptions WHERE ConsumedQuantity <> CONVERT(decimal(18,2), ConsumedQuantity) OR UnitCost <> CONVERT(decimal(18,2), UnitCost) OR TotalCost <> CONVERT(decimal(18,2), TotalCost))
        THROW 51712, N'Rollback is blocked because consumption values require six-decimal precision.', 1;

    ALTER TABLE dbo.InventoryItems ALTER COLUMN CurrentQuantity decimal(18,2) NOT NULL;
    ALTER TABLE dbo.InventoryItems ALTER COLUMN AvailableQuantity decimal(18,2) NOT NULL;
    ALTER TABLE dbo.InventoryItems ALTER COLUMN ReservedQuantity decimal(18,2) NOT NULL;
    ALTER TABLE dbo.InventoryTransactions ALTER COLUMN Quantity decimal(18,2) NOT NULL;
    ALTER TABLE dbo.InventoryTransactions ALTER COLUMN TotalCostImpact decimal(18,2) NULL;
    ALTER TABLE dbo.InventoryTransactions ALTER COLUMN UnitCost decimal(18,2) NULL;
    ALTER TABLE dbo.GoodsReceiptItems ALTER COLUMN ReceivedQuantity decimal(18,2) NOT NULL;
    ALTER TABLE dbo.GoodsReceiptItems ALTER COLUMN UnitCost decimal(18,2) NOT NULL;
    ALTER TABLE dbo.GoodsReceiptItems ALTER COLUMN LineTotal decimal(18,2) NOT NULL;
    ALTER TABLE dbo.ProductionMaterialConsumptions ALTER COLUMN ConsumedQuantity decimal(18,2) NOT NULL;
    ALTER TABLE dbo.ProductionMaterialConsumptions ALTER COLUMN UnitCost decimal(18,2) NOT NULL;
    ALTER TABLE dbo.ProductionMaterialConsumptions ALTER COLUMN TotalCost decimal(18,2) NOT NULL;

    EXEC(N'ALTER TABLE dbo.AccountingEvents ADD CONSTRAINT CK_AccountingEvents_Type CHECK ' + @oldAccountingEventTypeDefinition + N';');
    EXEC(N'ALTER TABLE dbo.AccountingEvents ADD CONSTRAINT CK_AccountingEvents_SourceCardinality CHECK ' + @oldSourceCardinalityDefinition + N';');

    DROP PROCEDURE dbo.usp_PostAccountingEvent;
    EXEC sys.sp_executesql @procedureDefinition;

    IF CONVERT(char(64), HASHBYTES('SHA2_256', CONVERT(varbinary(max), OBJECT_DEFINITION(OBJECT_ID(N'dbo.usp_PostAccountingEvent')))), 2) <> @procedureHashBefore
        THROW 51713, N'The restored accounting event writer hash does not match its captured original.', 1;

    DELETE FROM dbo.LedgerAccounts
    WHERE AccountCode IN (N'1101', N'1102', N'5300')
      AND NOT EXISTS (SELECT 1 FROM dbo.FinancialTransactions WHERE TransactionType IN (N'FabricInventoryReceived', N'FabricInventoryConsumed', N'ConsumableInventoryReceived', N'ConsumableInventoryConsumed'));

    DROP TABLE dbo.InventoryFoundationProcedureBackups;

    COMMIT TRANSACTION;
    SELECT N'PRODUCTION_FOUNDATION_GAP_ROLLBACK_APPLIED' AS Result;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
