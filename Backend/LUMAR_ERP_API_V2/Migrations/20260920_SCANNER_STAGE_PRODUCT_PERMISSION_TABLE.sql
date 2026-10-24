-- Durable scanner authorization model for production stages.
-- This table is the persisted source of truth for scanner -> product type -> stage permissions.
-- It is intentionally separate from the employee piece-rate authorization and is enforced at scan time.

IF DB_NAME() <> N'LUMAR_ERP_ES_VALIDATION'
    THROW 52010, N'This scanner permission migration is restricted to LUMAR_ERP_ES_VALIDATION.', 1;

IF OBJECT_ID(N'dbo.ScannerStageProductPermissions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ScannerStageProductPermissions
    (
        ScannerStageProductPermissionId INT IDENTITY(1,1) NOT NULL,
        ScannerId INT NOT NULL,
        ProductTypeId INT NOT NULL,
        Stage NVARCHAR(100) NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_ScannerStageProductPermissions_IsActive DEFAULT (1),
        CreatedAt DATETIME2(3) NOT NULL CONSTRAINT DF_ScannerStageProductPermissions_CreatedAt DEFAULT (SYSDATETIME()),
        UpdatedAt DATETIME2(3) NULL,

        CONSTRAINT PK_ScannerStageProductPermissions PRIMARY KEY (ScannerStageProductPermissionId),
        CONSTRAINT FK_ScannerStageProductPermissions_ScannerId FOREIGN KEY (ScannerId) REFERENCES dbo.Scanners (ScannerId),
        CONSTRAINT FK_ScannerStageProductPermissions_ProductTypeId FOREIGN KEY (ProductTypeId) REFERENCES dbo.PricingProductTypes (ProductTypeId),
        CONSTRAINT UQ_ScannerStageProductPermissions_Scanner_ProductType_Stage UNIQUE (ScannerId, ProductTypeId, Stage)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ScannerStageProductPermissions_ScannerId_ProductTypeId')
BEGIN
    CREATE INDEX IX_ScannerStageProductPermissions_ScannerId_ProductTypeId
        ON dbo.ScannerStageProductPermissions (ScannerId, ProductTypeId, IsActive);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ScannerStageProductPermissions_ProductTypeId_Stage')
BEGIN
    CREATE INDEX IX_ScannerStageProductPermissions_ProductTypeId_Stage
        ON dbo.ScannerStageProductPermissions (ProductTypeId, Stage, IsActive);
END;

-- Sample usage:
-- INSERT INTO dbo.ScannerStageProductPermissions (ScannerId, ProductTypeId, Stage, IsActive)
-- VALUES (3, 12, N'Cutting', 1), (3, 12, N'Sewing', 1);
