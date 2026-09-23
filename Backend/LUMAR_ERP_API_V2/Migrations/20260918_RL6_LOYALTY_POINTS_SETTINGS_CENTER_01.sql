SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- Required source objects must exist before any loyalty schema change is allowed.
IF OBJECT_ID(N'dbo.LoyaltyPiecePointSettings', N'U') IS NULL
    THROW 51000, 'RL6 migration blocked: dbo.LoyaltyPiecePointSettings does not exist.', 1;
IF OBJECT_ID(N'dbo.PricingProductTypes', N'U') IS NULL
    THROW 51000, 'RL6 migration blocked: dbo.PricingProductTypes does not exist.', 1;
IF OBJECT_ID(N'dbo.ImportedReadyMadeProducts', N'U') IS NULL
    THROW 51000, 'RL6 migration blocked: dbo.ImportedReadyMadeProducts does not exist.', 1;

IF COL_LENGTH(N'dbo.PricingProductTypes', N'ProductTypeId') IS NULL
    THROW 51000, 'RL6 migration blocked: PricingProductTypes.ProductTypeId does not exist.', 1;
IF COL_LENGTH(N'dbo.ImportedReadyMadeProducts', N'ImportedReadyMadeProductId') IS NULL
    THROW 51000, 'RL6 migration blocked: ImportedReadyMadeProducts.ImportedReadyMadeProductId does not exist.', 1;

-- Zero-existence gate: the new source key must not already be present in the legacy settings table.
DECLARE @ProductTypeColumnCount int =
(
    SELECT COUNT(*)
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.LoyaltyPiecePointSettings')
      AND name = N'ProductTypeId'
);
IF @ProductTypeColumnCount <> 0
    THROW 51000, 'RL6 migration blocked: ProductTypeId already exists in LoyaltyPiecePointSettings; expected 0.', 1;

ALTER TABLE dbo.LoyaltyPiecePointSettings
    ADD ProductTypeId int NULL;

DECLARE @ProductTypeIndexCount int =
(
    SELECT COUNT(*)
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.LoyaltyPiecePointSettings')
      AND name = N'UX_LoyaltyPiecePointSettings_ProductTypeId'
);
IF @ProductTypeIndexCount <> 0
    THROW 51000, 'RL6 migration blocked: UX_LoyaltyPiecePointSettings_ProductTypeId already exists; expected 0.', 1;

EXEC sys.sp_executesql N'
CREATE UNIQUE INDEX UX_LoyaltyPiecePointSettings_ProductTypeId
    ON dbo.LoyaltyPiecePointSettings(ProductTypeId)
    WHERE ProductTypeId IS NOT NULL;';

DECLARE @ProductTypeForeignKeyCount int =
(
    SELECT COUNT(*)
    FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID(N'dbo.LoyaltyPiecePointSettings')
      AND name = N'FK_LoyaltyPiecePointSettings_PricingProductTypes_ProductTypeId'
);
IF @ProductTypeForeignKeyCount <> 0
    THROW 51000, 'RL6 migration blocked: ProductTypeId foreign key already exists; expected 0.', 1;

EXEC sys.sp_executesql N'
ALTER TABLE dbo.LoyaltyPiecePointSettings
    ADD CONSTRAINT FK_LoyaltyPiecePointSettings_PricingProductTypes_ProductTypeId
    FOREIGN KEY (ProductTypeId)
    REFERENCES dbo.PricingProductTypes(ProductTypeId);';

-- Zero-existence gate for the imported-product point-settings table.
DECLARE @ImportedSettingsTableCount int =
(
    SELECT COUNT(*)
    FROM sys.tables
    WHERE object_id = OBJECT_ID(N'dbo.LoyaltyImportedReadyMadeProductPointSettings')
);
IF @ImportedSettingsTableCount <> 0
    THROW 51000, 'RL6 migration blocked: imported loyalty point-settings table already exists; expected 0.', 1;

CREATE TABLE dbo.LoyaltyImportedReadyMadeProductPointSettings
(
    ImportedReadyMadeProductId int NOT NULL,
    Points decimal(18,2) NOT NULL,
    IsActive bit NOT NULL CONSTRAINT DF_LoyaltyImportedReadyMadeProductPointSettings_IsActive DEFAULT (1),
    CreatedAtUtc datetime2 NOT NULL CONSTRAINT DF_LoyaltyImportedReadyMadeProductPointSettings_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc datetime2 NOT NULL CONSTRAINT DF_LoyaltyImportedReadyMadeProductPointSettings_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_LoyaltyImportedReadyMadeProductPointSettings PRIMARY KEY (ImportedReadyMadeProductId),
    CONSTRAINT FK_LoyaltyImportedReadyMadeProductPointSettings_ImportedReadyMadeProducts FOREIGN KEY (ImportedReadyMadeProductId)
        REFERENCES dbo.ImportedReadyMadeProducts(ImportedReadyMadeProductId),
    CONSTRAINT CK_LoyaltyImportedReadyMadeProductPointSettings_Points CHECK (Points >= 0)
);

CREATE INDEX IX_LoyaltyImportedReadyMadeProductPointSettings_IsActive
    ON dbo.LoyaltyImportedReadyMadeProductPointSettings(IsActive, ImportedReadyMadeProductId);

COMMIT TRANSACTION;
