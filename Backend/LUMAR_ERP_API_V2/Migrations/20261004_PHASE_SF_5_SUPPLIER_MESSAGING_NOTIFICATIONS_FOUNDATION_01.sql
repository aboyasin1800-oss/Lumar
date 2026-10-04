SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

IF DB_NAME() <> N'LUMAR_ERP_ES_VALIDATION'
    THROW 51600, N'This Phase SF-5 supplier messaging foundation migration is restricted to LUMAR_ERP_ES_VALIDATION.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.Suppliers', N'U') IS NULL
        THROW 51601, N'Suppliers is required to create supplier-owned message and notification records.', 1;

    IF OBJECT_ID(N'dbo.MobileAccounts', N'U') IS NULL
        THROW 51602, N'MobileAccounts is required to bind supplier messages and notifications to the authenticated mobile account.', 1;

    IF OBJECT_ID(N'dbo.SupplierMessages', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.SupplierMessages
        (
            SupplierMessageId int IDENTITY(1,1) NOT NULL,
            SupplierId int NOT NULL,
            MobileAccountId int NOT NULL,
            MessageType nvarchar(64) NOT NULL,
            Subject nvarchar(255) NULL,
            Body nvarchar(MAX) NOT NULL,
            Channel nvarchar(32) NOT NULL,
            RelatedEntityType nvarchar(64) NULL,
            RelatedEntityId int NULL,
            DeliveryStatus nvarchar(32) NOT NULL CONSTRAINT DF_SupplierMessages_DeliveryStatus DEFAULT N'Queued',
            IsRead bit NOT NULL CONSTRAINT DF_SupplierMessages_IsRead DEFAULT 0,
            ReadAtUtc datetime2(3) NULL,
            CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_SupplierMessages_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
            SentAtUtc datetime2(3) NULL,
            IdempotencyKey nvarchar(128) NOT NULL,
            CONSTRAINT PK_SupplierMessages PRIMARY KEY (SupplierMessageId),
            CONSTRAINT FK_SupplierMessages_Suppliers FOREIGN KEY (SupplierId) REFERENCES dbo.Suppliers(SupplierId),
            CONSTRAINT FK_SupplierMessages_MobileAccounts FOREIGN KEY (MobileAccountId) REFERENCES dbo.MobileAccounts(MobileAccountId),
            CONSTRAINT CK_SupplierMessages_MessageType CHECK (MessageType IN (N'Invoice', N'Payment', N'GoodsReceipt', N'System', N'General')),
            CONSTRAINT CK_SupplierMessages_Channel CHECK (Channel IN (N'InApp', N'Email', N'SMS', N'WhatsApp')),
            CONSTRAINT CK_SupplierMessages_DeliveryStatus CHECK (DeliveryStatus IN (N'Queued', N'Sent', N'Delivered', N'Failed')),
            CONSTRAINT CK_SupplierMessages_Idempotency CHECK (LEN(IdempotencyKey) > 0)
        );
    END;

    IF OBJECT_ID(N'dbo.SupplierNotifications', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.SupplierNotifications
        (
            SupplierNotificationId int IDENTITY(1,1) NOT NULL,
            SupplierId int NOT NULL,
            MobileAccountId int NOT NULL,
            NotificationType nvarchar(64) NOT NULL,
            Title nvarchar(255) NOT NULL,
            Body nvarchar(MAX) NOT NULL,
            IsRead bit NOT NULL CONSTRAINT DF_SupplierNotifications_IsRead DEFAULT 0,
            ReadAtUtc datetime2(3) NULL,
            CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_SupplierNotifications_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
            ExpiresAtUtc datetime2(3) NULL,
            ReferenceType nvarchar(64) NULL,
            ReferenceId int NULL,
            CONSTRAINT PK_SupplierNotifications PRIMARY KEY (SupplierNotificationId),
            CONSTRAINT FK_SupplierNotifications_Suppliers FOREIGN KEY (SupplierId) REFERENCES dbo.Suppliers(SupplierId),
            CONSTRAINT FK_SupplierNotifications_MobileAccounts FOREIGN KEY (MobileAccountId) REFERENCES dbo.MobileAccounts(MobileAccountId),
            CONSTRAINT CK_SupplierNotifications_NotificationType CHECK (NotificationType IN (N'Invoice', N'Payment', N'GoodsReceipt', N'System', N'General'))
        );
    END;

    IF OBJECT_ID(N'dbo.SupplierAnnouncements', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.SupplierAnnouncements
        (
            SupplierAnnouncementId int IDENTITY(1,1) NOT NULL,
            SupplierId int NOT NULL,
            Title nvarchar(255) NOT NULL,
            Body nvarchar(MAX) NOT NULL,
            IsActive bit NOT NULL CONSTRAINT DF_SupplierAnnouncements_IsActive DEFAULT 1,
            StartsAtUtc datetime2(3) NOT NULL,
            EndsAtUtc datetime2(3) NULL,
            CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_SupplierAnnouncements_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
            CONSTRAINT PK_SupplierAnnouncements PRIMARY KEY (SupplierAnnouncementId),
            CONSTRAINT FK_SupplierAnnouncements_Suppliers FOREIGN KEY (SupplierId) REFERENCES dbo.Suppliers(SupplierId),
            CONSTRAINT CK_SupplierAnnouncements_Validity CHECK (EndsAtUtc IS NULL OR EndsAtUtc >= StartsAtUtc)
        );
    END;

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.SupplierMessages') AND name = N'IX_SupplierMessages_SupplierId_CreatedAtUtc')
    BEGIN
        CREATE INDEX IX_SupplierMessages_SupplierId_CreatedAtUtc
            ON dbo.SupplierMessages(SupplierId, CreatedAtUtc DESC);
    END;

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.SupplierNotifications') AND name = N'IX_SupplierNotifications_SupplierId_CreatedAtUtc')
    BEGIN
        CREATE INDEX IX_SupplierNotifications_SupplierId_CreatedAtUtc
            ON dbo.SupplierNotifications(SupplierId, CreatedAtUtc DESC);
    END;

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.SupplierAnnouncements') AND name = N'IX_SupplierAnnouncements_SupplierId_IsActive')
    BEGIN
        CREATE INDEX IX_SupplierAnnouncements_SupplierId_IsActive
            ON dbo.SupplierAnnouncements(SupplierId, IsActive, StartsAtUtc, EndsAtUtc);
    END;

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.SupplierMessages') AND name = N'UX_SupplierMessages_IdempotencyKey')
    BEGIN
        CREATE UNIQUE INDEX UX_SupplierMessages_IdempotencyKey
            ON dbo.SupplierMessages(IdempotencyKey)
            WHERE IdempotencyKey IS NOT NULL;
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
