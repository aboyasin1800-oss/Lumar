SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

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
        THROW 51110, N'Cash movements require a caller-owned SQL transaction.', 1;

    IF @CashDirection <> 1 OR @Amount <= 0 OR @CurrencyCode <> 'YER'
        THROW 51111, N'The requested cash movement is outside the Phase-1 contract.', 1;

    DECLARE @eventAmount decimal(18,2);
    DECLARE @eventType tinyint;
    SELECT @eventAmount = PostingAmount, @eventType = AccountingEventType
    FROM dbo.AccountingEvents WITH (UPDLOCK, HOLDLOCK)
    WHERE AccountingEventId = @AccountingEventId;

    IF @eventAmount IS NULL OR @eventType NOT IN (1,2) OR @eventAmount <> @Amount
        THROW 51112, N'The accounting event is not an eligible cash receipt.', 1;

    DECLARE @cashControlLedgerAccountId int = (
        SELECT TOP (1) LedgerAccountId
        FROM dbo.LedgerAccounts WITH (UPDLOCK, HOLDLOCK)
        WHERE AccountCode = N'1000' AND IsActive = 1
        ORDER BY LedgerAccountId
    );

    IF @cashControlLedgerAccountId IS NULL
        THROW 51113, N'The approved cash control account 1000 is unavailable.', 1;

    DECLARE @isEligible bit;
    SELECT @isEligible = CASE
        WHEN IsActive = 1
         AND CashAccountType = 1
         AND AllowsReceipts = 1
         AND CurrencyCode = @CurrencyCode
         AND LedgerControlAccountId = @cashControlLedgerAccountId
        THEN 1 ELSE 0 END
    FROM dbo.CashAccounts WITH (UPDLOCK, HOLDLOCK)
    WHERE CashAccountId = @CashAccountId;

    IF @isEligible IS NULL OR @isEligible = 0
        THROW 51113, N'The explicit cash account is unavailable for this cash receipt.', 1;

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
            THROW 51114, N'The cash movement retry conflicts with the official movement.', 1;

        SELECT @existingMovementId AS CashMovementId, CAST(1 AS bit) AS IsExisting;
        RETURN;
    END;

    BEGIN TRY
        EXEC sys.sp_set_session_context @key=N'CashMovementWriter', @value=1;

        DECLARE @cashMovementOutput TABLE (CashMovementId bigint NOT NULL);
        INSERT INTO dbo.CashMovements (CashAccountId, AccountingEventId, CashDirection, Amount, OccurredAt)
        OUTPUT INSERTED.CashMovementId INTO @cashMovementOutput(CashMovementId)
        VALUES (@CashAccountId, @AccountingEventId, @CashDirection, @Amount, SYSUTCDATETIME());

        EXEC sys.sp_set_session_context @key=N'CashMovementWriter', @value=NULL;
        SELECT CashMovementId, CAST(0 AS bit) AS IsExisting FROM @cashMovementOutput;
    END TRY
    BEGIN CATCH
        EXEC sys.sp_set_session_context @key=N'CashMovementWriter', @value=NULL;
        THROW;
    END CATCH;
END;
GO
