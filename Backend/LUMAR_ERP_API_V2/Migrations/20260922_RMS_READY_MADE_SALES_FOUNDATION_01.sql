SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.ReadyMadeInventoryProducts', N'U') IS NULL
    THROW 51000, 'RMS migration blocked: dbo.ReadyMadeInventoryProducts does not exist.', 1;
IF OBJECT_ID(N'dbo.PricingProductTypes', N'U') IS NULL
    THROW 51000, 'RMS migration blocked: dbo.PricingProductTypes does not exist.', 1;
IF OBJECT_ID(N'dbo.Orders', N'U') IS NULL OR OBJECT_ID(N'dbo.OrderItems', N'U') IS NULL
    THROW 51000, 'RMS migration blocked: order tables do not exist.', 1;
IF OBJECT_ID(N'dbo.Invoice_Header', N'U') IS NULL OR OBJECT_ID(N'dbo.Invoice_Details', N'U') IS NULL
    THROW 51000, 'RMS migration blocked: invoice tables do not exist.', 1;

IF COL_LENGTH(N'dbo.ReadyMadeInventoryProducts', N'ProductTypeId') IS NULL
    EXEC(N'ALTER TABLE dbo.ReadyMadeInventoryProducts ADD ProductTypeId int NULL');

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ReadyMadeInventoryProducts_PricingProductTypes_ProductTypeId')
    EXEC(N'ALTER TABLE dbo.ReadyMadeInventoryProducts ADD CONSTRAINT FK_ReadyMadeInventoryProducts_PricingProductTypes_ProductTypeId FOREIGN KEY (ProductTypeId) REFERENCES dbo.PricingProductTypes(ProductTypeId)');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ReadyMadeInventoryProducts_ProductTypeId')
    EXEC(N'CREATE INDEX IX_ReadyMadeInventoryProducts_ProductTypeId ON dbo.ReadyMadeInventoryProducts(ProductTypeId) WHERE ProductTypeId IS NOT NULL');

IF COL_LENGTH(N'dbo.ReadyMadeProductionOrderItems', N'ProductTypeId') IS NULL
    EXEC(N'ALTER TABLE dbo.ReadyMadeProductionOrderItems ADD ProductTypeId int NULL');

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ReadyMadeProductionOrderItems_PricingProductTypes_ProductTypeId')
    EXEC(N'ALTER TABLE dbo.ReadyMadeProductionOrderItems ADD CONSTRAINT FK_ReadyMadeProductionOrderItems_PricingProductTypes_ProductTypeId FOREIGN KEY (ProductTypeId) REFERENCES dbo.PricingProductTypes(ProductTypeId)');

IF COL_LENGTH(N'dbo.OrderItems', N'ReadyMadeInventoryProductId') IS NULL
    EXEC(N'ALTER TABLE dbo.OrderItems ADD ReadyMadeInventoryProductId int NULL');

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_OrderItems_ReadyMadeInventoryProducts_ReadyMadeInventoryProductId')
    EXEC(N'ALTER TABLE dbo.OrderItems ADD CONSTRAINT FK_OrderItems_ReadyMadeInventoryProducts_ReadyMadeInventoryProductId FOREIGN KEY (ReadyMadeInventoryProductId) REFERENCES dbo.ReadyMadeInventoryProducts(ReadyMadeInventoryProductId)');

IF COL_LENGTH(N'dbo.Orders', N'SaleReference') IS NULL
    EXEC(N'ALTER TABLE dbo.Orders ADD SaleReference nvarchar(100) NULL');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Orders_SaleReference')
    EXEC(N'CREATE UNIQUE INDEX UX_Orders_SaleReference ON dbo.Orders(SaleReference) WHERE SaleReference IS NOT NULL');

IF COL_LENGTH(N'dbo.Invoice_Header', N'InvoiceNumber') IS NULL
    EXEC(N'ALTER TABLE dbo.Invoice_Header ADD InvoiceNumber nvarchar(50) NULL');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Invoice_Header_InvoiceNumber')
    EXEC(N'CREATE UNIQUE INDEX UX_Invoice_Header_InvoiceNumber ON dbo.Invoice_Header(InvoiceNumber) WHERE InvoiceNumber IS NOT NULL');

IF COL_LENGTH(N'dbo.Invoice_Details', N'ReadyMadeInventoryProductId') IS NULL
    EXEC(N'ALTER TABLE dbo.Invoice_Details ADD ReadyMadeInventoryProductId int NULL');
IF COL_LENGTH(N'dbo.Invoice_Details', N'ImportedReadyMadeProductId') IS NULL
    EXEC(N'ALTER TABLE dbo.Invoice_Details ADD ImportedReadyMadeProductId int NULL');

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Invoice_Details_ReadyMadeInventoryProducts_ReadyMadeInventoryProductId')
    EXEC(N'ALTER TABLE dbo.Invoice_Details ADD CONSTRAINT FK_Invoice_Details_ReadyMadeInventoryProducts_ReadyMadeInventoryProductId FOREIGN KEY (ReadyMadeInventoryProductId) REFERENCES dbo.ReadyMadeInventoryProducts(ReadyMadeInventoryProductId)');

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Invoice_Details_ImportedReadyMadeProducts_ImportedReadyMadeProductId')
    EXEC(N'ALTER TABLE dbo.Invoice_Details ADD CONSTRAINT FK_Invoice_Details_ImportedReadyMadeProducts_ImportedReadyMadeProductId FOREIGN KEY (ImportedReadyMadeProductId) REFERENCES dbo.ImportedReadyMadeProducts(ImportedReadyMadeProductId)');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Invoice_Details_ReadyMadeInventoryProductId')
    EXEC(N'CREATE INDEX IX_Invoice_Details_ReadyMadeInventoryProductId ON dbo.Invoice_Details(ReadyMadeInventoryProductId) WHERE ReadyMadeInventoryProductId IS NOT NULL');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Invoice_Details_ImportedReadyMadeProductId')
    EXEC(N'CREATE INDEX IX_Invoice_Details_ImportedReadyMadeProductId ON dbo.Invoice_Details(ImportedReadyMadeProductId) WHERE ImportedReadyMadeProductId IS NOT NULL');

COMMIT TRANSACTION;