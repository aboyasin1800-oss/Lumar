SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.PricingProductTypes', N'U') IS NULL
    THROW 51000, 'RL6 migration blocked: dbo.PricingProductTypes does not exist.', 1;
IF COL_LENGTH(N'dbo.PricingProductTypes', N'ProductTypeId') IS NULL
    THROW 51000, 'RL6 migration blocked: PricingProductTypes.ProductTypeId does not exist.', 1;

IF OBJECT_ID(N'dbo.LoyaltyReadyMadeProductTypePointSettings', N'U') IS NOT NULL
    THROW 51000, 'RL6 migration blocked: LoyaltyReadyMadeProductTypePointSettings already exists; expected 0.', 1;

CREATE TABLE dbo.LoyaltyReadyMadeProductTypePointSettings
(
    ProductTypeId int NOT NULL,
    Points decimal(18,2) NOT NULL,
    IsActive bit NOT NULL CONSTRAINT DF_LoyaltyReadyMadeProductTypePointSettings_IsActive DEFAULT (1),
    CreatedAtUtc datetime2 NOT NULL CONSTRAINT DF_LoyaltyReadyMadeProductTypePointSettings_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc datetime2 NOT NULL CONSTRAINT DF_LoyaltyReadyMadeProductTypePointSettings_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_LoyaltyReadyMadeProductTypePointSettings PRIMARY KEY (ProductTypeId),
    CONSTRAINT FK_LoyaltyReadyMadeProductTypePointSettings_PricingProductTypes_ProductTypeId FOREIGN KEY (ProductTypeId)
        REFERENCES dbo.PricingProductTypes(ProductTypeId),
    CONSTRAINT CK_LoyaltyReadyMadeProductTypePointSettings_Points CHECK (Points >= 0)
);

CREATE INDEX IX_LoyaltyReadyMadeProductTypePointSettings_IsActive
    ON dbo.LoyaltyReadyMadeProductTypePointSettings(IsActive, ProductTypeId);

COMMIT TRANSACTION;