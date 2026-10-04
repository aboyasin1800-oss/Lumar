SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE dbo.usp_PostAccountingEvent
    @AccountingEventType tinyint,
    @PostingAmount decimal(18,2),
    @PaymentId int = NULL,
    @OrderId int = NULL,
    @MeasurementCardPrintHistoryId int = NULL,
    @ReadyMadeInventoryProductId int = NULL,
    @CustomerAdvanceApplicationId bigint = NULL,
    @InventoryReceiptPostingId bigint = NULL,
    @FabricConsumptionSourceId bigint = NULL,
    @ProductionMaterialConsumptionId int = NULL,
    @ReadyMadeSaleCostPostingId bigint = NULL,
    @ImportedReadyMadeInventoryReceiptId bigint = NULL,
    @ImportedReadyMadeSaleCostPostingId bigint = NULL,
    @ToolIssuanceId bigint = NULL,
    @ToolIssuanceReversalId bigint = NULL,
    @ReferenceNumber nvarchar(200) = NULL,
    @Description nvarchar(1000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @@TRANCOUNT = 0
        THROW 51510, N'Accounting events require a caller-owned SQL transaction.', 1;

    IF @AccountingEventType NOT BETWEEN 1 AND 15 OR @PostingAmount <= 0
        THROW 51511, N'Unsupported accounting event type or posting amount.', 1;

    IF NOT
    (
           (@AccountingEventType IN (1,2) AND @PaymentId IS NOT NULL AND @OrderId IS NULL AND @MeasurementCardPrintHistoryId IS NULL AND @ReadyMadeInventoryProductId IS NULL AND @CustomerAdvanceApplicationId IS NULL AND @InventoryReceiptPostingId IS NULL AND @FabricConsumptionSourceId IS NULL AND @ProductionMaterialConsumptionId IS NULL AND @ReadyMadeSaleCostPostingId IS NULL AND @ImportedReadyMadeInventoryReceiptId IS NULL AND @ImportedReadyMadeSaleCostPostingId IS NULL AND @ToolIssuanceId IS NULL AND @ToolIssuanceReversalId IS NULL)
        OR (@AccountingEventType = 3 AND @PaymentId IS NULL AND @OrderId IS NOT NULL AND @MeasurementCardPrintHistoryId IS NULL AND @ReadyMadeInventoryProductId IS NULL AND @CustomerAdvanceApplicationId IS NULL AND @InventoryReceiptPostingId IS NULL AND @FabricConsumptionSourceId IS NULL AND @ProductionMaterialConsumptionId IS NULL AND @ReadyMadeSaleCostPostingId IS NULL AND @ImportedReadyMadeInventoryReceiptId IS NULL AND @ImportedReadyMadeSaleCostPostingId IS NULL AND @ToolIssuanceId IS NULL AND @ToolIssuanceReversalId IS NULL)
        OR (@AccountingEventType = 4 AND @PaymentId IS NULL AND @OrderId IS NULL AND @MeasurementCardPrintHistoryId IS NOT NULL AND @ReadyMadeInventoryProductId IS NULL AND @CustomerAdvanceApplicationId IS NULL AND @InventoryReceiptPostingId IS NULL AND @FabricConsumptionSourceId IS NULL AND @ProductionMaterialConsumptionId IS NULL AND @ReadyMadeSaleCostPostingId IS NULL AND @ImportedReadyMadeInventoryReceiptId IS NULL AND @ImportedReadyMadeSaleCostPostingId IS NULL AND @ToolIssuanceId IS NULL AND @ToolIssuanceReversalId IS NULL)
        OR (@AccountingEventType = 5 AND @PaymentId IS NULL AND @OrderId IS NULL AND @MeasurementCardPrintHistoryId IS NULL AND @ReadyMadeInventoryProductId IS NOT NULL AND @CustomerAdvanceApplicationId IS NULL AND @InventoryReceiptPostingId IS NULL AND @FabricConsumptionSourceId IS NULL AND @ProductionMaterialConsumptionId IS NULL AND @ReadyMadeSaleCostPostingId IS NULL AND @ImportedReadyMadeInventoryReceiptId IS NULL AND @ImportedReadyMadeSaleCostPostingId IS NULL AND @ToolIssuanceId IS NULL AND @ToolIssuanceReversalId IS NULL)
        OR (@AccountingEventType = 6 AND @PaymentId IS NULL AND @OrderId IS NULL AND @MeasurementCardPrintHistoryId IS NULL AND @ReadyMadeInventoryProductId IS NULL AND @CustomerAdvanceApplicationId IS NOT NULL AND @InventoryReceiptPostingId IS NULL AND @FabricConsumptionSourceId IS NULL AND @ProductionMaterialConsumptionId IS NULL AND @ReadyMadeSaleCostPostingId IS NULL AND @ImportedReadyMadeInventoryReceiptId IS NULL AND @ImportedReadyMadeSaleCostPostingId IS NULL AND @ToolIssuanceId IS NULL AND @ToolIssuanceReversalId IS NULL)
        OR (@AccountingEventType IN (7,9) AND @PaymentId IS NULL AND @OrderId IS NULL AND @MeasurementCardPrintHistoryId IS NULL AND @ReadyMadeInventoryProductId IS NULL AND @CustomerAdvanceApplicationId IS NULL AND @InventoryReceiptPostingId IS NOT NULL AND @FabricConsumptionSourceId IS NULL AND @ProductionMaterialConsumptionId IS NULL AND @ReadyMadeSaleCostPostingId IS NULL AND @ImportedReadyMadeInventoryReceiptId IS NULL AND @ImportedReadyMadeSaleCostPostingId IS NULL AND @ToolIssuanceId IS NULL AND @ToolIssuanceReversalId IS NULL)
        OR (@AccountingEventType = 8 AND @PaymentId IS NULL AND @OrderId IS NULL AND @MeasurementCardPrintHistoryId IS NULL AND @ReadyMadeInventoryProductId IS NULL AND @CustomerAdvanceApplicationId IS NULL AND @InventoryReceiptPostingId IS NULL AND @FabricConsumptionSourceId IS NOT NULL AND @ProductionMaterialConsumptionId IS NULL AND @ReadyMadeSaleCostPostingId IS NULL AND @ImportedReadyMadeInventoryReceiptId IS NULL AND @ImportedReadyMadeSaleCostPostingId IS NULL AND @ToolIssuanceId IS NULL AND @ToolIssuanceReversalId IS NULL)
        OR (@AccountingEventType = 10 AND @PaymentId IS NULL AND @OrderId IS NULL AND @MeasurementCardPrintHistoryId IS NULL AND @ReadyMadeInventoryProductId IS NULL AND @CustomerAdvanceApplicationId IS NULL AND @InventoryReceiptPostingId IS NULL AND @FabricConsumptionSourceId IS NULL AND @ProductionMaterialConsumptionId IS NOT NULL AND @ReadyMadeSaleCostPostingId IS NULL AND @ImportedReadyMadeInventoryReceiptId IS NULL AND @ImportedReadyMadeSaleCostPostingId IS NULL AND @ToolIssuanceId IS NULL AND @ToolIssuanceReversalId IS NULL)
        OR (@AccountingEventType = 11 AND @PaymentId IS NULL AND @OrderId IS NULL AND @MeasurementCardPrintHistoryId IS NULL AND @ReadyMadeInventoryProductId IS NULL AND @CustomerAdvanceApplicationId IS NULL AND @InventoryReceiptPostingId IS NULL AND @FabricConsumptionSourceId IS NULL AND @ProductionMaterialConsumptionId IS NULL AND @ReadyMadeSaleCostPostingId IS NOT NULL AND @ImportedReadyMadeInventoryReceiptId IS NULL AND @ImportedReadyMadeSaleCostPostingId IS NULL AND @ToolIssuanceId IS NULL AND @ToolIssuanceReversalId IS NULL)
        OR (@AccountingEventType = 12 AND @PaymentId IS NULL AND @OrderId IS NULL AND @MeasurementCardPrintHistoryId IS NULL AND @ReadyMadeInventoryProductId IS NULL AND @CustomerAdvanceApplicationId IS NULL AND @InventoryReceiptPostingId IS NULL AND @FabricConsumptionSourceId IS NULL AND @ProductionMaterialConsumptionId IS NULL AND @ReadyMadeSaleCostPostingId IS NULL AND @ImportedReadyMadeInventoryReceiptId IS NOT NULL AND @ImportedReadyMadeSaleCostPostingId IS NULL AND @ToolIssuanceId IS NULL AND @ToolIssuanceReversalId IS NULL)
        OR (@AccountingEventType = 13 AND @PaymentId IS NULL AND @OrderId IS NULL AND @MeasurementCardPrintHistoryId IS NULL AND @ReadyMadeInventoryProductId IS NULL AND @CustomerAdvanceApplicationId IS NULL AND @InventoryReceiptPostingId IS NULL AND @FabricConsumptionSourceId IS NULL AND @ProductionMaterialConsumptionId IS NULL AND @ReadyMadeSaleCostPostingId IS NULL AND @ImportedReadyMadeInventoryReceiptId IS NULL AND @ImportedReadyMadeSaleCostPostingId IS NOT NULL AND @ToolIssuanceId IS NULL AND @ToolIssuanceReversalId IS NULL)
        OR (@AccountingEventType = 14 AND @PaymentId IS NULL AND @OrderId IS NULL AND @MeasurementCardPrintHistoryId IS NULL AND @ReadyMadeInventoryProductId IS NULL AND @CustomerAdvanceApplicationId IS NULL AND @InventoryReceiptPostingId IS NULL AND @FabricConsumptionSourceId IS NULL AND @ProductionMaterialConsumptionId IS NULL AND @ReadyMadeSaleCostPostingId IS NULL AND @ImportedReadyMadeInventoryReceiptId IS NULL AND @ImportedReadyMadeSaleCostPostingId IS NULL AND @ToolIssuanceId IS NOT NULL AND @ToolIssuanceReversalId IS NULL)
        OR (@AccountingEventType = 15 AND @PaymentId IS NULL AND @OrderId IS NULL AND @MeasurementCardPrintHistoryId IS NULL AND @ReadyMadeInventoryProductId IS NULL AND @CustomerAdvanceApplicationId IS NULL AND @InventoryReceiptPostingId IS NULL AND @FabricConsumptionSourceId IS NULL AND @ProductionMaterialConsumptionId IS NULL AND @ReadyMadeSaleCostPostingId IS NULL AND @ImportedReadyMadeInventoryReceiptId IS NULL AND @ImportedReadyMadeSaleCostPostingId IS NULL AND @ToolIssuanceId IS NULL AND @ToolIssuanceReversalId IS NOT NULL)
    )
        THROW 51512, N'The accounting event source does not match its event type.', 1;

    DECLARE @existingAccountingEventId bigint;
    DECLARE @existingPostingAmount decimal(18,2);
    SELECT TOP (1) @existingAccountingEventId = AccountingEventId, @existingPostingAmount = PostingAmount
    FROM dbo.AccountingEvents
    WHERE (@AccountingEventType IN (1,2) AND PaymentId = @PaymentId)
       OR (@AccountingEventType = 3 AND OrderId = @OrderId)
       OR (@AccountingEventType = 4 AND MeasurementCardPrintHistoryId = @MeasurementCardPrintHistoryId)
       OR (@AccountingEventType = 5 AND ReadyMadeInventoryProductId = @ReadyMadeInventoryProductId)
       OR (@AccountingEventType = 6 AND CustomerAdvanceApplicationId = @CustomerAdvanceApplicationId)
       OR (@AccountingEventType IN (7,9) AND InventoryReceiptPostingId = @InventoryReceiptPostingId)
       OR (@AccountingEventType = 8 AND FabricConsumptionSourceId = @FabricConsumptionSourceId)
       OR (@AccountingEventType = 10 AND ProductionMaterialConsumptionId = @ProductionMaterialConsumptionId)
       OR (@AccountingEventType = 11 AND ReadyMadeSaleCostPostingId = @ReadyMadeSaleCostPostingId)
       OR (@AccountingEventType = 12 AND ImportedReadyMadeInventoryReceiptId = @ImportedReadyMadeInventoryReceiptId)
       OR (@AccountingEventType = 13 AND ImportedReadyMadeSaleCostPostingId = @ImportedReadyMadeSaleCostPostingId)
       OR (@AccountingEventType = 14 AND ToolIssuanceId = @ToolIssuanceId)
       OR (@AccountingEventType = 15 AND ToolIssuanceReversalId = @ToolIssuanceReversalId);

    IF @existingAccountingEventId IS NOT NULL
    BEGIN
        IF @existingPostingAmount <> @PostingAmount
            THROW 51513, N'The accounting event source already has a different posting amount.', 1;
        SELECT @existingAccountingEventId AS AccountingEventId,
               (SELECT FinancialTransactionId FROM dbo.FinancialTransactions WHERE AccountingEventId = @existingAccountingEventId) AS FinancialTransactionId,
               (SELECT JournalEntryId FROM dbo.JournalEntries WHERE AccountingEventId = @existingAccountingEventId) AS JournalEntryId,
               CAST(1 AS bit) AS IsExisting;
        RETURN;
    END;

    DECLARE @debitAccountCode nvarchar(20);
    DECLARE @creditAccountCode nvarchar(20);
    DECLARE @transactionType nvarchar(100);
    DECLARE @sourcePostingAmount decimal(18,2);
    DECLARE @sourceOperationalAmount decimal(18,6);
    DECLARE @sourceOpposingLedgerAccountId int;

    IF @AccountingEventType IN (7,9)
    BEGIN
        SELECT
            @sourcePostingAmount = p.PostingAmount,
            @sourceOperationalAmount = p.OperationalAmount,
            @creditAccountCode = la.AccountCode
        FROM dbo.InventoryReceiptPostings p WITH (UPDLOCK, HOLDLOCK)
        INNER JOIN dbo.LedgerAccounts la WITH (UPDLOCK, HOLDLOCK)
            ON la.LedgerAccountId = p.OpposingLedgerAccountId
           AND la.IsActive = 1
        WHERE p.InventoryReceiptPostingId = @InventoryReceiptPostingId;
        IF @sourcePostingAmount IS NULL
            THROW 51514, N'The inventory receipt source is missing or its opposing account is inactive.', 1;
        IF @sourcePostingAmount <> @PostingAmount
           OR CONVERT(decimal(18,2), ROUND(@sourceOperationalAmount, 2)) <> @PostingAmount
            THROW 51515, N'The inventory receipt amount does not match.', 1;
        IF (@AccountingEventType = 7 AND @creditAccountCode NOT IN (N'2100', N'1101'))
           OR (@AccountingEventType = 9 AND @creditAccountCode <> N'1102')
            THROW 51516, N'The inventory receipt opposing account is not approved for this event.', 1;
        SET @debitAccountCode = CASE @AccountingEventType WHEN 7 THEN N'1101' ELSE N'1102' END;
        SET @transactionType = CASE @AccountingEventType WHEN 7 THEN N'FabricInventoryReceived' ELSE N'ConsumableInventoryReceived' END;
    END;
    ELSE IF @AccountingEventType = 8
    BEGIN
        SELECT @sourcePostingAmount = PostingAmount, @sourceOperationalAmount = OperationalAmount
        FROM dbo.FabricConsumptionSources WITH (UPDLOCK, HOLDLOCK)
        WHERE FabricConsumptionSourceId = @FabricConsumptionSourceId;
        IF @sourcePostingAmount IS NULL
            THROW 51517, N'The fabric consumption source is missing.', 1;
        IF @sourcePostingAmount <> @PostingAmount
           OR CONVERT(decimal(18,2), ROUND(@sourceOperationalAmount, 2)) <> @PostingAmount
            THROW 51518, N'The fabric consumption amount does not match.', 1;
        SET @transactionType = N'FabricInventoryConsumed';
        SET @debitAccountCode = N'1130';
        SET @creditAccountCode = N'1101';
    END;
    ELSE IF @AccountingEventType = 10
    BEGIN
        SELECT @sourcePostingAmount = PostingAmount, @sourceOperationalAmount = OperationalAmount
        FROM dbo.ProductionMaterialConsumptions WITH (UPDLOCK, HOLDLOCK)
        WHERE ProductionMaterialConsumptionId = @ProductionMaterialConsumptionId;
        IF @sourcePostingAmount IS NULL
            THROW 51519, N'The consumable consumption source is missing.', 1;
        IF @sourcePostingAmount <> @PostingAmount
           OR CONVERT(decimal(18,2), ROUND(@sourceOperationalAmount, 2)) <> @PostingAmount
            THROW 51520, N'The consumable consumption amount does not match.', 1;
        SET @transactionType = N'ConsumableInventoryConsumed';
        SET @debitAccountCode = N'5300';
        SET @creditAccountCode = N'1102';
    END;
    ELSE IF @AccountingEventType = 12
    BEGIN
        SELECT
            @sourcePostingAmount = r.PostingAmount,
            @sourceOperationalAmount = r.OperationalAmount,
            @sourceOpposingLedgerAccountId = r.OpposingLedgerAccountId,
            @creditAccountCode = la.AccountCode
        FROM dbo.ImportedReadyMadeInventoryReceipts r WITH (UPDLOCK, HOLDLOCK)
        INNER JOIN dbo.LedgerAccounts la WITH (UPDLOCK, HOLDLOCK)
            ON la.LedgerAccountId = r.OpposingLedgerAccountId
           AND la.IsActive = 1
        WHERE r.ImportedReadyMadeInventoryReceiptId = @ImportedReadyMadeInventoryReceiptId;
        IF @sourcePostingAmount IS NULL
            THROW 51521, N'The imported ready-made receipt source is missing or inactive.', 1;
        IF @sourcePostingAmount <> @PostingAmount
           OR CONVERT(decimal(18,2), ROUND(@sourceOperationalAmount, 2)) <> @PostingAmount
            THROW 51522, N'The imported ready-made receipt amount does not match.', 1;
        SET @transactionType = N'ImportedReadyMadeInventoryReceived';
        SET @debitAccountCode = N'1103';
    END;
    ELSE IF @AccountingEventType = 11
    BEGIN
        SELECT @sourcePostingAmount = PostingAmount, @sourceOperationalAmount = OperationalAmount
        FROM dbo.ReadyMadeSaleCostPostings WITH (UPDLOCK, HOLDLOCK)
        WHERE ReadyMadeSaleCostPostingId = @ReadyMadeSaleCostPostingId;
        IF @sourcePostingAmount IS NULL
            THROW 51523, N'The ready-made sale cost source is missing.', 1;
        IF @sourcePostingAmount <> @PostingAmount
           OR CONVERT(decimal(18,2), ROUND(@sourceOperationalAmount, 2)) <> @PostingAmount
            THROW 51524, N'The ready-made sale cost amount does not match.', 1;
        SET @transactionType = N'ReadyMadeCost';
        SET @debitAccountCode = N'5200';
        SET @creditAccountCode = N'1110';
    END;
    ELSE IF @AccountingEventType = 13
    BEGIN
        SELECT @sourcePostingAmount = PostingAmount, @sourceOperationalAmount = OperationalAmount
        FROM dbo.ImportedReadyMadeSaleCostPostings WITH (UPDLOCK, HOLDLOCK)
        WHERE ImportedReadyMadeSaleCostPostingId = @ImportedReadyMadeSaleCostPostingId;
        IF @sourcePostingAmount IS NULL
            THROW 51525, N'The imported ready-made sale cost source is missing.', 1;
        IF @sourcePostingAmount <> @PostingAmount
           OR CONVERT(decimal(18,2), ROUND(@sourceOperationalAmount, 2)) <> @PostingAmount
            THROW 51526, N'The imported ready-made sale cost amount does not match.', 1;
        SET @transactionType = N'ImportedReadyMadeCost';
        SET @debitAccountCode = N'5200';
        SET @creditAccountCode = N'1103';
    END;
    ELSE IF @AccountingEventType = 14
    BEGIN
        SELECT
            @sourcePostingAmount = PostingAmount,
            @sourceOperationalAmount = OperationalAmount
        FROM dbo.ToolIssuances WITH (UPDLOCK, HOLDLOCK)
        WHERE ToolIssuanceId = @ToolIssuanceId
          AND IssueType = N'Operational';
        IF @sourcePostingAmount IS NULL
            THROW 51527, N'The tool operational issuance source is missing.', 1;
        IF @sourcePostingAmount <> @PostingAmount
           OR CONVERT(decimal(18,2), ROUND(@sourceOperationalAmount, 2)) <> @PostingAmount
            THROW 51528, N'The tool operational issuance amount does not match.', 1;
        SET @transactionType = N'ToolOperationalIssue';
        SET @debitAccountCode = N'5300';
        SET @creditAccountCode = N'1102';
    END;
    ELSE IF @AccountingEventType = 15
    BEGIN
        SELECT
            @sourcePostingAmount = r.PostingAmount,
            @sourceOperationalAmount = i.OperationalAmount
        FROM dbo.ToolIssuanceReversals r WITH (UPDLOCK, HOLDLOCK)
        INNER JOIN dbo.ToolIssuances i ON i.ToolIssuanceId = r.ToolIssuanceId
        WHERE r.ToolIssuanceReversalId = @ToolIssuanceReversalId;
        IF @sourcePostingAmount IS NULL
            THROW 51529, N'The tool operational reversal source is missing.', 1;
        IF @sourcePostingAmount <> @PostingAmount
           OR CONVERT(decimal(18,2), ROUND(@sourceOperationalAmount, 2)) <> @PostingAmount
            THROW 51530, N'The tool operational reversal amount does not match.', 1;
        SET @transactionType = N'ToolOperationalReversal';
        SET @debitAccountCode = N'1102';
        SET @creditAccountCode = N'5300';
    END;
    ELSE
    BEGIN
        SET @transactionType = CASE @AccountingEventType
            WHEN 1 THEN N'CustomerAdvance'
            WHEN 2 THEN N'CustomerPayment'
            WHEN 3 THEN N'RevenueRecognized'
            WHEN 4 THEN N'RevenueRecognized'
            WHEN 5 THEN N'WipToFinishedGoods'
            WHEN 6 THEN N'CustomerAdvanceApplied'
        END;
        SET @debitAccountCode = CASE @AccountingEventType
            WHEN 1 THEN N'1000'
            WHEN 2 THEN N'1000'
            WHEN 3 THEN N'1200'
            WHEN 4 THEN N'1200'
            WHEN 5 THEN N'1110'
            WHEN 6 THEN N'1160'
        END;
        SET @creditAccountCode = CASE @AccountingEventType
            WHEN 1 THEN N'1160'
            WHEN 2 THEN N'1200'
            WHEN 3 THEN N'4200'
            WHEN 4 THEN N'4200'
            WHEN 5 THEN N'1130'
            WHEN 6 THEN N'1200'
        END;
    END;

    IF @AccountingEventType IN (1,2)
    BEGIN
        DECLARE @paymentAmount decimal(18,2);
        DECLARE @paymentKind nvarchar(100);
        SELECT @paymentAmount = Amount, @paymentKind = PaymentKind
        FROM dbo.Payments WITH (UPDLOCK, HOLDLOCK)
        WHERE PaymentID = @PaymentId;
        IF @paymentAmount IS NULL OR @paymentAmount <> @PostingAmount
            THROW 51531, N'The payment source is missing or has a different amount.', 1;
        IF (@AccountingEventType = 1 AND @paymentKind <> N'Advance')
           OR (@AccountingEventType = 2 AND @paymentKind NOT IN (N'DebtCollection', N'SaleCash', N'MeasurementCardPieceSale'))
            THROW 51532, N'The payment kind is not supported for this accounting event.', 1;
    END;

    IF @AccountingEventType = 3
       AND NOT EXISTS (SELECT 1 FROM dbo.Orders WITH (UPDLOCK, HOLDLOCK) WHERE OrderID = @OrderId)
        THROW 51533, N'The revenue order source does not exist.', 1;

    IF @AccountingEventType = 4
       AND NOT EXISTS
       (
           SELECT 1 FROM dbo.MeasurementCardPrintHistory WITH (UPDLOCK, HOLDLOCK)
           WHERE PrintHistoryId = @MeasurementCardPrintHistoryId
             AND ReprintReasonCode = N'PieceSold'
             AND SaleAmount = @PostingAmount
       )
        THROW 51534, N'The print-sale source is missing or does not match the posting amount.', 1;

    IF @AccountingEventType = 5
       AND NOT EXISTS
       (
           SELECT 1 FROM dbo.ReadyMadeInventoryProducts WITH (UPDLOCK, HOLDLOCK)
           WHERE ReadyMadeInventoryProductId = @ReadyMadeInventoryProductId
             AND CAST(ActualCost AS decimal(18,2)) = @PostingAmount
       )
        THROW 51535, N'The ready-made inventory source is missing or does not match the posting amount.', 1;

    IF @AccountingEventType = 11
       AND NOT EXISTS
       (
           SELECT 1
           FROM dbo.ReadyMadeSaleCostPostings s WITH (UPDLOCK, HOLDLOCK)
           INNER JOIN dbo.OrderItems oi WITH (UPDLOCK, HOLDLOCK) ON oi.OrderItemID = s.OrderItemId AND oi.OrderID = s.OrderId
           INNER JOIN dbo.ReadyMadeInventoryProducts rip WITH (UPDLOCK, HOLDLOCK) ON rip.ReadyMadeInventoryProductId = s.ReadyMadeInventoryProductId
           WHERE s.ReadyMadeSaleCostPostingId = @ReadyMadeSaleCostPostingId
             AND oi.ReadyMadeInventoryProductId = s.ReadyMadeInventoryProductId
             AND rip.ActualCost = s.OfficialUnitCost
       )
        THROW 51536, N'The ready-made sale cost source identity is invalid.', 1;

    IF @AccountingEventType = 12
       AND NOT EXISTS
       (
           SELECT 1 FROM dbo.ImportedReadyMadeInventoryReceipts r WITH (UPDLOCK, HOLDLOCK)
           INNER JOIN dbo.ImportedReadyMadeProducts p WITH (UPDLOCK, HOLDLOCK) ON p.ImportedReadyMadeProductId = r.ImportedReadyMadeProductId
           WHERE r.ImportedReadyMadeInventoryReceiptId = @ImportedReadyMadeInventoryReceiptId
       )
        THROW 51537, N'The imported ready-made receipt source identity is invalid.', 1;

    IF @AccountingEventType = 13
       AND NOT EXISTS
       (
           SELECT 1
           FROM dbo.ImportedReadyMadeSaleCostPostings s WITH (UPDLOCK, HOLDLOCK)
           INNER JOIN dbo.OrderItems oi WITH (UPDLOCK, HOLDLOCK) ON oi.OrderItemID = s.OrderItemId AND oi.OrderID = s.OrderId
           INNER JOIN dbo.ImportedReadyMadeProducts p WITH (UPDLOCK, HOLDLOCK) ON p.ImportedReadyMadeProductId = s.ImportedReadyMadeProductId
           WHERE s.ImportedReadyMadeSaleCostPostingId = @ImportedReadyMadeSaleCostPostingId
             AND oi.ImportedReadyMadeProductId = s.ImportedReadyMadeProductId
             AND p.PurchasePrice = s.OfficialUnitCost
       )
        THROW 51538, N'The imported ready-made sale cost source identity is invalid.', 1;

    IF @AccountingEventType = 14
       AND NOT EXISTS
       (
           SELECT 1 FROM dbo.ToolIssuances t WITH (UPDLOCK, HOLDLOCK)
           INNER JOIN dbo.InventoryItems i WITH (UPDLOCK, HOLDLOCK) ON i.InventoryItemID = t.InventoryItemId
           WHERE t.ToolIssuanceId = @ToolIssuanceId
             AND t.IssueType = N'Operational'
             AND i.IsActive = 1
       )
        THROW 51539, N'The tool operational issuance source identity is invalid.', 1;

    IF @AccountingEventType = 15
       AND NOT EXISTS
       (
           SELECT 1 FROM dbo.ToolIssuanceReversals r WITH (UPDLOCK, HOLDLOCK)
           INNER JOIN dbo.ToolIssuances t WITH (UPDLOCK, HOLDLOCK) ON t.ToolIssuanceId = r.ToolIssuanceId
           WHERE r.ToolIssuanceReversalId = @ToolIssuanceReversalId
             AND t.IssueType = N'Operational'
       )
        THROW 51540, N'The tool operational reversal source identity is invalid.', 1;

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
        THROW 51541, N'A required active ledger account is missing.', 1;

    INSERT INTO dbo.AccountingEvents
    (
        AccountingEventType,
        PostingAmount,
        PaymentId,
        OrderId,
        MeasurementCardPrintHistoryId,
        ReadyMadeInventoryProductId,
        CustomerAdvanceApplicationId,
        InventoryReceiptPostingId,
        FabricConsumptionSourceId,
        ProductionMaterialConsumptionId,
        ReadyMadeSaleCostPostingId,
        ImportedReadyMadeInventoryReceiptId,
        ImportedReadyMadeSaleCostPostingId,
        ToolIssuanceId,
        ToolIssuanceReversalId
    )
    VALUES
    (
        @AccountingEventType,
        @PostingAmount,
        @PaymentId,
        @OrderId,
        @MeasurementCardPrintHistoryId,
        @ReadyMadeInventoryProductId,
        @CustomerAdvanceApplicationId,
        @InventoryReceiptPostingId,
        @FabricConsumptionSourceId,
        @ProductionMaterialConsumptionId,
        @ReadyMadeSaleCostPostingId,
        @ImportedReadyMadeInventoryReceiptId,
        @ImportedReadyMadeSaleCostPostingId,
        @ToolIssuanceId,
        @ToolIssuanceReversalId
    );

    DECLARE @accountingEventId bigint = SCOPE_IDENTITY();
    SET @ReferenceNumber = NULLIF(LTRIM(RTRIM(@ReferenceNumber)), N'');
    SET @ReferenceNumber = COALESCE(@ReferenceNumber, CONCAT(N'AE-', @accountingEventId));
    SET @Description = NULLIF(LTRIM(RTRIM(@Description)), N'');
    SET @Description = COALESCE(@Description, CONCAT(N'Accounting event ', @accountingEventId));

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
        THROW 51542, N'The accounting journal is not balanced.', 1;

    IF @AccountingEventType IN (7,9)
        UPDATE dbo.InventoryReceiptPostings SET AccountingEventId = @accountingEventId WHERE InventoryReceiptPostingId = @InventoryReceiptPostingId;
    ELSE IF @AccountingEventType = 8
        UPDATE dbo.FabricConsumptionSources SET AccountingEventId = @accountingEventId WHERE FabricConsumptionSourceId = @FabricConsumptionSourceId;
    ELSE IF @AccountingEventType = 10
        UPDATE dbo.ProductionMaterialConsumptions SET AccountingEventId = @accountingEventId WHERE ProductionMaterialConsumptionId = @ProductionMaterialConsumptionId;
    ELSE IF @AccountingEventType = 11
        UPDATE dbo.ReadyMadeSaleCostPostings SET AccountingEventId = @accountingEventId WHERE ReadyMadeSaleCostPostingId = @ReadyMadeSaleCostPostingId;
    ELSE IF @AccountingEventType = 12
        UPDATE dbo.ImportedReadyMadeInventoryReceipts SET AccountingEventId = @accountingEventId WHERE ImportedReadyMadeInventoryReceiptId = @ImportedReadyMadeInventoryReceiptId;
    ELSE IF @AccountingEventType = 13
        UPDATE dbo.ImportedReadyMadeSaleCostPostings SET AccountingEventId = @accountingEventId WHERE ImportedReadyMadeSaleCostPostingId = @ImportedReadyMadeSaleCostPostingId;
    ELSE IF @AccountingEventType = 14
    BEGIN
        UPDATE dbo.ToolIssuances
        SET AccountingEventId = @accountingEventId,
            Status = N'Posted'
        WHERE ToolIssuanceId = @ToolIssuanceId
          AND Status = N'PendingPosting';

        IF @@ROWCOUNT <> 1
            THROW 51543, N'The tool operational issuance was not advanced to Posted in a single atomic update.', 1;
    END;
    ELSE IF @AccountingEventType = 15
        UPDATE dbo.ToolIssuanceReversals SET AccountingEventId = @accountingEventId WHERE ToolIssuanceReversalId = @ToolIssuanceReversalId;

    SELECT @accountingEventId AS AccountingEventId,
           @financialTransactionId AS FinancialTransactionId,
           @journalEntryId AS JournalEntryId,
           CAST(0 AS bit) AS IsExisting;
END;
GO
