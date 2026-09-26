SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.AccountingEvents', N'U') IS NOT NULL
        THROW 51000, N'AccountingEvents already exists. This foundation migration must not be reapplied.', 1;

    IF COL_LENGTH(N'dbo.FinancialTransactions', N'AccountingEventId') IS NOT NULL
        THROW 51001, N'FinancialTransactions.AccountingEventId already exists.', 1;

    IF COL_LENGTH(N'dbo.JournalEntries', N'AccountingEventId') IS NOT NULL
        THROW 51002, N'JournalEntries.AccountingEventId already exists.', 1;

    CREATE TABLE dbo.AccountingEvents
    (
        AccountingEventId bigint IDENTITY(1,1) NOT NULL,
        AccountingEventType tinyint NOT NULL,
        PostingAmount decimal(18,2) NOT NULL,
        PaymentId int NULL,
        OrderId int NULL,
        MeasurementCardPrintHistoryId int NULL,
        ReadyMadeInventoryProductId int NULL,
        CreatedAt datetime2(7) NOT NULL
            CONSTRAINT DF_AccountingEvents_CreatedAt DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_AccountingEvents PRIMARY KEY (AccountingEventId),
        CONSTRAINT CK_AccountingEvents_Type CHECK (AccountingEventType IN (1,2,3,4,5)),
        CONSTRAINT CK_AccountingEvents_PostingAmount CHECK (PostingAmount > 0),
        CONSTRAINT CK_AccountingEvents_SourceCardinality CHECK
        (
               (AccountingEventType IN (1,2)
                AND PaymentId IS NOT NULL
                AND OrderId IS NULL
                AND MeasurementCardPrintHistoryId IS NULL
                AND ReadyMadeInventoryProductId IS NULL)
            OR (AccountingEventType = 3
                AND PaymentId IS NULL
                AND OrderId IS NOT NULL
                AND MeasurementCardPrintHistoryId IS NULL
                AND ReadyMadeInventoryProductId IS NULL)
            OR (AccountingEventType = 4
                AND PaymentId IS NULL
                AND OrderId IS NULL
                AND MeasurementCardPrintHistoryId IS NOT NULL
                AND ReadyMadeInventoryProductId IS NULL)
            OR (AccountingEventType = 5
                AND PaymentId IS NULL
                AND OrderId IS NULL
                AND MeasurementCardPrintHistoryId IS NULL
                AND ReadyMadeInventoryProductId IS NOT NULL)
        ),
        CONSTRAINT FK_AccountingEvents_Payments_PaymentId
            FOREIGN KEY (PaymentId) REFERENCES dbo.Payments(PaymentID),
        CONSTRAINT FK_AccountingEvents_Orders_OrderId
            FOREIGN KEY (OrderId) REFERENCES dbo.Orders(OrderID),
        CONSTRAINT FK_AccountingEvents_MeasurementCardPrintHistory_PrintHistoryId
            FOREIGN KEY (MeasurementCardPrintHistoryId)
            REFERENCES dbo.MeasurementCardPrintHistory(PrintHistoryId),
        CONSTRAINT FK_AccountingEvents_ReadyMadeInventoryProducts_ProductId
            FOREIGN KEY (ReadyMadeInventoryProductId)
            REFERENCES dbo.ReadyMadeInventoryProducts(ReadyMadeInventoryProductId)
    );

    ALTER TABLE dbo.FinancialTransactions ADD AccountingEventId bigint NULL;
    ALTER TABLE dbo.JournalEntries ADD AccountingEventId bigint NULL;

    EXEC(N'
        ALTER TABLE dbo.FinancialTransactions
            ADD CONSTRAINT FK_FinancialTransactions_AccountingEvents_AccountingEventId
            FOREIGN KEY (AccountingEventId) REFERENCES dbo.AccountingEvents(AccountingEventId);

        ALTER TABLE dbo.JournalEntries
            ADD CONSTRAINT FK_JournalEntries_AccountingEvents_AccountingEventId
            FOREIGN KEY (AccountingEventId) REFERENCES dbo.AccountingEvents(AccountingEventId);');

    CREATE UNIQUE INDEX UX_AccountingEvents_PaymentId
        ON dbo.AccountingEvents(PaymentId)
        WHERE PaymentId IS NOT NULL;

    CREATE UNIQUE INDEX UX_AccountingEvents_Order_RevenueRecognized
        ON dbo.AccountingEvents(OrderId)
        WHERE AccountingEventType = 3 AND OrderId IS NOT NULL;

    CREATE UNIQUE INDEX UX_AccountingEvents_PrintHistory_RevenueRecognized
        ON dbo.AccountingEvents(MeasurementCardPrintHistoryId)
        WHERE AccountingEventType = 4 AND MeasurementCardPrintHistoryId IS NOT NULL;

    CREATE UNIQUE INDEX UX_AccountingEvents_ReadyMadeInventoryProduct_WipToFinishedGoods
        ON dbo.AccountingEvents(ReadyMadeInventoryProductId)
        WHERE AccountingEventType = 5 AND ReadyMadeInventoryProductId IS NOT NULL;

    CREATE INDEX IX_AccountingEvents_Type_CreatedAt
        ON dbo.AccountingEvents(AccountingEventType, CreatedAt);

    EXEC(N'
        CREATE UNIQUE INDEX UX_FinancialTransactions_AccountingEventId
            ON dbo.FinancialTransactions(AccountingEventId)
            WHERE AccountingEventId IS NOT NULL;

        CREATE UNIQUE INDEX UX_JournalEntries_AccountingEventId
            ON dbo.JournalEntries(AccountingEventId)
            WHERE AccountingEventId IS NOT NULL;');

    DECLARE @procedureSql nvarchar(max) = N'
CREATE OR ALTER PROCEDURE dbo.usp_PostAccountingEvent
    @AccountingEventType tinyint,
    @PostingAmount decimal(18,2),
    @PaymentId int = NULL,
    @OrderId int = NULL,
    @MeasurementCardPrintHistoryId int = NULL,
    @ReadyMadeInventoryProductId int = NULL,
    @ReferenceNumber nvarchar(200) = NULL,
    @Description nvarchar(1000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @@TRANCOUNT = 0
        THROW 51010, N''Accounting events require a caller-owned SQL transaction.'', 1;

    IF @AccountingEventType NOT IN (1,2,3,4,5) OR @PostingAmount <= 0
        THROW 51011, N''Unsupported accounting event type or posting amount.'', 1;

    IF NOT
    (
           (@AccountingEventType IN (1,2) AND @PaymentId IS NOT NULL AND @OrderId IS NULL AND @MeasurementCardPrintHistoryId IS NULL AND @ReadyMadeInventoryProductId IS NULL)
        OR (@AccountingEventType = 3 AND @PaymentId IS NULL AND @OrderId IS NOT NULL AND @MeasurementCardPrintHistoryId IS NULL AND @ReadyMadeInventoryProductId IS NULL)
        OR (@AccountingEventType = 4 AND @PaymentId IS NULL AND @OrderId IS NULL AND @MeasurementCardPrintHistoryId IS NOT NULL AND @ReadyMadeInventoryProductId IS NULL)
        OR (@AccountingEventType = 5 AND @PaymentId IS NULL AND @OrderId IS NULL AND @MeasurementCardPrintHistoryId IS NULL AND @ReadyMadeInventoryProductId IS NOT NULL)
    )
        THROW 51012, N''The accounting event source does not match its event type.'', 1;

    DECLARE @existingAccountingEventId bigint;
    SELECT @existingAccountingEventId = AccountingEventId
    FROM dbo.AccountingEvents WITH (UPDLOCK, HOLDLOCK)
    WHERE (@AccountingEventType IN (1,2) AND PaymentId = @PaymentId)
       OR (@AccountingEventType = 3 AND OrderId = @OrderId)
       OR (@AccountingEventType = 4 AND MeasurementCardPrintHistoryId = @MeasurementCardPrintHistoryId)
       OR (@AccountingEventType = 5 AND ReadyMadeInventoryProductId = @ReadyMadeInventoryProductId);

    IF @existingAccountingEventId IS NOT NULL
    BEGIN
        DECLARE @existingFinancialTransactionId int;
        DECLARE @existingJournalEntryId int;

        SELECT @existingFinancialTransactionId = FinancialTransactionId
        FROM dbo.FinancialTransactions WITH (UPDLOCK, HOLDLOCK)
        WHERE AccountingEventId = @existingAccountingEventId;

        SELECT @existingJournalEntryId = JournalEntryId
        FROM dbo.JournalEntries WITH (UPDLOCK, HOLDLOCK)
        WHERE AccountingEventId = @existingAccountingEventId;

        IF @existingFinancialTransactionId IS NULL OR @existingJournalEntryId IS NULL
            THROW 51013, N''The existing accounting event is incomplete.'', 1;

        SELECT @existingAccountingEventId AS AccountingEventId,
               @existingFinancialTransactionId AS FinancialTransactionId,
               @existingJournalEntryId AS JournalEntryId,
               CAST(1 AS bit) AS IsExisting;
        RETURN;
    END;

    IF @AccountingEventType IN (1,2)
    BEGIN
        DECLARE @paymentAmount decimal(18,2);
        DECLARE @paymentKind nvarchar(100);
        SELECT @paymentAmount = Amount, @paymentKind = PaymentKind
        FROM dbo.Payments WITH (UPDLOCK, HOLDLOCK)
        WHERE PaymentID = @PaymentId;

        IF @paymentAmount IS NULL OR @paymentAmount <> @PostingAmount
            THROW 51014, N''The payment source is missing or has a different amount.'', 1;

        IF (@AccountingEventType = 1 AND @paymentKind <> N''Advance'')
           OR (@AccountingEventType = 2 AND @paymentKind NOT IN (N''DebtCollection'', N''SaleCash'', N''MeasurementCardPieceSale''))
            THROW 51015, N''The payment kind is not supported by this accounting event.'', 1;
    END;

    IF @AccountingEventType = 3
       AND NOT EXISTS (SELECT 1 FROM dbo.Orders WITH (UPDLOCK, HOLDLOCK) WHERE OrderID = @OrderId)
        THROW 51016, N''The revenue order source does not exist.'', 1;

    IF @AccountingEventType = 4
       AND NOT EXISTS
       (
           SELECT 1
           FROM dbo.MeasurementCardPrintHistory WITH (UPDLOCK, HOLDLOCK)
           WHERE PrintHistoryId = @MeasurementCardPrintHistoryId
             AND ReprintReasonCode = N''PieceSold''
             AND SaleAmount = @PostingAmount
       )
        THROW 51017, N''The print-sale source is missing or does not match the posting amount.'', 1;

    IF @AccountingEventType = 5
       AND NOT EXISTS
       (
           SELECT 1
           FROM dbo.ReadyMadeInventoryProducts WITH (UPDLOCK, HOLDLOCK)
           WHERE ReadyMadeInventoryProductId = @ReadyMadeInventoryProductId
             AND CAST(ActualCost AS decimal(18,2)) = @PostingAmount
       )
        THROW 51018, N''The ready-made inventory source is missing or does not match the posting amount.'', 1;

    INSERT INTO dbo.AccountingEvents
        (AccountingEventType, PostingAmount, PaymentId, OrderId, MeasurementCardPrintHistoryId, ReadyMadeInventoryProductId)
    VALUES
        (@AccountingEventType, @PostingAmount, @PaymentId, @OrderId, @MeasurementCardPrintHistoryId, @ReadyMadeInventoryProductId);

    DECLARE @accountingEventId bigint = SCOPE_IDENTITY();
    DECLARE @transactionType nvarchar(200) = CASE @AccountingEventType
        WHEN 1 THEN N''CustomerAdvance''
        WHEN 2 THEN N''CustomerPayment''
        WHEN 3 THEN N''RevenueRecognized''
        WHEN 4 THEN N''RevenueRecognized''
        WHEN 5 THEN N''WipToFinishedGoods''
    END;
    DECLARE @debitAccountCode nvarchar(100) = CASE @AccountingEventType
        WHEN 1 THEN N''1000'' WHEN 2 THEN N''1000'' WHEN 3 THEN N''1200'' WHEN 4 THEN N''1200'' WHEN 5 THEN N''1110'' END;
    DECLARE @creditAccountCode nvarchar(100) = CASE @AccountingEventType
        WHEN 1 THEN N''1160'' WHEN 2 THEN N''1200'' WHEN 3 THEN N''4200'' WHEN 4 THEN N''4200'' WHEN 5 THEN N''1130'' END;
    DECLARE @debitLedgerAccountId int;
    DECLARE @creditLedgerAccountId int;

    SELECT TOP (1) @debitLedgerAccountId = LedgerAccountId
    FROM dbo.LedgerAccounts WITH (UPDLOCK, HOLDLOCK)
    WHERE AccountCode = @debitAccountCode AND IsActive = 1
    ORDER BY LedgerAccountId;

    SELECT TOP (1) @creditLedgerAccountId = LedgerAccountId
    FROM dbo.LedgerAccounts WITH (UPDLOCK, HOLDLOCK)
    WHERE AccountCode = @creditAccountCode AND IsActive = 1
    ORDER BY LedgerAccountId;

    IF @debitLedgerAccountId IS NULL OR @creditLedgerAccountId IS NULL
        THROW 51019, N''A required active ledger account is missing.'', 1;

    SET @ReferenceNumber = NULLIF(LTRIM(RTRIM(@ReferenceNumber)), N'''');
    SET @ReferenceNumber = COALESCE(@ReferenceNumber, CONCAT(N''AE-'', @accountingEventId));
    SET @Description = NULLIF(LTRIM(RTRIM(@Description)), N'''');
    SET @Description = COALESCE(@Description, CONCAT(N''Accounting event '', @accountingEventId));

    DECLARE @FinancialTransactionOutput TABLE (FinancialTransactionId int NOT NULL);
    DECLARE @JournalEntryOutput TABLE (JournalEntryId int NOT NULL);
    DECLARE @financialTransactionId int;
    INSERT INTO dbo.FinancialTransactions
        (ReferenceNumber, TransactionType, Amount, Description, CreatedAt, AccountingEventId)
    OUTPUT INSERTED.FinancialTransactionId INTO @FinancialTransactionOutput(FinancialTransactionId)
    VALUES
        (@ReferenceNumber, @transactionType, @PostingAmount, @Description, SYSUTCDATETIME(), @accountingEventId);

    SELECT @financialTransactionId = FinancialTransactionId FROM @FinancialTransactionOutput;

    DECLARE @journalEntryId int;
    INSERT INTO dbo.JournalEntries
        (ReferenceNumber, Description, EntryDate, CreatedAt, AccountingEventId)
    OUTPUT INSERTED.JournalEntryId INTO @JournalEntryOutput(JournalEntryId)
    VALUES
        (@ReferenceNumber, @Description, SYSUTCDATETIME(), SYSUTCDATETIME(), @accountingEventId);

    SELECT @journalEntryId = JournalEntryId FROM @JournalEntryOutput;

    INSERT INTO dbo.JournalEntryLines (JournalEntryId, LedgerAccountId, DebitAmount, CreditAmount, Description)
    VALUES
        (@journalEntryId, @debitLedgerAccountId, @PostingAmount, 0, @Description),
        (@journalEntryId, @creditLedgerAccountId, 0, @PostingAmount, @Description);

    IF (SELECT COUNT(*) FROM dbo.JournalEntryLines WHERE JournalEntryId = @journalEntryId) < 2
       OR (SELECT SUM(DebitAmount) FROM dbo.JournalEntryLines WHERE JournalEntryId = @journalEntryId) <> @PostingAmount
       OR (SELECT SUM(CreditAmount) FROM dbo.JournalEntryLines WHERE JournalEntryId = @journalEntryId) <> @PostingAmount
        THROW 51020, N''The accounting journal is not balanced.'', 1;

    SELECT @accountingEventId AS AccountingEventId,
           @financialTransactionId AS FinancialTransactionId,
           @journalEntryId AS JournalEntryId,
           CAST(0 AS bit) AS IsExisting;
END;';

    EXEC sys.sp_executesql @procedureSql;

    IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'db_lumar_accounting_event_writer' AND type = 'R')
        EXEC(N'CREATE ROLE db_lumar_accounting_event_writer');

    GRANT EXECUTE ON dbo.usp_PostAccountingEvent TO db_lumar_accounting_event_writer;

    DECLARE @cutoverUtc nvarchar(33) = CONVERT(nvarchar(33), SYSUTCDATETIME(), 126) + N'Z';
    EXEC sys.sp_addextendedproperty
        @name = N'AccountingEventFoundationCutoverUtc',
        @value = @cutoverUtc,
        @level0type = N'SCHEMA', @level0name = N'dbo',
        @level1type = N'TABLE', @level1name = N'AccountingEvents';

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;