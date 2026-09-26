SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.CustomerAdvanceApplications', N'U') IS NOT NULL
        THROW 51200, N'CustomerAdvanceApplications already exists. This migration must not be reapplied.', 1;

    IF COL_LENGTH(N'dbo.AccountingEvents', N'CustomerAdvanceApplicationId') IS NOT NULL
        THROW 51201, N'AccountingEvents.CustomerAdvanceApplicationId already exists.', 1;

    IF EXISTS (SELECT 1 FROM dbo.AccountingEvents WHERE AccountingEventType = 6)
        THROW 51202, N'Accounting event type 6 is already in use.', 1;

    CREATE TABLE dbo.CustomerAdvanceApplications
    (
        AdvanceApplicationId bigint IDENTITY(1,1) NOT NULL,
        AdvancePaymentId int NOT NULL,
        OrderId int NOT NULL,
        RevenueAccountingEventId bigint NOT NULL,
        AppliedAmount decimal(18,2) NOT NULL,
        CreatedAt datetime2(7) NOT NULL
            CONSTRAINT DF_CustomerAdvanceApplications_CreatedAt DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_CustomerAdvanceApplications PRIMARY KEY (AdvanceApplicationId),
        CONSTRAINT CK_CustomerAdvanceApplications_AppliedAmount CHECK (AppliedAmount > 0),
        CONSTRAINT FK_CustomerAdvanceApplications_Payments_AdvancePaymentId
            FOREIGN KEY (AdvancePaymentId) REFERENCES dbo.Payments(PaymentID),
        CONSTRAINT FK_CustomerAdvanceApplications_Orders_OrderId
            FOREIGN KEY (OrderId) REFERENCES dbo.Orders(OrderID),
        CONSTRAINT FK_CustomerAdvanceApplications_AccountingEvents_RevenueEventId
            FOREIGN KEY (RevenueAccountingEventId) REFERENCES dbo.AccountingEvents(AccountingEventId)
    );

    CREATE UNIQUE INDEX UX_CustomerAdvanceApplications_Payment_RevenueEvent
        ON dbo.CustomerAdvanceApplications(AdvancePaymentId, RevenueAccountingEventId);

    CREATE INDEX IX_CustomerAdvanceApplications_RevenueEvent
        ON dbo.CustomerAdvanceApplications(RevenueAccountingEventId, AdvanceApplicationId);

    CREATE INDEX IX_CustomerAdvanceApplications_AdvancePayment
        ON dbo.CustomerAdvanceApplications(AdvancePaymentId, AdvanceApplicationId);

    EXEC(N'ALTER TABLE dbo.AccountingEvents ADD CustomerAdvanceApplicationId bigint NULL;');

    EXEC(N'
        ALTER TABLE dbo.AccountingEvents
            ADD CONSTRAINT FK_AccountingEvents_CustomerAdvanceApplications_ApplicationId
            FOREIGN KEY (CustomerAdvanceApplicationId)
            REFERENCES dbo.CustomerAdvanceApplications(AdvanceApplicationId);

        CREATE UNIQUE INDEX UX_AccountingEvents_CustomerAdvanceApplicationId
            ON dbo.AccountingEvents(CustomerAdvanceApplicationId)
            WHERE CustomerAdvanceApplicationId IS NOT NULL;');

    ALTER TABLE dbo.AccountingEvents DROP CONSTRAINT CK_AccountingEvents_Type;
    ALTER TABLE dbo.AccountingEvents DROP CONSTRAINT CK_AccountingEvents_SourceCardinality;

    EXEC(N'
        ALTER TABLE dbo.AccountingEvents ADD
            CONSTRAINT CK_AccountingEvents_Type CHECK (AccountingEventType IN (1,2,3,4,5,6)),
            CONSTRAINT CK_AccountingEvents_SourceCardinality CHECK
            (
               (AccountingEventType IN (1,2)
                AND PaymentId IS NOT NULL
                AND OrderId IS NULL
                AND MeasurementCardPrintHistoryId IS NULL
                AND ReadyMadeInventoryProductId IS NULL
                AND CustomerAdvanceApplicationId IS NULL)
            OR (AccountingEventType = 3
                AND PaymentId IS NULL
                AND OrderId IS NOT NULL
                AND MeasurementCardPrintHistoryId IS NULL
                AND ReadyMadeInventoryProductId IS NULL
                AND CustomerAdvanceApplicationId IS NULL)
            OR (AccountingEventType = 4
                AND PaymentId IS NULL
                AND OrderId IS NULL
                AND MeasurementCardPrintHistoryId IS NOT NULL
                AND ReadyMadeInventoryProductId IS NULL
                AND CustomerAdvanceApplicationId IS NULL)
            OR (AccountingEventType = 5
                AND PaymentId IS NULL
                AND OrderId IS NULL
                AND MeasurementCardPrintHistoryId IS NULL
                AND ReadyMadeInventoryProductId IS NOT NULL
                AND CustomerAdvanceApplicationId IS NULL)
            OR (AccountingEventType = 6
                AND PaymentId IS NULL
                AND OrderId IS NULL
                AND MeasurementCardPrintHistoryId IS NULL
                AND ReadyMadeInventoryProductId IS NULL
                AND CustomerAdvanceApplicationId IS NOT NULL)
            );');

    EXEC(N'
CREATE OR ALTER PROCEDURE dbo.usp_PostAccountingEvent
    @AccountingEventType tinyint,
    @PostingAmount decimal(18,2),
    @PaymentId int = NULL,
    @OrderId int = NULL,
    @MeasurementCardPrintHistoryId int = NULL,
    @ReadyMadeInventoryProductId int = NULL,
    @CustomerAdvanceApplicationId bigint = NULL,
    @ReferenceNumber nvarchar(200) = NULL,
    @Description nvarchar(1000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @@TRANCOUNT = 0
        THROW 51210, N''Accounting events require a caller-owned SQL transaction.'', 1;

    IF @AccountingEventType NOT IN (1,2,3,4,5,6) OR @PostingAmount <= 0
        THROW 51211, N''Unsupported accounting event type or posting amount.'', 1;

    IF NOT
    (
           (@AccountingEventType IN (1,2) AND @PaymentId IS NOT NULL AND @OrderId IS NULL AND @MeasurementCardPrintHistoryId IS NULL AND @ReadyMadeInventoryProductId IS NULL AND @CustomerAdvanceApplicationId IS NULL)
        OR (@AccountingEventType = 3 AND @PaymentId IS NULL AND @OrderId IS NOT NULL AND @MeasurementCardPrintHistoryId IS NULL AND @ReadyMadeInventoryProductId IS NULL AND @CustomerAdvanceApplicationId IS NULL)
        OR (@AccountingEventType = 4 AND @PaymentId IS NULL AND @OrderId IS NULL AND @MeasurementCardPrintHistoryId IS NOT NULL AND @ReadyMadeInventoryProductId IS NULL AND @CustomerAdvanceApplicationId IS NULL)
        OR (@AccountingEventType = 5 AND @PaymentId IS NULL AND @OrderId IS NULL AND @MeasurementCardPrintHistoryId IS NULL AND @ReadyMadeInventoryProductId IS NOT NULL AND @CustomerAdvanceApplicationId IS NULL)
        OR (@AccountingEventType = 6 AND @PaymentId IS NULL AND @OrderId IS NULL AND @MeasurementCardPrintHistoryId IS NULL AND @ReadyMadeInventoryProductId IS NULL AND @CustomerAdvanceApplicationId IS NOT NULL)
    )
        THROW 51212, N''The accounting event source does not match its event type.'', 1;

    DECLARE @existingAccountingEventId bigint;
    SELECT @existingAccountingEventId = AccountingEventId
    FROM dbo.AccountingEvents WITH (UPDLOCK,HOLDLOCK)
    WHERE (@AccountingEventType IN (1,2) AND PaymentId = @PaymentId)
       OR (@AccountingEventType = 3 AND OrderId = @OrderId)
       OR (@AccountingEventType = 4 AND MeasurementCardPrintHistoryId = @MeasurementCardPrintHistoryId)
       OR (@AccountingEventType = 5 AND ReadyMadeInventoryProductId = @ReadyMadeInventoryProductId)
       OR (@AccountingEventType = 6 AND CustomerAdvanceApplicationId = @CustomerAdvanceApplicationId);

    IF @existingAccountingEventId IS NOT NULL
    BEGIN
        DECLARE @existingFinancialTransactionId int;
        DECLARE @existingJournalEntryId int;
        SELECT @existingFinancialTransactionId = FinancialTransactionId FROM dbo.FinancialTransactions WITH (UPDLOCK,HOLDLOCK) WHERE AccountingEventId = @existingAccountingEventId;
        SELECT @existingJournalEntryId = JournalEntryId FROM dbo.JournalEntries WITH (UPDLOCK,HOLDLOCK) WHERE AccountingEventId = @existingAccountingEventId;
        IF @existingFinancialTransactionId IS NULL OR @existingJournalEntryId IS NULL
            THROW 51213, N''The existing accounting event is incomplete.'', 1;
        SELECT @existingAccountingEventId AS AccountingEventId, @existingFinancialTransactionId AS FinancialTransactionId, @existingJournalEntryId AS JournalEntryId, CAST(1 AS bit) AS IsExisting;
        RETURN;
    END;

    IF @AccountingEventType IN (1,2)
    BEGIN
        DECLARE @paymentAmount decimal(18,2);
        DECLARE @paymentKind nvarchar(100);
        SELECT @paymentAmount = Amount, @paymentKind = PaymentKind FROM dbo.Payments WITH (UPDLOCK,HOLDLOCK) WHERE PaymentID = @PaymentId;
        IF @paymentAmount IS NULL OR @paymentAmount <> @PostingAmount
            THROW 51214, N''The payment source is missing or has a different amount.'', 1;
        IF (@AccountingEventType = 1 AND @paymentKind <> N''Advance'') OR (@AccountingEventType = 2 AND @paymentKind NOT IN (N''DebtCollection'', N''SaleCash'', N''MeasurementCardPieceSale''))
            THROW 51215, N''The payment kind is not supported by this accounting event.'', 1;
    END;

    IF @AccountingEventType = 3 AND NOT EXISTS (SELECT 1 FROM dbo.Orders WITH (UPDLOCK,HOLDLOCK) WHERE OrderID = @OrderId)
        THROW 51216, N''The revenue order source does not exist.'', 1;

    IF @AccountingEventType = 6
    BEGIN
        DECLARE @applicationAmount decimal(18,2);
        DECLARE @applicationPaymentId int;
        DECLARE @applicationOrderId int;
        DECLARE @applicationRevenueEventId bigint;
        DECLARE @advancePaymentKind nvarchar(100);
        DECLARE @revenueEventType tinyint;
        DECLARE @revenueEventOrderId int;
        SELECT @applicationAmount = app.AppliedAmount, @applicationPaymentId = app.AdvancePaymentId, @applicationOrderId = app.OrderId, @applicationRevenueEventId = app.RevenueAccountingEventId, @advancePaymentKind = p.PaymentKind, @revenueEventType = revenue.AccountingEventType, @revenueEventOrderId = revenue.OrderId
        FROM dbo.CustomerAdvanceApplications app WITH (UPDLOCK,HOLDLOCK)
        INNER JOIN dbo.Payments p WITH (UPDLOCK,HOLDLOCK) ON p.PaymentID = app.AdvancePaymentId
        INNER JOIN dbo.AccountingEvents revenue WITH (UPDLOCK,HOLDLOCK) ON revenue.AccountingEventId = app.RevenueAccountingEventId
        WHERE app.AdvanceApplicationId = @CustomerAdvanceApplicationId;
        IF @applicationAmount IS NULL OR @applicationAmount <> @PostingAmount OR @advancePaymentKind <> N''Advance'' OR @revenueEventType <> 3 OR @revenueEventOrderId <> @applicationOrderId
            THROW 51217, N''The advance application source is invalid.'', 1;
        IF NOT EXISTS (SELECT 1 FROM dbo.Payments WHERE PaymentID = @applicationPaymentId AND OrderID = @applicationOrderId)
            THROW 51218, N''The advance payment does not belong to the application order.'', 1;
    END;

    INSERT INTO dbo.AccountingEvents (AccountingEventType, PostingAmount, PaymentId, OrderId, MeasurementCardPrintHistoryId, ReadyMadeInventoryProductId, CustomerAdvanceApplicationId)
    VALUES (@AccountingEventType, @PostingAmount, @PaymentId, @OrderId, @MeasurementCardPrintHistoryId, @ReadyMadeInventoryProductId, @CustomerAdvanceApplicationId);

    DECLARE @accountingEventId bigint = SCOPE_IDENTITY();
    DECLARE @transactionType nvarchar(200) = CASE @AccountingEventType WHEN 1 THEN N''CustomerAdvance'' WHEN 2 THEN N''CustomerPayment'' WHEN 3 THEN N''RevenueRecognized'' WHEN 4 THEN N''RevenueRecognized'' WHEN 5 THEN N''WipToFinishedGoods'' WHEN 6 THEN N''CustomerAdvanceApplied'' END;
    DECLARE @debitAccountCode nvarchar(100) = CASE @AccountingEventType WHEN 1 THEN N''1000'' WHEN 2 THEN N''1000'' WHEN 3 THEN N''1200'' WHEN 4 THEN N''1200'' WHEN 5 THEN N''1110'' WHEN 6 THEN N''1160'' END;
    DECLARE @creditAccountCode nvarchar(100) = CASE @AccountingEventType WHEN 1 THEN N''1160'' WHEN 2 THEN N''1200'' WHEN 3 THEN N''4200'' WHEN 4 THEN N''4200'' WHEN 5 THEN N''1130'' WHEN 6 THEN N''1200'' END;
    DECLARE @debitLedgerAccountId int;
    DECLARE @creditLedgerAccountId int;
    SELECT TOP (1) @debitLedgerAccountId = LedgerAccountId FROM dbo.LedgerAccounts WITH (UPDLOCK,HOLDLOCK) WHERE AccountCode = @debitAccountCode AND IsActive = 1 ORDER BY LedgerAccountId;
    SELECT TOP (1) @creditLedgerAccountId = LedgerAccountId FROM dbo.LedgerAccounts WITH (UPDLOCK,HOLDLOCK) WHERE AccountCode = @creditAccountCode AND IsActive = 1 ORDER BY LedgerAccountId;
    IF @debitLedgerAccountId IS NULL OR @creditLedgerAccountId IS NULL
        THROW 51219, N''A required active ledger account is missing.'', 1;

    SET @ReferenceNumber = NULLIF(LTRIM(RTRIM(@ReferenceNumber)), N'''');
    SET @ReferenceNumber = COALESCE(@ReferenceNumber, CONCAT(N''AE-'', @accountingEventId));
    SET @Description = NULLIF(LTRIM(RTRIM(@Description)), N'''');
    SET @Description = COALESCE(@Description, CONCAT(N''Accounting event '', @accountingEventId));

    DECLARE @FinancialTransactionOutput TABLE (FinancialTransactionId int NOT NULL);
    DECLARE @JournalEntryOutput TABLE (JournalEntryId int NOT NULL);
    DECLARE @financialTransactionId int;
    INSERT INTO dbo.FinancialTransactions (ReferenceNumber, TransactionType, Amount, Description, CreatedAt, AccountingEventId)
    OUTPUT INSERTED.FinancialTransactionId INTO @FinancialTransactionOutput(FinancialTransactionId)
    VALUES (@ReferenceNumber, @transactionType, @PostingAmount, @Description, SYSUTCDATETIME(), @accountingEventId);
    SELECT @financialTransactionId = FinancialTransactionId FROM @FinancialTransactionOutput;
    DECLARE @journalEntryId int;
    INSERT INTO dbo.JournalEntries (ReferenceNumber, Description, EntryDate, CreatedAt, AccountingEventId)
    OUTPUT INSERTED.JournalEntryId INTO @JournalEntryOutput(JournalEntryId)
    VALUES (@ReferenceNumber, @Description, SYSUTCDATETIME(), SYSUTCDATETIME(), @accountingEventId);
    SELECT @journalEntryId = JournalEntryId FROM @JournalEntryOutput;
    INSERT INTO dbo.JournalEntryLines (JournalEntryId, LedgerAccountId, DebitAmount, CreditAmount, Description)
    VALUES (@journalEntryId, @debitLedgerAccountId, @PostingAmount, 0, @Description), (@journalEntryId, @creditLedgerAccountId, 0, @PostingAmount, @Description);
    IF (SELECT COUNT(*) FROM dbo.JournalEntryLines WHERE JournalEntryId = @journalEntryId) < 2 OR (SELECT SUM(DebitAmount) FROM dbo.JournalEntryLines WHERE JournalEntryId = @journalEntryId) <> @PostingAmount OR (SELECT SUM(CreditAmount) FROM dbo.JournalEntryLines WHERE JournalEntryId = @journalEntryId) <> @PostingAmount
        THROW 51220, N''The accounting journal is not balanced.'', 1;
    SELECT @accountingEventId AS AccountingEventId, @financialTransactionId AS FinancialTransactionId, @journalEntryId AS JournalEntryId, CAST(0 AS bit) AS IsExisting;
END;');

    EXEC(N'
        CREATE OR ALTER TRIGGER dbo.trg_CustomerAdvanceApplications_OfficialWriter
        ON dbo.CustomerAdvanceApplications
        AFTER INSERT, UPDATE, DELETE
        AS
        BEGIN
            SET NOCOUNT ON;
            IF TRY_CAST(SESSION_CONTEXT(N''CustomerAdvanceApplicationWriter'') AS int) <> 1
                THROW 51230, N''CustomerAdvanceApplications may only be written by the official delivery writer.'', 1;
        END;');

    DECLARE @cutoverUtc nvarchar(33) = CONVERT(nvarchar(33), SYSUTCDATETIME(), 126) + N'Z';
    EXEC sys.sp_addextendedproperty
        @name = N'CustomerAdvanceApplicationCutoverUtc',
        @value = @cutoverUtc,
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'TABLE', @level1name = N'CustomerAdvanceApplications';

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;