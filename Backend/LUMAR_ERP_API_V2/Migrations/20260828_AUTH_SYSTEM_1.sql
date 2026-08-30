SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH('dbo.Users', 'PasswordHash') IS NULL
    ALTER TABLE dbo.Users ADD PasswordHash nvarchar(512) NULL;

IF COL_LENGTH('dbo.Users', 'SecurityStamp') IS NULL
    ALTER TABLE dbo.Users ADD SecurityStamp uniqueidentifier NOT NULL CONSTRAINT DF_Users_SecurityStamp DEFAULT NEWID();

IF COL_LENGTH('dbo.Users', 'LastLoginUtc') IS NULL
    ALTER TABLE dbo.Users ADD LastLoginUtc datetime2 NULL;

IF OBJECT_ID('dbo.UserSessions', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserSessions
    (
        SessionId uniqueidentifier NOT NULL CONSTRAINT PK_UserSessions PRIMARY KEY,
        UserId int NOT NULL,
        TokenHash varbinary(32) NOT NULL,
        CreatedAtUtc datetime2 NOT NULL,
        ExpiresAtUtc datetime2 NOT NULL,
        LastUsedAtUtc datetime2 NULL,
        RevokedAtUtc datetime2 NULL,
        RevocationReason nvarchar(100) NULL,
        CONSTRAINT FK_UserSessions_Users_UserId FOREIGN KEY (UserId) REFERENCES dbo.Users(UserID),
        CONSTRAINT UQ_UserSessions_TokenHash UNIQUE (TokenHash)
    );
    CREATE INDEX IX_UserSessions_UserId_Active ON dbo.UserSessions(UserId, ExpiresAtUtc) WHERE RevokedAtUtc IS NULL;
END;

COMMIT TRANSACTION;