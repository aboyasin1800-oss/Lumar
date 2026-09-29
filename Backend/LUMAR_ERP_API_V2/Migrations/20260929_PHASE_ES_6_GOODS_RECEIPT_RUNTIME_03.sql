SET XACT_ABORT ON;
IF DB_NAME() NOT IN(N'LUMAR_ERP_TEST',N'LUMAR_ERP_ES_VALIDATION') THROW 52420,N'ES-6 goods receipt runtime is restricted to approved ES validation databases.',1;
IF OBJECT_ID(N'dbo.Warehouses',N'U') IS NULL OR OBJECT_ID(N'dbo.SupplierInvoiceLines',N'U') IS NULL OR COL_LENGTH(N'dbo.GoodsReceiptItems',N'InventoryItemId') IS NULL THROW 52421,N'ES-6 identity foundations are required.',1;
BEGIN TRY BEGIN TRANSACTION;
IF COL_LENGTH(N'dbo.GoodsReceipts',N'SourceOperationId') IS NULL ALTER TABLE dbo.GoodsReceipts ADD SourceOperationId uniqueidentifier NULL;
IF COL_LENGTH(N'dbo.GoodsReceipts',N'ReceiptStatus') IS NULL ALTER TABLE dbo.GoodsReceipts ADD ReceiptStatus nvarchar(20) NOT NULL CONSTRAINT DF_GoodsReceipts_ES6_Status DEFAULT N'Posted';
IF COL_LENGTH(N'dbo.GoodsReceipts',N'OriginalGoodsReceiptId') IS NULL ALTER TABLE dbo.GoodsReceipts ADD OriginalGoodsReceiptId int NULL;
IF COL_LENGTH(N'dbo.GoodsReceiptItems',N'SupplierInvoiceLineId') IS NULL ALTER TABLE dbo.GoodsReceiptItems ADD SupplierInvoiceLineId bigint NULL;
IF EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.GoodsReceipts') AND name=N'PurchaseOrderId' AND is_nullable=0) ALTER TABLE dbo.GoodsReceipts ALTER COLUMN PurchaseOrderId int NULL;
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_GoodsReceipts_ES6_Original') EXEC(N'ALTER TABLE dbo.GoodsReceipts ADD CONSTRAINT FK_GoodsReceipts_ES6_Original FOREIGN KEY(OriginalGoodsReceiptId) REFERENCES dbo.GoodsReceipts(GoodsReceiptId);');
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_GoodsReceiptItems_ES6_InvoiceLine') EXEC(N'ALTER TABLE dbo.GoodsReceiptItems ADD CONSTRAINT FK_GoodsReceiptItems_ES6_InvoiceLine FOREIGN KEY(SupplierInvoiceLineId) REFERENCES dbo.SupplierInvoiceLines(SupplierInvoiceLineId);');
IF NOT EXISTS(SELECT 1 FROM sys.check_constraints WHERE name=N'CK_GoodsReceipts_ES6_Status') EXEC(N'ALTER TABLE dbo.GoodsReceipts ADD CONSTRAINT CK_GoodsReceipts_ES6_Status CHECK(ReceiptStatus IN(N''Posted'',N''Reversed''));');
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.GoodsReceipts') AND name=N'UX_GoodsReceipts_ES6_SourceOperation') EXEC(N'CREATE UNIQUE INDEX UX_GoodsReceipts_ES6_SourceOperation ON dbo.GoodsReceipts(SourceOperationId) WHERE SourceOperationId IS NOT NULL;');
CREATE TABLE dbo.GoodsReceiptDifferences(
 GoodsReceiptDifferenceId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_GoodsReceiptDifferences PRIMARY KEY,GoodsReceiptItemId int NOT NULL,DifferenceType nvarchar(40) NOT NULL,ExpectedQuantity decimal(18,6) NULL,ActualQuantity decimal(18,6) NULL,ExpectedUnitCost decimal(18,6) NULL,ActualUnitCost decimal(18,6) NULL,CreatedAt datetime2(7) NOT NULL CONSTRAINT DF_GoodsReceiptDifferences_CreatedAt DEFAULT SYSUTCDATETIME(),
 CONSTRAINT FK_GoodsReceiptDifferences_Item FOREIGN KEY(GoodsReceiptItemId) REFERENCES dbo.GoodsReceiptItems(GoodsReceiptItemId));
CREATE TABLE dbo.GoodsReceiptReversals(
 GoodsReceiptReversalId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_GoodsReceiptReversals PRIMARY KEY,OriginalGoodsReceiptId int NOT NULL,SourceOperationId uniqueidentifier NOT NULL,Reason nvarchar(1000) NOT NULL,ReversedBy nvarchar(100) NOT NULL,CreatedAt datetime2(7) NOT NULL CONSTRAINT DF_GoodsReceiptReversals_CreatedAt DEFAULT SYSUTCDATETIME(),
 CONSTRAINT FK_GoodsReceiptReversals_Original FOREIGN KEY(OriginalGoodsReceiptId) REFERENCES dbo.GoodsReceipts(GoodsReceiptId),CONSTRAINT UQ_GoodsReceiptReversals_Original UNIQUE(OriginalGoodsReceiptId),CONSTRAINT UQ_GoodsReceiptReversals_Operation UNIQUE(SourceOperationId));
CREATE TABLE dbo.GoodsReceiptReversalLines(
 GoodsReceiptReversalLineId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_GoodsReceiptReversalLines PRIMARY KEY,GoodsReceiptReversalId bigint NOT NULL,GoodsReceiptItemId int NOT NULL,InventoryTransactionId int NOT NULL,AccountingEventId bigint NOT NULL,
 CONSTRAINT FK_GoodsReceiptReversalLines_Reversal FOREIGN KEY(GoodsReceiptReversalId) REFERENCES dbo.GoodsReceiptReversals(GoodsReceiptReversalId),CONSTRAINT FK_GoodsReceiptReversalLines_Item FOREIGN KEY(GoodsReceiptItemId) REFERENCES dbo.GoodsReceiptItems(GoodsReceiptItemId),CONSTRAINT UQ_GoodsReceiptReversalLines_Item UNIQUE(GoodsReceiptItemId));
COMMIT TRANSACTION; END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;