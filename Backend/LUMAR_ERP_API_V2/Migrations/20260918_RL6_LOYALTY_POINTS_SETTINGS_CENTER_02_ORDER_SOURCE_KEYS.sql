SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.OrderItems', N'U') IS NULL
    THROW 51000, 'RL6 migration blocked: dbo.OrderItems does not exist.', 1;
IF OBJECT_ID(N'dbo.PricingProductTypes', N'U') IS NULL
    THROW 51000, 'RL6 migration blocked: dbo.PricingProductTypes does not exist.', 1;
IF OBJECT_ID(N'dbo.ImportedReadyMadeProducts', N'U') IS NULL
    THROW 51000, 'RL6 migration blocked: dbo.ImportedReadyMadeProducts does not exist.', 1;

DECLARE @ProductTypeColumnCount int =
(
    SELECT COUNT(*) FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.OrderItems') AND name = N'ProductTypeId'
);
IF @ProductTypeColumnCount <> 0
    THROW 51000, 'RL6 migration blocked: OrderItems.ProductTypeId already exists; expected 0.', 1;

DECLARE @ImportedProductColumnCount int =
(
    SELECT COUNT(*) FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.OrderItems') AND name = N'ImportedReadyMadeProductId'
);
IF @ImportedProductColumnCount <> 0
    THROW 51000, 'RL6 migration blocked: OrderItems.ImportedReadyMadeProductId already exists; expected 0.', 1;

ALTER TABLE dbo.OrderItems ADD ProductTypeId int NULL, ImportedReadyMadeProductId int NULL;

DECLARE @ProductTypeIndexCount int =
(
    SELECT COUNT(*) FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.OrderItems') AND name = N'IX_OrderItems_ProductTypeId'
);
IF @ProductTypeIndexCount <> 0
    THROW 51000, 'RL6 migration blocked: IX_OrderItems_ProductTypeId already exists; expected 0.', 1;

DECLARE @ImportedProductIndexCount int =
(
    SELECT COUNT(*) FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.OrderItems') AND name = N'IX_OrderItems_ImportedReadyMadeProductId'
);
IF @ImportedProductIndexCount <> 0
    THROW 51000, 'RL6 migration blocked: IX_OrderItems_ImportedReadyMadeProductId already exists; expected 0.', 1;

EXEC sys.sp_executesql N'
CREATE INDEX IX_OrderItems_ProductTypeId ON dbo.OrderItems(ProductTypeId) WHERE ProductTypeId IS NOT NULL;
CREATE INDEX IX_OrderItems_ImportedReadyMadeProductId ON dbo.OrderItems(ImportedReadyMadeProductId) WHERE ImportedReadyMadeProductId IS NOT NULL;
ALTER TABLE dbo.OrderItems ADD CONSTRAINT FK_OrderItems_PricingProductTypes_ProductTypeId FOREIGN KEY (ProductTypeId) REFERENCES dbo.PricingProductTypes(ProductTypeId);
ALTER TABLE dbo.OrderItems ADD CONSTRAINT FK_OrderItems_ImportedReadyMadeProducts_ImportedReadyMadeProductId FOREIGN KEY (ImportedReadyMadeProductId) REFERENCES dbo.ImportedReadyMadeProducts(ImportedReadyMadeProductId);';

COMMIT TRANSACTION;
