SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.LoyaltyProgramSettings', N'U') IS NULL
    THROW 51000, 'RL6 migration blocked: dbo.LoyaltyProgramSettings does not exist.', 1;
IF OBJECT_ID(N'dbo.LoyaltyAccounts', N'U') IS NULL
    THROW 51000, 'RL6 migration blocked: dbo.LoyaltyAccounts does not exist.', 1;

DECLARE @ProgramColumns TABLE (ColumnName sysname NOT NULL, ColumnDefinition nvarchar(400) NOT NULL);
INSERT INTO @ProgramColumns (ColumnName, ColumnDefinition)
VALUES
    (N'AllowRedemption', N'bit NOT NULL CONSTRAINT DF_LoyaltyProgramSettings_AllowRedemption DEFAULT (1)'),
    (N'MinimumRedemptionPoints', N'decimal(18,2) NOT NULL CONSTRAINT DF_LoyaltyProgramSettings_MinimumRedemptionPoints DEFAULT (0)'),
    (N'MaximumRedemptionPoints', N'decimal(18,2) NOT NULL CONSTRAINT DF_LoyaltyProgramSettings_MaximumRedemptionPoints DEFAULT (0)'),
    (N'LoyaltyFreezeEnabled', N'bit NOT NULL CONSTRAINT DF_LoyaltyProgramSettings_LoyaltyFreezeEnabled DEFAULT (0)'),
    (N'GracePeriodDays', N'int NOT NULL CONSTRAINT DF_LoyaltyProgramSettings_GracePeriodDays DEFAULT (180)'),
    (N'WarningPeriodDays', N'int NOT NULL CONSTRAINT DF_LoyaltyProgramSettings_WarningPeriodDays DEFAULT (30)'),
    (N'ManualReactivationEnabled', N'bit NOT NULL CONSTRAINT DF_LoyaltyProgramSettings_ManualReactivationEnabled DEFAULT (1)'),
    (N'PurchaseReactivationEnabled', N'bit NOT NULL CONSTRAINT DF_LoyaltyProgramSettings_PurchaseReactivationEnabled DEFAULT (1)');

IF EXISTS
(
    SELECT 1
    FROM @ProgramColumns expected
    JOIN sys.columns existing
      ON existing.object_id = OBJECT_ID(N'dbo.LoyaltyProgramSettings')
     AND existing.name = expected.ColumnName
)
    THROW 51000, 'RL6 migration blocked: one or more program settings columns already exist; expected zero.', 1;

DECLARE @ProgramSql nvarchar(max) = N'';
SELECT @ProgramSql += N'ALTER TABLE dbo.LoyaltyProgramSettings ADD ' + QUOTENAME(ColumnName) + N' ' + ColumnDefinition + N';' + CHAR(13)
FROM @ProgramColumns;
EXEC sys.sp_executesql @ProgramSql;

EXEC sys.sp_executesql N'
ALTER TABLE dbo.LoyaltyProgramSettings
    ADD CONSTRAINT CK_LoyaltyProgramSettings_RedemptionBounds
    CHECK (MinimumRedemptionPoints >= 0 AND MaximumRedemptionPoints >= 0 AND (MaximumRedemptionPoints = 0 OR MaximumRedemptionPoints >= MinimumRedemptionPoints));';
EXEC sys.sp_executesql N'
ALTER TABLE dbo.LoyaltyProgramSettings
    ADD CONSTRAINT CK_LoyaltyProgramSettings_FreezePeriods
    CHECK (GracePeriodDays > 0 AND WarningPeriodDays >= 0 AND WarningPeriodDays < GracePeriodDays);';

DECLARE @AccountColumns TABLE (ColumnName sysname NOT NULL, ColumnDefinition nvarchar(400) NOT NULL);
INSERT INTO @AccountColumns (ColumnName, ColumnDefinition)
VALUES
    (N'LoyaltyAccountStatus', N'nvarchar(20) NOT NULL CONSTRAINT DF_LoyaltyAccounts_LoyaltyAccountStatus DEFAULT (N''Active'')'),
    (N'WarningStartedAtUtc', N'datetime2 NULL'),
    (N'FrozenAtUtc', N'datetime2 NULL'),
    (N'ReactivatedAtUtc', N'datetime2 NULL'),
    (N'FreezeReason', N'nvarchar(500) NULL'),
    (N'LastQualifyingActivityAtUtc', N'datetime2 NULL');

IF EXISTS
(
    SELECT 1
    FROM @AccountColumns expected
    JOIN sys.columns existing
      ON existing.object_id = OBJECT_ID(N'dbo.LoyaltyAccounts')
     AND existing.name = expected.ColumnName
)
    THROW 51000, 'RL6 migration blocked: one or more loyalty account status columns already exist; expected zero.', 1;

DECLARE @AccountSql nvarchar(max) = N'';
SELECT @AccountSql += N'ALTER TABLE dbo.LoyaltyAccounts ADD ' + QUOTENAME(ColumnName) + N' ' + ColumnDefinition + N';' + CHAR(13)
FROM @AccountColumns;
EXEC sys.sp_executesql @AccountSql;

EXEC sys.sp_executesql N'
ALTER TABLE dbo.LoyaltyAccounts
    ADD CONSTRAINT CK_LoyaltyAccounts_LoyaltyAccountStatus
    CHECK (LoyaltyAccountStatus IN (N''Active'', N''Warning'', N''Frozen''));';

COMMIT TRANSACTION;
