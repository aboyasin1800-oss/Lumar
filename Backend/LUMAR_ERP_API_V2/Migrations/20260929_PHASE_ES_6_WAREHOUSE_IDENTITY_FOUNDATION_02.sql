SET XACT_ABORT ON;
IF DB_NAME() NOT IN(N'LUMAR_ERP_TEST',N'LUMAR_ERP_ES_VALIDATION') THROW 52410,N'ES-6 warehouse foundation is restricted to approved ES validation databases.',1;
BEGIN TRY BEGIN TRANSACTION;
IF OBJECT_ID(N'dbo.Warehouses',N'U') IS NOT NULL OR COL_LENGTH(N'dbo.GoodsReceipts',N'WarehouseId') IS NOT NULL OR COL_LENGTH(N'dbo.InventoryTransactions',N'WarehouseId') IS NOT NULL THROW 52411,N'Warehouse identity already exists.',1;
CREATE TABLE dbo.Warehouses(
 WarehouseId int IDENTITY(1,1) NOT NULL PRIMARY KEY,WarehouseCode nvarchar(50) NOT NULL,WarehouseName nvarchar(200) NOT NULL,IsActive bit NOT NULL CONSTRAINT DF_Warehouses_IsActive DEFAULT 1,CreatedAt datetime2(7) NOT NULL CONSTRAINT DF_Warehouses_CreatedAt DEFAULT SYSUTCDATETIME(),
 CONSTRAINT UQ_Warehouses_Code UNIQUE(WarehouseCode));
ALTER TABLE dbo.GoodsReceipts ADD WarehouseId int NULL;
ALTER TABLE dbo.InventoryTransactions ADD WarehouseId int NULL;
ALTER TABLE dbo.GoodsReceipts ADD CONSTRAINT FK_GoodsReceipts_Warehouse FOREIGN KEY(WarehouseId) REFERENCES dbo.Warehouses(WarehouseId);
ALTER TABLE dbo.InventoryTransactions ADD CONSTRAINT FK_InventoryTransactions_Warehouse FOREIGN KEY(WarehouseId) REFERENCES dbo.Warehouses(WarehouseId);
EXEC(N'CREATE INDEX IX_GoodsReceipts_Warehouse ON dbo.GoodsReceipts(WarehouseId,GoodsReceiptId) WHERE WarehouseId IS NOT NULL;');
EXEC(N'CREATE INDEX IX_InventoryTransactions_Warehouse ON dbo.InventoryTransactions(WarehouseId,TransactionID) WHERE WarehouseId IS NOT NULL;');
COMMIT TRANSACTION; END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;