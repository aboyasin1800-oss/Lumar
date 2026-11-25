SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() NOT IN (N'LUMAR_ERP_TEST', N'LUMAR_ERP_ES_VALIDATION', N'LUMAR_ERP_ES_VALIDATION_R3')
    THROW 53001, N'Account recovery migration is restricted to approved development and validation databases.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.AccountRecoveryRequests', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.AccountRecoveryRequests
        (
            RecoveryRequestId bigint IDENTITY(1,1) NOT NULL PRIMARY KEY,
            AccountType nvarchar(20) NOT NULL,
            NormalizedPhone nvarchar(30) NOT NULL,
            SubmittedUsername nvarchar(50) NULL,
            VerificationInfo nvarchar(1000) NULL,
            TargetUserId int NULL,
            TargetMobileAccountId int NULL,
            TargetCustomerId int NULL,
            TargetEmployeeId int NULL,
            TargetSupplierId int NULL,
            RequestStatus nvarchar(20) NOT NULL CONSTRAINT DF_AccountRecoveryRequests_Status DEFAULT(N'Pending'),
            PublicRequestKey uniqueidentifier NOT NULL CONSTRAINT DF_AccountRecoveryRequests_Key DEFAULT(NEWID()),
            CompletionTokenHash varbinary(32) NULL,
            CompletionTokenExpiresAtUtc datetime2(7) NULL,
            RejectionReason nvarchar(500) NULL,
            CreatedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_AccountRecoveryRequests_CreatedAt DEFAULT(SYSUTCDATETIME()),
            ReviewedAtUtc datetime2(7) NULL,
            ReviewedByUserId int NULL,
            CompletedAtUtc datetime2(7) NULL,
            CONSTRAINT CK_AccountRecoveryRequests_Type CHECK(AccountType IN(N'Customer',N'Employee',N'Supplier')),
            CONSTRAINT CK_AccountRecoveryRequests_Status CHECK(RequestStatus IN(N'Pending',N'MatchRequired',N'Approved',N'Rejected',N'Completed'))
        );
        CREATE UNIQUE INDEX UX_AccountRecoveryRequests_PublicKey ON dbo.AccountRecoveryRequests(PublicRequestKey);
        CREATE INDEX IX_AccountRecoveryRequests_PhoneStatusCreated ON dbo.AccountRecoveryRequests(NormalizedPhone, AccountType, RequestStatus, CreatedAtUtc DESC);
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
