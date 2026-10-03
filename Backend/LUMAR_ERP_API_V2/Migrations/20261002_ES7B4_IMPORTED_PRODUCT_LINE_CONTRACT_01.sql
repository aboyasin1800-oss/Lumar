SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.SupplierInvoiceLines', N'ProductType') IS NULL
    ALTER TABLE dbo.SupplierInvoiceLines ADD ProductType nvarchar(50) NULL;

IF COL_LENGTH(N'dbo.SupplierInvoiceLines', N'UnitCode') IS NULL
    ALTER TABLE dbo.SupplierInvoiceLines ADD UnitCode nvarchar(30) NULL;

IF COL_LENGTH(N'dbo.SupplierInvoiceLines', N'ItemCount') IS NULL
    ALTER TABLE dbo.SupplierInvoiceLines ADD ItemCount decimal(18,6) NULL;

IF COL_LENGTH(N'dbo.GoodsReceiptItems', N'ProductType') IS NULL
    ALTER TABLE dbo.GoodsReceiptItems ADD ProductType nvarchar(50) NULL;

IF COL_LENGTH(N'dbo.GoodsReceiptItems', N'UnitCode') IS NULL
    ALTER TABLE dbo.GoodsReceiptItems ADD UnitCode nvarchar(30) NULL;

IF COL_LENGTH(N'dbo.GoodsReceiptItems', N'ItemCount') IS NULL
    ALTER TABLE dbo.GoodsReceiptItems ADD ItemCount decimal(18,6) NULL;

IF COL_LENGTH(N'dbo.GoodsReceiptItems', N'ReceivedItemCount') IS NULL
    ALTER TABLE dbo.GoodsReceiptItems ADD ReceivedItemCount decimal(18,6) NULL;

IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.SupplierInvoiceLines') AND name = N'IX_SupplierInvoiceLines_ProductType')
    EXEC(N'DROP INDEX IX_SupplierInvoiceLines_ProductType ON dbo.SupplierInvoiceLines;');

IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.GoodsReceiptItems') AND name = N'IX_GoodsReceiptItems_ProductType')
    EXEC(N'DROP INDEX IX_GoodsReceiptItems_ProductType ON dbo.GoodsReceiptItems;');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.SupplierInvoiceLines') AND name = N'IX_SupplierInvoiceLines_ProductType')
    EXEC(N'CREATE INDEX IX_SupplierInvoiceLines_ProductType ON dbo.SupplierInvoiceLines(ProductType) WHERE ProductType IS NOT NULL;');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.GoodsReceiptItems') AND name = N'IX_GoodsReceiptItems_ProductType')
    EXEC(N'CREATE INDEX IX_GoodsReceiptItems_ProductType ON dbo.GoodsReceiptItems(ProductType) WHERE ProductType IS NOT NULL;');

COMMIT TRANSACTION;
