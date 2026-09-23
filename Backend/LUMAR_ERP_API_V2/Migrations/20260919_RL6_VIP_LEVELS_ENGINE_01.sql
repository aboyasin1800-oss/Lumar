SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.VipLevels', N'U') IS NULL
    THROW 51000, 'RL6 VIP migration blocked: dbo.VipLevels does not exist.', 1;
IF OBJECT_ID(N'dbo.LoyaltyAccounts', N'U') IS NULL
    THROW 51000, 'RL6 VIP migration blocked: dbo.LoyaltyAccounts does not exist.', 1;
IF OBJECT_ID(N'dbo.ReferralTransactions', N'U') IS NULL
    THROW 51000, 'RL6 VIP migration blocked: dbo.ReferralTransactions does not exist.', 1;
IF OBJECT_ID(N'dbo.Orders', N'U') IS NULL
    THROW 51000, 'RL6 VIP migration blocked: dbo.Orders does not exist.', 1;

IF COL_LENGTH(N'dbo.LoyaltyAccounts', N'VipLevelEvaluatedAtUtc') IS NULL
    ALTER TABLE dbo.LoyaltyAccounts ADD VipLevelEvaluatedAtUtc datetime2 NULL;
IF COL_LENGTH(N'dbo.LoyaltyAccounts', N'VipLevelReason') IS NULL
    ALTER TABLE dbo.LoyaltyAccounts ADD VipLevelReason nvarchar(2000) NULL;
IF COL_LENGTH(N'dbo.LoyaltyAccounts', N'VipLevelScore') IS NULL
    ALTER TABLE dbo.LoyaltyAccounts ADD VipLevelScore decimal(9,4) NULL;
IF COL_LENGTH(N'dbo.LoyaltyAccounts', N'VipDirectReferralCount') IS NULL
    ALTER TABLE dbo.LoyaltyAccounts ADD VipDirectReferralCount int NOT NULL CONSTRAINT DF_LoyaltyAccounts_VipDirectReferralCount DEFAULT (0);
IF COL_LENGTH(N'dbo.LoyaltyAccounts', N'VipOwnOrderCount') IS NULL
    ALTER TABLE dbo.LoyaltyAccounts ADD VipOwnOrderCount int NOT NULL CONSTRAINT DF_LoyaltyAccounts_VipOwnOrderCount DEFAULT (0);
IF COL_LENGTH(N'dbo.LoyaltyAccounts', N'VipNetworkOrderCount') IS NULL
    ALTER TABLE dbo.LoyaltyAccounts ADD VipNetworkOrderCount int NOT NULL CONSTRAINT DF_LoyaltyAccounts_VipNetworkOrderCount DEFAULT (0);
IF COL_LENGTH(N'dbo.LoyaltyAccounts', N'VipNetworkSize') IS NULL
    ALTER TABLE dbo.LoyaltyAccounts ADD VipNetworkSize int NOT NULL CONSTRAINT DF_LoyaltyAccounts_VipNetworkSize DEFAULT (0);
IF COL_LENGTH(N'dbo.LoyaltyAccounts', N'VipNetworkMaxDepth') IS NULL
    ALTER TABLE dbo.LoyaltyAccounts ADD VipNetworkMaxDepth int NOT NULL CONSTRAINT DF_LoyaltyAccounts_VipNetworkMaxDepth DEFAULT (0);

IF OBJECT_ID(N'dbo.VipLevelEvaluationCriteria', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.VipLevelEvaluationCriteria
    (
        VipLevelCriteriaId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_VipLevelEvaluationCriteria PRIMARY KEY,
        VipLevelId int NOT NULL,
        MinimumDirectReferrals int NOT NULL CONSTRAINT DF_VipLevelEvaluationCriteria_MinimumDirectReferrals DEFAULT (0),
        MinimumOwnOrders int NOT NULL CONSTRAINT DF_VipLevelEvaluationCriteria_MinimumOwnOrders DEFAULT (0),
        MinimumNetworkOrders int NOT NULL CONSTRAINT DF_VipLevelEvaluationCriteria_MinimumNetworkOrders DEFAULT (0),
        MinimumNetworkSize int NOT NULL CONSTRAINT DF_VipLevelEvaluationCriteria_MinimumNetworkSize DEFAULT (0),
        MinimumScore decimal(5,2) NOT NULL CONSTRAINT DF_VipLevelEvaluationCriteria_MinimumScore DEFAULT (80.00),
        DirectReferralWeight decimal(5,2) NOT NULL CONSTRAINT DF_VipLevelEvaluationCriteria_DirectReferralWeight DEFAULT (30.00),
        OwnOrderWeight decimal(5,2) NOT NULL CONSTRAINT DF_VipLevelEvaluationCriteria_OwnOrderWeight DEFAULT (25.00),
        NetworkOrderWeight decimal(5,2) NOT NULL CONSTRAINT DF_VipLevelEvaluationCriteria_NetworkOrderWeight DEFAULT (25.00),
        NetworkSizeWeight decimal(5,2) NOT NULL CONSTRAINT DF_VipLevelEvaluationCriteria_NetworkSizeWeight DEFAULT (20.00),
        CreatedAtUtc datetime2 NOT NULL CONSTRAINT DF_VipLevelEvaluationCriteria_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc datetime2 NOT NULL CONSTRAINT DF_VipLevelEvaluationCriteria_UpdatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_VipLevelEvaluationCriteria_VipLevelId UNIQUE (VipLevelId),
        CONSTRAINT FK_VipLevelEvaluationCriteria_VipLevels FOREIGN KEY (VipLevelId) REFERENCES dbo.VipLevels(VipLevelId),
        CONSTRAINT CK_VipLevelEvaluationCriteria_Minimums CHECK (MinimumDirectReferrals >= 0 AND MinimumOwnOrders >= 0 AND MinimumNetworkOrders >= 0 AND MinimumNetworkSize >= 0),
        CONSTRAINT CK_VipLevelEvaluationCriteria_Score CHECK (MinimumScore >= 0 AND MinimumScore <= 100),
        CONSTRAINT CK_VipLevelEvaluationCriteria_Weights CHECK (DirectReferralWeight >= 0 AND OwnOrderWeight >= 0 AND NetworkOrderWeight >= 0 AND NetworkSizeWeight >= 0 AND DirectReferralWeight + OwnOrderWeight + NetworkOrderWeight + NetworkSizeWeight = 100)
    );
END;

IF NOT EXISTS (SELECT 1 FROM dbo.VipLevelEvaluationCriteria c INNER JOIN dbo.VipLevels v ON v.VipLevelId = c.VipLevelId WHERE v.Code = N'BRONZE')
    INSERT INTO dbo.VipLevelEvaluationCriteria (VipLevelId, MinimumDirectReferrals, MinimumOwnOrders, MinimumNetworkOrders, MinimumNetworkSize, MinimumScore, DirectReferralWeight, OwnOrderWeight, NetworkOrderWeight, NetworkSizeWeight)
    SELECT VipLevelId, 0, 0, 0, 0, 0, 30, 25, 25, 20 FROM dbo.VipLevels WHERE Code = N'BRONZE';

IF NOT EXISTS (SELECT 1 FROM dbo.VipLevelEvaluationCriteria c INNER JOIN dbo.VipLevels v ON v.VipLevelId = c.VipLevelId WHERE v.Code = N'SILVER')
    INSERT INTO dbo.VipLevelEvaluationCriteria (VipLevelId, MinimumDirectReferrals, MinimumOwnOrders, MinimumNetworkOrders, MinimumNetworkSize, MinimumScore, DirectReferralWeight, OwnOrderWeight, NetworkOrderWeight, NetworkSizeWeight)
    SELECT VipLevelId, 1, 1, 3, 2, 80, 30, 25, 25, 20 FROM dbo.VipLevels WHERE Code = N'SILVER';

IF NOT EXISTS (SELECT 1 FROM dbo.VipLevelEvaluationCriteria c INNER JOIN dbo.VipLevels v ON v.VipLevelId = c.VipLevelId WHERE v.Code = N'GOLD')
    INSERT INTO dbo.VipLevelEvaluationCriteria (VipLevelId, MinimumDirectReferrals, MinimumOwnOrders, MinimumNetworkOrders, MinimumNetworkSize, MinimumScore, DirectReferralWeight, OwnOrderWeight, NetworkOrderWeight, NetworkSizeWeight)
    SELECT VipLevelId, 1, 5, 5, 4, 80, 30, 25, 25, 20 FROM dbo.VipLevels WHERE Code = N'GOLD';

IF NOT EXISTS (SELECT 1 FROM dbo.VipLevelEvaluationCriteria c INNER JOIN dbo.VipLevels v ON v.VipLevelId = c.VipLevelId WHERE v.Code = N'PLATINUM')
    INSERT INTO dbo.VipLevelEvaluationCriteria (VipLevelId, MinimumDirectReferrals, MinimumOwnOrders, MinimumNetworkOrders, MinimumNetworkSize, MinimumScore, DirectReferralWeight, OwnOrderWeight, NetworkOrderWeight, NetworkSizeWeight)
    SELECT VipLevelId, 2, 10, 10, 10, 80, 30, 25, 25, 20 FROM dbo.VipLevels WHERE Code = N'PLATINUM';

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.LoyaltyAccounts') AND name = N'IX_LoyaltyAccounts_VipLevelEvaluatedAtUtc')
    CREATE INDEX IX_LoyaltyAccounts_VipLevelEvaluatedAtUtc ON dbo.LoyaltyAccounts(VipLevelEvaluatedAtUtc, CustomerId);

COMMIT TRANSACTION;
