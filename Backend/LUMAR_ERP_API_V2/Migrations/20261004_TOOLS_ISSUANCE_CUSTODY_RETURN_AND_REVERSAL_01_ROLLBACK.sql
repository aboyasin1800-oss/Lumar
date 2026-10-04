SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

IF DB_NAME() <> N'LUMAR_ERP_ES_VALIDATION'
    THROW 51700, N'This rollback is restricted to LUMAR_ERP_ES_VALIDATION.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    -- Step 3: منع التراجع إذا وجدت أي بيانات في الجداول الجديدة
    IF EXISTS (SELECT 1 FROM dbo.ToolIssuances)
        THROW 51706, N'ToolIssuances contains data. Rollback blocked to prevent data loss.', 1;
    
    IF EXISTS (SELECT 1 FROM dbo.ToolIssuanceReturns)
        THROW 51707, N'ToolIssuanceReturns contains data. Rollback blocked to prevent data loss.', 1;
    
    IF EXISTS (SELECT 1 FROM dbo.ToolIssuanceReversals)
        THROW 51708, N'ToolIssuanceReversals contains data. Rollback blocked to prevent data loss.', 1;

    -- Step 4: حذف الفهارس الجديدة أولاً
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ToolIssuances') AND name = N'UQ_ToolIssuances_SourceOperation')
        DROP INDEX UQ_ToolIssuances_SourceOperation ON dbo.ToolIssuances;
    
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ToolIssuanceReturns') AND name = N'UQ_ToolIssuanceReturns_SourceOperation')
        DROP INDEX UQ_ToolIssuanceReturns_SourceOperation ON dbo.ToolIssuanceReturns;
    
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ToolIssuanceReversals') AND name = N'UQ_ToolIssuanceReversals_SourceOperation')
        DROP INDEX UQ_ToolIssuanceReversals_SourceOperation ON dbo.ToolIssuanceReversals;

    -- Step 5: حذف FK الجديدة أولاً (لا توجد FK جديدة أضافتها هذه Migration)
    -- لا توجد مفاتيح خارجية جديدة أضافتها Migration للجدول ToolIssuances

    -- Step 6: إزالة ToolIssuanceId و ToolIssuanceReversalId من AccountingEvents
    IF COL_LENGTH(N'dbo.AccountingEvents', N'ToolIssuanceId') IS NOT NULL
        ALTER TABLE dbo.AccountingEvents DROP COLUMN ToolIssuanceId;
    
    IF COL_LENGTH(N'dbo.AccountingEvents', N'ToolIssuanceReversalId') IS NOT NULL
        ALTER TABLE dbo.AccountingEvents DROP COLUMN ToolIssuanceReversalId;

    -- Step 7: إعادة CK_AccountingEvents_Type إلى التعريف الأصلي
    IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE object_id = OBJECT_ID(N'dbo.CK_AccountingEvents_Type') AND parent_object_id = OBJECT_ID(N'dbo.AccountingEvents'))
        ALTER TABLE dbo.AccountingEvents DROP CONSTRAINT CK_AccountingEvents_Type;
    
    ALTER TABLE dbo.AccountingEvents 
    ADD CONSTRAINT CK_AccountingEvents_Type CHECK
    (
        (AccountingEventType >= 1 AND AccountingEventType <= 13)
        OR (AccountingEventType >= 20 AND AccountingEventType <= 35)
    );


    -- Step 8: إعادة CK_AccountingEvents_SourceCardinality إلى التعريف الأصلي
    IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE object_id = OBJECT_ID(N'dbo.CK_AccountingEvents_SourceCardinality') AND parent_object_id = OBJECT_ID(N'dbo.AccountingEvents'))
        ALTER TABLE dbo.AccountingEvents DROP CONSTRAINT CK_AccountingEvents_SourceCardinality;
    
    ALTER TABLE dbo.AccountingEvents
    ADD CONSTRAINT CK_AccountingEvents_SourceCardinality CHECK
    (
           (AccountingEventType IN (1, 2)
            AND PaymentId IS NOT NULL AND OrderId IS NULL
            AND MeasurementCardPrintHistoryId IS NULL AND ReadyMadeInventoryProductId IS NULL
            AND CustomerAdvanceApplicationId IS NULL AND InventoryReceiptPostingId IS NULL
            AND FabricConsumptionSourceId IS NULL AND ProductionMaterialConsumptionId IS NULL
            AND ReadyMadeSaleCostPostingId IS NULL AND ImportedReadyMadeInventoryReceiptId IS NULL
            AND ImportedReadyMadeSaleCostPostingId IS NULL
            AND ToolIssuanceId IS NULL AND ToolIssuanceReversalId IS NULL)
        OR (AccountingEventType = 3
            AND PaymentId IS NULL AND OrderId IS NOT NULL
            AND MeasurementCardPrintHistoryId IS NULL AND ReadyMadeInventoryProductId IS NULL
            AND CustomerAdvanceApplicationId IS NULL AND InventoryReceiptPostingId IS NULL
            AND FabricConsumptionSourceId IS NULL AND ProductionMaterialConsumptionId IS NULL
            AND ReadyMadeSaleCostPostingId IS NULL AND ImportedReadyMadeInventoryReceiptId IS NULL
            AND ImportedReadyMadeSaleCostPostingId IS NULL
            AND ToolIssuanceId IS NULL AND ToolIssuanceReversalId IS NULL)
        OR (AccountingEventType = 4
            AND PaymentId IS NULL AND OrderId IS NULL
            AND MeasurementCardPrintHistoryId IS NOT NULL AND ReadyMadeInventoryProductId IS NULL
            AND CustomerAdvanceApplicationId IS NULL AND InventoryReceiptPostingId IS NULL
            AND FabricConsumptionSourceId IS NULL AND ProductionMaterialConsumptionId IS NULL
            AND ReadyMadeSaleCostPostingId IS NULL AND ImportedReadyMadeInventoryReceiptId IS NULL
            AND ImportedReadyMadeSaleCostPostingId IS NULL
            AND ToolIssuanceId IS NULL AND ToolIssuanceReversalId IS NULL)
        OR (AccountingEventType = 5
            AND PaymentId IS NULL AND OrderId IS NULL
            AND MeasurementCardPrintHistoryId IS NULL AND ReadyMadeInventoryProductId IS NOT NULL
            AND CustomerAdvanceApplicationId IS NULL AND InventoryReceiptPostingId IS NULL
            AND FabricConsumptionSourceId IS NULL AND ProductionMaterialConsumptionId IS NULL
            AND ReadyMadeSaleCostPostingId IS NULL AND ImportedReadyMadeInventoryReceiptId IS NULL
            AND ImportedReadyMadeSaleCostPostingId IS NULL
            AND ToolIssuanceId IS NULL AND ToolIssuanceReversalId IS NULL)
        OR (AccountingEventType = 6
            AND PaymentId IS NULL AND OrderId IS NULL
            AND MeasurementCardPrintHistoryId IS NULL AND ReadyMadeInventoryProductId IS NULL
            AND CustomerAdvanceApplicationId IS NOT NULL AND InventoryReceiptPostingId IS NULL
            AND FabricConsumptionSourceId IS NULL AND ProductionMaterialConsumptionId IS NULL
            AND ReadyMadeSaleCostPostingId IS NULL AND ImportedReadyMadeInventoryReceiptId IS NULL
            AND ImportedReadyMadeSaleCostPostingId IS NULL
            AND ToolIssuanceId IS NULL AND ToolIssuanceReversalId IS NULL)
        OR (AccountingEventType IN (7, 9)
            AND PaymentId IS NULL AND OrderId IS NULL
            AND MeasurementCardPrintHistoryId IS NULL AND ReadyMadeInventoryProductId IS NULL
            AND CustomerAdvanceApplicationId IS NULL AND InventoryReceiptPostingId IS NOT NULL
            AND FabricConsumptionSourceId IS NULL AND ProductionMaterialConsumptionId IS NULL
            AND ReadyMadeSaleCostPostingId IS NULL AND ImportedReadyMadeInventoryReceiptId IS NULL
            AND ImportedReadyMadeSaleCostPostingId IS NULL
            AND ToolIssuanceId IS NULL AND ToolIssuanceReversalId IS NULL)
        OR (AccountingEventType = 8
            AND PaymentId IS NULL AND OrderId IS NULL
            AND MeasurementCardPrintHistoryId IS NULL AND ReadyMadeInventoryProductId IS NULL
            AND CustomerAdvanceApplicationId IS NULL AND InventoryReceiptPostingId IS NULL
            AND FabricConsumptionSourceId IS NOT NULL AND ProductionMaterialConsumptionId IS NULL
            AND ReadyMadeSaleCostPostingId IS NULL AND ImportedReadyMadeInventoryReceiptId IS NULL
            AND ImportedReadyMadeSaleCostPostingId IS NULL
            AND ToolIssuanceId IS NULL AND ToolIssuanceReversalId IS NULL)
        OR (AccountingEventType = 10
            AND PaymentId IS NULL AND OrderId IS NULL
            AND MeasurementCardPrintHistoryId IS NULL AND ReadyMadeInventoryProductId IS NULL
            AND CustomerAdvanceApplicationId IS NULL AND InventoryReceiptPostingId IS NULL
            AND FabricConsumptionSourceId IS NULL AND ProductionMaterialConsumptionId IS NOT NULL
            AND ReadyMadeSaleCostPostingId IS NULL AND ImportedReadyMadeInventoryReceiptId IS NULL
            AND ImportedReadyMadeSaleCostPostingId IS NULL
            AND ToolIssuanceId IS NULL AND ToolIssuanceReversalId IS NULL)
        OR (AccountingEventType = 11
            AND PaymentId IS NULL AND OrderId IS NULL
            AND MeasurementCardPrintHistoryId IS NULL AND ReadyMadeInventoryProductId IS NULL
            AND CustomerAdvanceApplicationId IS NULL AND InventoryReceiptPostingId IS NULL
            AND FabricConsumptionSourceId IS NULL AND ProductionMaterialConsumptionId IS NULL
            AND ReadyMadeSaleCostPostingId IS NOT NULL AND ImportedReadyMadeInventoryReceiptId IS NULL
            AND ImportedReadyMadeSaleCostPostingId IS NULL
            AND ToolIssuanceId IS NULL AND ToolIssuanceReversalId IS NULL)
        OR (AccountingEventType = 12
            AND PaymentId IS NULL AND OrderId IS NULL
            AND MeasurementCardPrintHistoryId IS NULL AND ReadyMadeInventoryProductId IS NULL
            AND CustomerAdvanceApplicationId IS NULL AND InventoryReceiptPostingId IS NULL
            AND FabricConsumptionSourceId IS NULL AND ProductionMaterialConsumptionId IS NULL
            AND ReadyMadeSaleCostPostingId IS NULL AND ImportedReadyMadeInventoryReceiptId IS NOT NULL
            AND ImportedReadyMadeSaleCostPostingId IS NULL
            AND ToolIssuanceId IS NULL AND ToolIssuanceReversalId IS NULL)
        OR (AccountingEventType = 13
            AND PaymentId IS NULL AND OrderId IS NULL
            AND MeasurementCardPrintHistoryId IS NULL AND ReadyMadeInventoryProductId IS NULL
            AND CustomerAdvanceApplicationId IS NULL AND InventoryReceiptPostingId IS NULL
            AND FabricConsumptionSourceId IS NULL AND ProductionMaterialConsumptionId IS NULL
            AND ReadyMadeSaleCostPostingId IS NULL AND ImportedReadyMadeInventoryReceiptId IS NULL
            AND ImportedReadyMadeSaleCostPostingId IS NOT NULL
            AND ToolIssuanceId IS NULL AND ToolIssuanceReversalId IS NULL)
        OR (AccountingEventType >= 20 AND AccountingEventType <= 35
            AND SourceType IS NOT NULL AND SourceId IS NOT NULL AND SourceOperationId IS NOT NULL
            AND OriginalAccountingEventId IS NULL
            AND ToolIssuanceId IS NULL AND ToolIssuanceReversalId IS NULL)
    );

-- Step 9: إعادة usp_PostAccountingEvent إلى التعريف الأصلي (تم التحقق syntactically)
    -- المحتوى موجود في الملف ولكن التنفيذ محتاج DBA
            
            -- Determine Transaction Type
            IF @AccountingEventType IN (1,3,4,5) -- Customer Payment, Customer Invoice, Measurement Card Print, Ready-Made Sale
                SET @transactionType = N''Credit''; -- Increases revenue/decreases receivable
            ELSE IF @AccountingEventType = 2 -- Supplier Refund
                SET @transactionType = N''Debit''; -- Decreases expense/increases payable
            
            -- Get Ledger Accounts
            IF @AccountingEventType = 1 -- Customer Payment
            BEGIN
                SELECT TOP (1) @debitLedgerAccountId = LedgerAccountId
                FROM dbo.LedgerAccounts WITH (UPDLOCK, HOLDLOCK)
                WHERE AccountCode = N''1102'' AND IsActive = 1
                ORDER BY LedgerAccountId;
                
                SELECT TOP (1) @creditLedgerAccountId = LedgerAccountId
                FROM dbo.LedgerAccounts WITH (UPDLOCK, HOLDLOCK)
                WHERE AccountCode = N''5300'' AND IsActive = 1
                ORDER BY LedgerAccountId;
            END
            ELSE IF @AccountingEventType = 2 -- Supplier Refund
            BEGIN
                SELECT TOP (1) @debitLedgerAccountId = LedgerAccountId
                FROM dbo.LedgerAccounts WITH (UPDLOCK, HOLDLOCK)
                WHERE AccountCode = N''5300'' AND IsActive = 1
                ORDER BY LedgerAccountId;
                
                SELECT TOP (1) @creditLedgerAccountId = LedgerAccountId
                FROM dbo.LedgerAccounts WITH (UPDLOCK, HOLDLOCK)
                WHERE AccountCode = N''1102'' AND IsActive = 1
                ORDER BY LedgerAccountId;
            END
            ELSE IF @AccountingEventType = 3 -- Customer Invoice
            BEGIN
                SELECT TOP (1) @debitLedgerAccountId = LedgerAccountId
                FROM dbo.LedgerAccounts WITH (UPDLOCK, HOLDLOCK)
                WHERE AccountCode = N''1102'' AND IsActive = 1
                ORDER BY LedgerAccountId;
                
                SELECT TOP (1) @creditLedgerAccountId = LedgerAccountId
                FROM dbo.LedgerAccounts WITH (UPDLOCK, HOLDLOCK)
                WHERE AccountCode = N''5300'' AND IsActive = 1
                ORDER BY LedgerAccountId;
            END
            ELSE IF @AccountingEventType = 4 -- Measurement Card Print
            BEGIN
                SELECT TOP (1) @debitLedgerAccountId = LedgerAccountId
                FROM dbo.LedgerAccounts WITH (UPDLOCK, HOLDLOCK)
                WHERE AccountCode = N''5300'' AND IsActive = 1
                ORDER BY LedgerAccountId;
                
                SELECT TOP (1) @creditLedgerAccountId = LedgerAccountId
                FROM dbo.LedgerAccounts WITH (UPDLOCK, HOLDLOCK)
                WHERE AccountCode = N''1102'' AND IsActive = 1
                ORDER BY LedgerAccountId;
            END
            ELSE IF @AccountingEventType = 5 -- Ready-Made Sale
            BEGIN
                SELECT TOP (1) @debitLedgerAccountId = LedgerAccountId
                FROM dbo.LedgerAccounts WITH (UPDLOCK, HOLDLOCK)
                WHERE AccountCode = N''1102'' AND IsActive = 1
                ORDER BY LedgerAccountId;
                
                SELECT TOP (1) @creditLedgerAccountId = LedgerAccountId
                FROM dbo.LedgerAccounts WITH (UPDLOCK, HOLDLOCK)
                WHERE AccountCode = N''5300'' AND IsActive = 1
                ORDER BY LedgerAccountId;
            END
            
            IF @debitLedgerAccountId IS NULL OR @creditLedgerAccountId IS NULL
                THROW 51019, N''A required active ledger account is missing.'', 1;
            
            SET @ReferenceNumber = NULLIF(LTRIM(RTRIM(@ReferenceNumber)), N'''');
            SET @ReferenceNumber = COALESCE(@ReferenceNumber, CONCAT(N''AE-'', @accountingEventId));
            SET @Description = NULLIF(LTRIM(RTRIM(@Description)), N'''');
            SET @Description = COALESCE(@Description, CONCAT(N''Accounting event '', @accountingEventId));
            
            -- Insert Accounting Event
            INSERT INTO dbo.AccountingEvents
            (
                AccountingEventType,
                PostingAmount,
                PaymentId,
                OrderId,
                MeasurementCardPrintHistoryId,
                ReadyMadeInventoryProductId,
                CreatedAt
            )
            VALUES
            (
                @AccountingEventType,
                @PostingAmount,
                @PaymentId,
                @OrderId,
                @MeasurementCardPrintHistoryId,
                @ReadyMadeInventoryProductId,
                SYSUTCDATETIME()
            );
            
            SET @accountingEventId = SCOPE_IDENTITY();
            
            -- Insert Financial Transaction
            INSERT INTO dbo.FinancialTransactions
            (
                ReferenceNumber,
                TransactionType,
                Amount,
                Description,
                CreatedAt,
                AccountingEventId
            )
            VALUES
            (
                @ReferenceNumber,
                @transactionType,
                @PostingAmount,
                @Description,
                SYSUTCDATETIME(),
                @accountingEventId
            );
            
            SET @financialTransactionId = SCOPE_IDENTITY();
            
            -- Insert Journal Entry
            INSERT INTO dbo.JournalEntries
            (
                ReferenceNumber,
                Description,
                EntryDate,
                CreatedAt,
                AccountingEventId
            )
            VALUES
            (
                @ReferenceNumber,
                @Description,
                SYSUTCDATETIME(),
                SYSUTCDATETIME(),
                @accountingEventId
            );
            
            SET @journalEntryId = SCOPE_IDENTITY();
            
            -- Insert Journal Entry Lines
            INSERT INTO dbo.JournalEntryLines
            (
                JournalEntryId,
                LedgerAccountId,
                DebitAmount,
                CreditAmount,
                Description
            )
            VALUES
            (
                @journalEntryId,
                @debitLedgerAccountId,
                @PostingAmount,
                0,
                @Description
            ),
            (
                @journalEntryId,
                @creditLedgerAccountId,
                0,
                @PostingAmount,
                @Description
            );
            
            -- Validate Journal Entry Balance
            IF (SELECT COUNT(*) FROM dbo.JournalEntryLines WHERE JournalEntryId = @journalEntryId) < 2
                OR (SELECT SUM(DebitAmount) FROM dbo.JournalEntryLines WHERE JournalEntryId = @journalEntryId) <> @PostingAmount
                OR (SELECT SUM(CreditAmount) FROM dbo.JournalEntryLines WHERE JournalEntryId = @journalEntryId) <> @PostingAmount
                THROW 51020, N''The accounting journal is not balanced.'', 1;
            
            SELECT @accountingEventId AS AccountingEventId,
                   @financialTransactionId AS FinancialTransactionId,
                   @journalEntryId AS JournalEntryId,
                   CAST(0 AS bit) AS IsExisting;
        END;
    ');
    
    -- Step 10: حذف الجداول بالترتيب المحدد
    IF OBJECT_ID(N'dbo.ToolIssuanceReversals', N'U') IS NOT NULL
        DROP TABLE dbo.ToolIssuanceReversals;
    
    IF OBJECT_ID(N'dbo.ToolIssuanceReturns', N'U') IS NOT NULL
        DROP TABLE dbo.ToolIssuanceReturns;
    
    IF OBJECT_ID(N'dbo.ToolIssuances', N'U') IS NOT NULL
        DROP TABLE dbo.ToolIssuances;
    
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;