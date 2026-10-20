SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.MobileAccounts', N'U') IS NULL
        THROW 51600, N'MobileAccounts is required for mobile push device registration.', 1;

    IF OBJECT_ID(N'dbo.MobilePushDevices', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.MobilePushDevices (
            MobilePushDeviceId int IDENTITY(1,1) NOT NULL,
            MobileAccountId int NOT NULL,
            Provider nvarchar(32) NOT NULL,
            Platform nvarchar(32) NOT NULL,
            DeviceKey nvarchar(128) NOT NULL,
            PushTokenCiphertext nvarchar(max) NOT NULL,
            TokenHash nvarchar(128) NOT NULL,
            IsActive bit NOT NULL CONSTRAINT DF_MobilePushDevices_IsActive DEFAULT ((1)),
            IsDisabled bit NOT NULL CONSTRAINT DF_MobilePushDevices_IsDisabled DEFAULT ((0)),
            CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_MobilePushDevices_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
            UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_MobilePushDevices_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
            LastRegisteredAtUtc datetime2(3) NULL,
            DisabledAtUtc datetime2(3) NULL,
            DisabledReason nvarchar(256) NULL,
            DeviceName nvarchar(128) NULL,
            AppVersion nvarchar(64) NULL,
            CONSTRAINT PK_MobilePushDevices PRIMARY KEY (MobilePushDeviceId),
            CONSTRAINT FK_MobilePushDevices_MobileAccounts FOREIGN KEY (MobileAccountId) REFERENCES dbo.MobileAccounts(MobileAccountId),
            CONSTRAINT CK_MobilePushDevices_Provider CHECK (Provider IN (N'FCM')),
            CONSTRAINT CK_MobilePushDevices_Platform CHECK (Platform IN (N'Android')),
            CONSTRAINT UQ_MobilePushDevices_TokenHash UNIQUE (TokenHash),
            CONSTRAINT UQ_MobilePushDevices_DeviceKey UNIQUE (DeviceKey)
        );
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'dbo.MobilePushDevices') AND name = N'FK_MobilePushDevices_MobileAccounts')
        ALTER TABLE dbo.MobilePushDevices WITH CHECK ADD CONSTRAINT FK_MobilePushDevices_MobileAccounts FOREIGN KEY (MobileAccountId) REFERENCES dbo.MobileAccounts(MobileAccountId);

    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.MobilePushDevices') AND name = N'CK_MobilePushDevices_Provider')
        ALTER TABLE dbo.MobilePushDevices WITH CHECK ADD CONSTRAINT CK_MobilePushDevices_Provider CHECK (Provider IN (N'FCM'));

    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.MobilePushDevices') AND name = N'CK_MobilePushDevices_Platform')
        ALTER TABLE dbo.MobilePushDevices WITH CHECK ADD CONSTRAINT CK_MobilePushDevices_Platform CHECK (Platform IN (N'Android'));

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.MobilePushDevices') AND name = N'IX_MobilePushDevices_MobileAccountId_IsActive')
        CREATE INDEX IX_MobilePushDevices_MobileAccountId_IsActive ON dbo.MobilePushDevices(MobileAccountId, IsActive, UpdatedAtUtc DESC);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.MobilePushDevices') AND name = N'IX_MobilePushDevices_DeviceKey')
        CREATE INDEX IX_MobilePushDevices_DeviceKey ON dbo.MobilePushDevices(DeviceKey);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.MobilePushDevices') AND name = N'IX_MobilePushDevices_TokenHash')
        CREATE INDEX IX_MobilePushDevices_TokenHash ON dbo.MobilePushDevices(TokenHash);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
