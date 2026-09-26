SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.AccountingEvents', N'U') IS NULL
        THROW 51100, N'AccountingEvents foundation is required before cash movements.', 1;

    IF OBJECT_ID(N'dbo.CashMovements', N'U') IS NOT NULL
        THROW 51101, N'CashMovements already exists. This migration must not be reapplied.', 1;

    IF COL_LENGTH(N'dbo.CashAccounts', N'CashAccountType') IS NOT NULL
        THROW 51102, N'CashAccounts is already configured for the cash foundation.', 1;

    ALTER TABLE dbo.CashAccounts ADD
        CashAccountType tinyint NULL,
        CurrencyCode char(3) NULL,
        LedgerControlAccountId int NULL,
        AllowsReceipts bit NULL;

    EXEC(N'
        ALTER TABLE dbo.CashAccounts
            ADD CONSTRAINT CK_CashAccounts_Type CHECK (CashAccountType IS NULL OR CashAccountType IN (1,2,3));
        ALTER TABLE dbo.CashAccounts
            ADD CONSTRAINT CK_CashAccounts_CurrencyCode CHECK (CurrencyCode IS NULL OR CurrencyCode COLLATE Latin1_General_100_BIN2 LIKE ''[A-Z][A-Z][A-Z]'');
        ALTER TABLE dbo.CashAccounts
            ADD CONSTRAINT FK_CashAccounts_LedgerAccounts_LedgerControlAccountId
            FOREIGN KEY (LedgerControlAccountId) REFERENCES dbo.LedgerAccounts(LedgerAccountId);');

    IF NOT EXISTS (SELECT 1 FROM dbo.LedgerAccounts WHERE LedgerAccountId = 7 AND AccountCode = N'1000' AND IsActive = 1)
        THROW 51103, N'The approved cash control account 1000 is unavailable.', 1;

        DECLARE @configuredAccounts int;
        EXEC sys.sp_executesql N'
                UPDATE dbo.CashAccounts
                SET CashAccountType = 1,
                        CurrencyCode = ''YER'',
                        LedgerControlAccountId = 7,
                        AllowsReceipts = 1
                WHERE CashAccountId = 1
                    AND AccountName = N''Petty Cash 0719001842'';
                SELECT @rows = @@ROWCOUNT;',
                N'@rows int OUTPUT', @rows = @configuredAccounts OUTPUT;

        IF @configuredAccounts <> 1
        THROW 51104, N'The approved Go-Live cash account was not found.', 1;

    CREATE TABLE dbo.CashMovements
    (
        CashMovementId bigint IDENTITY(1,1) NOT NULL,
        CashAccountId int NOT NULL,
        AccountingEventId bigint NOT NULL,
        CashDirection tinyint NOT NULL,
        Amount decimal(18,2) NOT NULL,
        OccurredAt datetime2(7) NOT NULL,
        CreatedAt datetime2(7) NOT NULL
            CONSTRAINT DF_CashMovements_CreatedAt DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_CashMovements PRIMARY KEY (CashMovementId),
        CONSTRAINT CK_CashMovements_Direction CHECK (CashDirection IN (1,2)),
        CONSTRAINT CK_CashMovements_Amount CHECK (Amount > 0),
        CONSTRAINT FK_CashMovements_CashAccounts_CashAccountId
            FOREIGN KEY (CashAccountId) REFERENCES dbo.CashAccounts(CashAccountId),
        CONSTRAINT FK_CashMovements_AccountingEvents_AccountingEventId
            FOREIGN KEY (AccountingEventId) REFERENCES dbo.AccountingEvents(AccountingEventId)
    );

    CREATE UNIQUE INDEX UX_CashMovements_AccountingEventId
        ON dbo.CashMovements(AccountingEventId);

    CREATE INDEX IX_CashMovements_CashAccount_OccurredAt
        ON dbo.CashMovements(CashAccountId, OccurredAt, CashMovementId);

    DECLARE @procedureSql nvarchar(max) = N'
CREATE OR ALTER PROCEDURE dbo.usp_PostCashMovement
    @AccountingEventId bigint,
    @CashAccountId int,
    @CashDirection tinyint,
    @Amount decimal(18,2),
    @CurrencyCode char(3)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @@TRANCOUNT = 0
        THROW 51110, N''Cash movements require a caller-owned SQL transaction.'', 1;

    IF @CashDirection <> 1 OR @Amount <= 0 OR @CurrencyCode <> ''YER''
        THROW 51111, N''The requested cash movement is outside the Phase-1 contract.'', 1;

    DECLARE @eventAmount decimal(18,2);
    DECLARE @eventType tinyint;
    SELECT @eventAmount = PostingAmount, @eventType = AccountingEventType
    FROM dbo.AccountingEvents WITH (UPDLOCK, HOLDLOCK)
    WHERE AccountingEventId = @AccountingEventId;

    IF @eventAmount IS NULL OR @eventType NOT IN (1,2) OR @eventAmount <> @Amount
        THROW 51112, N''The accounting event is not an eligible cash receipt.'', 1;

    DECLARE @isEligible bit;
    SELECT @isEligible = CASE WHEN IsActive = 1
                                  AND CashAccountType = 1
                                  AND AllowsReceipts = 1
                                  AND CurrencyCode = @CurrencyCode
                                  AND LedgerControlAccountId = 7
                              THEN 1 ELSE 0 END
    FROM dbo.CashAccounts WITH (UPDLOCK, HOLDLOCK)
    WHERE CashAccountId = @CashAccountId;

    IF @isEligible IS NULL OR @isEligible = 0
        THROW 51113, N''The explicit cash account is unavailable for this cash receipt.'', 1;

    DECLARE @existingMovementId bigint;
    DECLARE @existingCashAccountId int;
    DECLARE @existingDirection tinyint;
    DECLARE @existingAmount decimal(18,2);
    SELECT @existingMovementId = CashMovementId,
           @existingCashAccountId = CashAccountId,
           @existingDirection = CashDirection,
           @existingAmount = Amount
    FROM dbo.CashMovements WITH (UPDLOCK, HOLDLOCK)
    WHERE AccountingEventId = @AccountingEventId;

    IF @existingMovementId IS NOT NULL
    BEGIN
        IF @existingCashAccountId <> @CashAccountId OR @existingDirection <> @CashDirection OR @existingAmount <> @Amount
            THROW 51114, N''The cash movement retry conflicts with the official movement.'', 1;

        SELECT @existingMovementId AS CashMovementId, CAST(1 AS bit) AS IsExisting;
        RETURN;
    END;

    BEGIN TRY
        EXEC sys.sp_set_session_context @key=N''CashMovementWriter'', @value=1;

        DECLARE @cashMovementOutput TABLE (CashMovementId bigint NOT NULL);
        INSERT INTO dbo.CashMovements (CashAccountId, AccountingEventId, CashDirection, Amount, OccurredAt)
        OUTPUT INSERTED.CashMovementId INTO @cashMovementOutput(CashMovementId)
        VALUES (@CashAccountId, @AccountingEventId, @CashDirection, @Amount, SYSUTCDATETIME());

        EXEC sys.sp_set_session_context @key=N''CashMovementWriter'', @value=NULL;
        SELECT CashMovementId, CAST(0 AS bit) AS IsExisting FROM @cashMovementOutput;
    END TRY
    BEGIN CATCH
        EXEC sys.sp_set_session_context @key=N''CashMovementWriter'', @value=NULL;
        THROW;
    END CATCH;
END;';
    EXEC sys.sp_executesql @procedureSql;

    EXEC(N'
        CREATE OR ALTER TRIGGER dbo.trg_CashMovements_OfficialWriter
        ON dbo.CashMovements
        AFTER INSERT, UPDATE, DELETE
        AS
        BEGIN
            SET NOCOUNT ON;
            IF TRY_CAST(SESSION_CONTEXT(N''CashMovementWriter'') AS int) <> 1
                THROW 51120, N''CashMovements may only be written by dbo.usp_PostCashMovement.'', 1;
        END;');

    IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'db_lumar_cash_movement_writer' AND type = 'R')
        EXEC(N'CREATE ROLE db_lumar_cash_movement_writer');

    GRANT EXECUTE ON dbo.usp_PostCashMovement TO db_lumar_cash_movement_writer;

    DECLARE @cutoverUtc nvarchar(33) = CONVERT(nvarchar(33), SYSUTCDATETIME(), 126) + N'Z';
    EXEC sys.sp_addextendedproperty
        @name = N'CashMovementFoundationCutoverUtc',
        @value = @cutoverUtc,
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'TABLE', @level1name = N'CashMovements';

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;