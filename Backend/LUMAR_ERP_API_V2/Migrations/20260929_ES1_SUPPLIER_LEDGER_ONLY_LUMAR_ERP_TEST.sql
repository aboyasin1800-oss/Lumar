SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'LUMAR_ERP_TEST'
    THROW 52000, N'This supplier-only script is restricted to LUMAR_ERP_TEST.', 1;

IF OBJECT_ID(N'dbo.SupplierLedgerEntries', N'U') IS NULL
    THROW 52001, N'dbo.SupplierLedgerEntries is required for the supplier-ledger-only update.', 1;

IF OBJECT_ID(N'dbo.EmployeeLedgerEntries', N'U') IS NOT NULL
    THROW 52002, N'Employee ledger objects are not part of this supplier-only script; aborting to preserve employee data contracts.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    -- Supplier-only column additions: legacy rows remain untouched; new rows use the official contract.
    IF COL_LENGTH(N'dbo.SupplierLedgerEntries', N'EntryType') IS NULL
        ALTER TABLE dbo.SupplierLedgerEntries ADD EntryType nvarchar(50) NULL;
    IF COL_LENGTH(N'dbo.SupplierLedgerEntries', N'Amount') IS NULL
        ALTER TABLE dbo.SupplierLedgerEntries ADD Amount decimal(18,2) NULL;
    IF COL_LENGTH(N'dbo.SupplierLedgerEntries', N'BalanceEffect') IS NULL
        ALTER TABLE dbo.SupplierLedgerEntries ADD BalanceEffect decimal(18,2) NULL;
    IF COL_LENGTH(N'dbo.SupplierLedgerEntries', N'OccurredAt') IS NULL
        ALTER TABLE dbo.SupplierLedgerEntries ADD OccurredAt datetime2(7) NULL;
    IF COL_LENGTH(N'dbo.SupplierLedgerEntries', N'SourceType') IS NULL
        ALTER TABLE dbo.SupplierLedgerEntries ADD SourceType nvarchar(50) NULL;
    IF COL_LENGTH(N'dbo.SupplierLedgerEntries', N'SourceId') IS NULL
        ALTER TABLE dbo.SupplierLedgerEntries ADD SourceId bigint NULL;
    IF COL_LENGTH(N'dbo.SupplierLedgerEntries', N'SourceOperationId') IS NULL
        ALTER TABLE dbo.SupplierLedgerEntries ADD SourceOperationId uniqueidentifier NULL;
    IF COL_LENGTH(N'dbo.SupplierLedgerEntries', N'OriginalEntryId') IS NULL
        ALTER TABLE dbo.SupplierLedgerEntries ADD OriginalEntryId bigint NULL;
    IF COL_LENGTH(N'dbo.SupplierLedgerEntries', N'ReversalReason') IS NULL
        ALTER TABLE dbo.SupplierLedgerEntries ADD ReversalReason nvarchar(500) NULL;
    IF COL_LENGTH(N'dbo.SupplierLedgerEntries', N'ReversedBy') IS NULL
        ALTER TABLE dbo.SupplierLedgerEntries ADD ReversedBy nvarchar(100) NULL;
    IF COL_LENGTH(N'dbo.SupplierLedgerEntries', N'ReversedAt') IS NULL
        ALTER TABLE dbo.SupplierLedgerEntries ADD ReversedAt datetime2(7) NULL;
    IF COL_LENGTH(N'dbo.SupplierLedgerEntries', N'CreatedBy') IS NULL
        ALTER TABLE dbo.SupplierLedgerEntries ADD CreatedBy nvarchar(100) NULL;
    IF COL_LENGTH(N'dbo.SupplierLedgerEntries', N'Status') IS NULL
        ALTER TABLE dbo.SupplierLedgerEntries ADD Status nvarchar(20) NOT NULL CONSTRAINT DF_SupplierLedgerEntries_Status_LUMAR_ERP_TEST DEFAULT N'Legacy';

    IF NOT EXISTS (
        SELECT 1
        FROM sys.foreign_keys
        WHERE parent_object_id = OBJECT_ID(N'dbo.SupplierLedgerEntries')
          AND name = N'FK_SupplierLedgerEntries_OriginalEntry_OriginalEntryId'
    )
    ALTER TABLE dbo.SupplierLedgerEntries WITH CHECK
        ADD CONSTRAINT FK_SupplierLedgerEntries_OriginalEntry_OriginalEntryId
            FOREIGN KEY (OriginalEntryId) REFERENCES dbo.SupplierLedgerEntries(SupplierLedgerEntryId);

    IF NOT EXISTS (
        SELECT 1
        FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.SupplierLedgerEntries')
          AND name = N'CK_SupplierLedgerEntries_FoundationStatus'
    )
    EXEC(N'
        ALTER TABLE dbo.SupplierLedgerEntries WITH CHECK
            ADD CONSTRAINT CK_SupplierLedgerEntries_FoundationStatus
                CHECK (Status IN (N''Legacy'', N''Posted'', N''Reversed''));
    ');

    IF NOT EXISTS (
        SELECT 1
        FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.SupplierLedgerEntries')
          AND name = N'CK_SupplierLedgerEntries_FoundationFields'
    )
    EXEC(N'
        ALTER TABLE dbo.SupplierLedgerEntries WITH CHECK
            ADD CONSTRAINT CK_SupplierLedgerEntries_FoundationFields
                CHECK
                (
                    Status = N''Legacy''
                    OR
                    (
                        EntryType IS NOT NULL
                        AND Amount > 0
                        AND BalanceEffect <> 0
                        AND OccurredAt IS NOT NULL
                        AND SourceType IS NOT NULL
                        AND SourceId IS NOT NULL
                        AND SourceOperationId IS NOT NULL
                        AND CreatedBy IS NOT NULL
                        AND
                        (
                            (EntryType = N''Reversal''
                             AND OriginalEntryId IS NOT NULL
                             AND ReversalReason IS NOT NULL
                             AND ReversedBy IS NOT NULL
                             AND ReversedAt IS NOT NULL)
                            OR
                            (EntryType <> N''Reversal''
                             AND OriginalEntryId IS NULL
                             AND ReversalReason IS NULL
                             AND ReversedBy IS NULL
                             AND ReversedAt IS NULL)
                        )
                    )
                );
    ');

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.SupplierLedgerEntries')
          AND name = N'UX_SupplierLedgerEntries_SourceOperation_EntryType'
    )
    EXEC(N'CREATE UNIQUE INDEX UX_SupplierLedgerEntries_SourceOperation_EntryType ON dbo.SupplierLedgerEntries(SourceOperationId, EntryType) WHERE SourceOperationId IS NOT NULL;');

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.SupplierLedgerEntries')
          AND name = N'UX_SupplierLedgerEntries_OriginalEntry_Reversal'
    )
    EXEC(N'CREATE UNIQUE INDEX UX_SupplierLedgerEntries_OriginalEntry_Reversal ON dbo.SupplierLedgerEntries(OriginalEntryId) WHERE EntryType = N''Reversal'';');

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.SupplierLedgerEntries')
          AND name = N'IX_SupplierLedgerEntries_Supplier_OccurredAt'
    )
    EXEC(N'CREATE INDEX IX_SupplierLedgerEntries_Supplier_OccurredAt ON dbo.SupplierLedgerEntries(SupplierId, OccurredAt, SupplierLedgerEntryId) WHERE OccurredAt IS NOT NULL;');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
