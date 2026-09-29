SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() NOT IN (N'LUMAR_ERP_TEST', N'LUMAR_ERP_ES_VALIDATION')
    THROW 51900, N'This Phase ES-2 migration is restricted to approved ES validation databases.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.AccountingEventDefinitions', N'U') IS NOT NULL
       OR OBJECT_ID(N'dbo.AccountRoleMappings', N'U') IS NOT NULL
        THROW 51901, N'Phase ES-2 foundation already exists.', 1;

    IF OBJECT_ID(N'dbo.AccountingEvents', N'U') IS NULL
       OR OBJECT_ID(N'dbo.CashMovements', N'U') IS NULL
       OR OBJECT_ID(N'dbo.trg_CashMovements_OfficialWriter', N'TR') IS NULL
        THROW 51902, N'Existing accounting and official cash foundations are required.', 1;

    IF EXISTS (SELECT 1 FROM dbo.AccountingEvents WHERE AccountingEventType BETWEEN 20 AND 33)
        THROW 51903, N'Phase ES-2 event type range is already in use.', 1;

    CREATE TABLE dbo.AccountingEventDefinitions
    (
        AccountingEventType tinyint NOT NULL,
        EventName nvarchar(100) NOT NULL,
        SourceType nvarchar(50) NOT NULL,
        DebitAccountRole nvarchar(50) NULL,
        CreditAccountRole nvarchar(50) NULL,
        CashDirection tinyint NULL,
        RequiresCashMovement bit NOT NULL,
        LiabilityImpact smallint NOT NULL,
        ExpenseImpact tinyint NOT NULL,
        InventoryImpact tinyint NOT NULL,
        IsEnabled bit NOT NULL CONSTRAINT DF_AccountingEventDefinitions_IsEnabled DEFAULT 0,
        IsBusinessRuntimeEnabled bit NOT NULL CONSTRAINT DF_AccountingEventDefinitions_IsBusinessRuntimeEnabled DEFAULT 0,
        CONSTRAINT PK_AccountingEventDefinitions PRIMARY KEY (AccountingEventType),
        CONSTRAINT UQ_AccountingEventDefinitions_EventName UNIQUE (EventName),
        CONSTRAINT CK_AccountingEventDefinitions_Cash CHECK ((RequiresCashMovement=1 AND CashDirection IN (1,2)) OR (RequiresCashMovement=0 AND CashDirection IS NULL)),
        CONSTRAINT CK_AccountingEventDefinitions_Roles CHECK ((AccountingEventType=33 AND DebitAccountRole IS NULL AND CreditAccountRole IS NULL) OR (DebitAccountRole IS NOT NULL AND CreditAccountRole IS NOT NULL))
    );

    CREATE TABLE dbo.AccountRoleMappings
    (
        AccountRole nvarchar(50) NOT NULL,
        LedgerAccountId int NULL,
        IsEnabled bit NOT NULL CONSTRAINT DF_AccountRoleMappings_IsEnabled DEFAULT 0,
        CONSTRAINT PK_AccountRoleMappings PRIMARY KEY (AccountRole),
        CONSTRAINT FK_AccountRoleMappings_LedgerAccounts FOREIGN KEY (LedgerAccountId) REFERENCES dbo.LedgerAccounts(LedgerAccountId)
    );

    INSERT INTO dbo.AccountRoleMappings (AccountRole) VALUES
        (N'SalaryExpense'), (N'PieceWageExpense'), (N'EmployeeLiability'), (N'EmployeeAdvance'),
        (N'EmployeeDailyExpense'), (N'SupplierLiability'), (N'PurchaseClearing'), (N'Cash');

    INSERT INTO dbo.AccountingEventDefinitions
        (AccountingEventType, EventName, SourceType, DebitAccountRole, CreditAccountRole, CashDirection, RequiresCashMovement, LiabilityImpact, ExpenseImpact, InventoryImpact)
    VALUES
        (20,N'SalaryAccrual',N'EmployeeLedgerEntry',N'SalaryExpense',N'EmployeeLiability',NULL,0,0,1,0),
        (21,N'SeasonalBonus',N'EmployeeLedgerEntry',N'SalaryExpense',N'EmployeeLiability',NULL,0,0,1,0),
        (22,N'PieceWageAccrual',N'EmployeeLedgerEntry',N'PieceWageExpense',N'EmployeeLiability',NULL,0,0,1,0),
        (23,N'EmployeeAdvance',N'EmployeeLedgerEntry',N'EmployeeAdvance',N'Cash',2,1,0,0,0),
        (24,N'SalariedEmployeeDailyExpense',N'EmployeeLedgerEntry',N'EmployeeDailyExpense',N'Cash',2,1,0,1,0),
        (25,N'PieceWorkerDailyExpense',N'EmployeeLedgerEntry',N'EmployeeLiability',N'Cash',2,1,0,0,0),
        (26,N'EmployeeSalaryPayment',N'EmployeeLedgerEntry',N'EmployeeLiability',N'Cash',2,1,0,0,0),
        (27,N'PieceWorkerPayment',N'EmployeeLedgerEntry',N'EmployeeLiability',N'Cash',2,1,0,0,0),
        (28,N'SupplierInvoice',N'SupplierInvoice',N'PurchaseClearing',N'SupplierLiability',NULL,0,1,0,0),
        (29,N'SupplierInvoiceImmediatePayment',N'SupplierPayment',N'SupplierLiability',N'Cash',2,1,-1,0,0),
        (30,N'SupplierAdvancePayment',N'SupplierPayment',N'SupplierLiability',N'Cash',2,1,0,0,0),
        (31,N'SupplierPayment',N'SupplierPayment',N'SupplierLiability',N'Cash',2,1,-1,0,0),
        (32,N'SupplierPaymentAllocation',N'SupplierPaymentAllocation',N'SupplierLiability',N'SupplierLiability',NULL,0,0,0,0),
        (33,N'Reversal',N'AccountingEvent',NULL,NULL,NULL,0,0,0,0);

    ALTER TABLE dbo.AccountingEvents ADD
        SourceType nvarchar(50) NULL,
        SourceId bigint NULL,
        SourceOperationId uniqueidentifier NULL,
        OriginalAccountingEventId bigint NULL,
        ReversalReason nvarchar(500) NULL,
        ReversedBy nvarchar(100) NULL,
        ReversedAt datetime2(7) NULL,
        Status nvarchar(20) NOT NULL CONSTRAINT DF_AccountingEvents_ES2_Status DEFAULT N'Legacy';

    ALTER TABLE dbo.CashAccounts ADD AllowsDisbursements bit NULL;
    ALTER TABLE dbo.CashMovements ADD
        ReferenceNumber nvarchar(200) NULL,
        RecipientType nvarchar(50) NULL,
        RecipientId bigint NULL,
        SourceType nvarchar(50) NULL,
        SourceId bigint NULL,
        SourceOperationId uniqueidentifier NULL,
        OriginalCashMovementId bigint NULL,
        CreatedBy nvarchar(100) NULL,
        Status nvarchar(20) NOT NULL CONSTRAINT DF_CashMovements_ES2_Status DEFAULT N'Legacy';

    ALTER TABLE dbo.AccountingEvents DROP CONSTRAINT CK_AccountingEvents_Type;
    ALTER TABLE dbo.AccountingEvents DROP CONSTRAINT CK_AccountingEvents_SourceCardinality;
    ALTER TABLE dbo.AccountingEvents ADD
        CONSTRAINT CK_AccountingEvents_Type CHECK (AccountingEventType BETWEEN 1 AND 13 OR AccountingEventType BETWEEN 20 AND 33),
        CONSTRAINT FK_AccountingEvents_OriginalAccountingEvent FOREIGN KEY (OriginalAccountingEventId) REFERENCES dbo.AccountingEvents(AccountingEventId),
        CONSTRAINT CK_AccountingEvents_ES2_Status CHECK (Status IN (N'Legacy',N'Posted',N'Reversed')),
        CONSTRAINT CK_AccountingEvents_SourceCardinality CHECK
        (
            (AccountingEventType BETWEEN 1 AND 13)
            OR
            (AccountingEventType BETWEEN 20 AND 32
             AND SourceType IS NOT NULL AND SourceId IS NOT NULL AND SourceOperationId IS NOT NULL AND OriginalAccountingEventId IS NULL)
            OR
            (AccountingEventType=33
             AND SourceType=N'AccountingEvent' AND SourceId IS NOT NULL AND SourceOperationId IS NOT NULL
             AND OriginalAccountingEventId=SourceId AND ReversalReason IS NOT NULL AND ReversedBy IS NOT NULL AND ReversedAt IS NOT NULL)
        );

    ALTER TABLE dbo.CashMovements ADD
        CONSTRAINT FK_CashMovements_OriginalCashMovement FOREIGN KEY (OriginalCashMovementId) REFERENCES dbo.CashMovements(CashMovementId),
        CONSTRAINT CK_CashMovements_ES2_Status CHECK (Status IN (N'Legacy',N'Posted',N'Reversed')),
        CONSTRAINT CK_CashMovements_ES2_Foundation CHECK
        (
            Status=N'Legacy'
            OR (ReferenceNumber IS NOT NULL AND RecipientType IS NOT NULL AND RecipientId IS NOT NULL
                AND SourceType IS NOT NULL AND SourceId IS NOT NULL AND SourceOperationId IS NOT NULL AND CreatedBy IS NOT NULL)
        );

    EXEC(N'CREATE UNIQUE INDEX UX_AccountingEvents_SourceOperation_EventType ON dbo.AccountingEvents(SourceOperationId, AccountingEventType) WHERE SourceOperationId IS NOT NULL;');
    EXEC(N'CREATE UNIQUE INDEX UX_AccountingEvents_Original_Reversal ON dbo.AccountingEvents(OriginalAccountingEventId) WHERE AccountingEventType=33;');
    EXEC(N'CREATE UNIQUE INDEX UX_CashMovements_SourceOperation ON dbo.CashMovements(SourceOperationId) WHERE SourceOperationId IS NOT NULL;');
    EXEC(N'CREATE UNIQUE INDEX UX_CashMovements_Original_Reversal ON dbo.CashMovements(OriginalCashMovementId) WHERE OriginalCashMovementId IS NOT NULL;');

    EXEC(N'
CREATE OR ALTER PROCEDURE dbo.usp_PostFoundationCashMovement
    @AccountingEventId bigint, @CashAccountId int, @CashDirection tinyint, @Amount decimal(18,2), @CurrencyCode char(3),
    @ReferenceNumber nvarchar(200), @RecipientType nvarchar(50), @RecipientId bigint, @SourceType nvarchar(50), @SourceId bigint,
    @SourceOperationId uniqueidentifier, @CreatedBy nvarchar(100), @OriginalCashMovementId bigint = NULL
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF @@TRANCOUNT=0 THROW 51920, N''Foundation cash movements require a caller-owned transaction.'',1;
 IF @CashDirection NOT IN (1,2) OR @Amount<=0 OR @SourceOperationId IS NULL THROW 51921,N''Invalid foundation cash movement.'',1;
 IF NOT EXISTS (SELECT 1 FROM dbo.AccountingEvents WITH (UPDLOCK,HOLDLOCK) WHERE AccountingEventId=@AccountingEventId AND PostingAmount=@Amount AND Status=N''Posted'') THROW 51922,N''The accounting event is unavailable.'',1;
 IF NOT EXISTS (SELECT 1 FROM dbo.CashAccounts WITH (UPDLOCK,HOLDLOCK) WHERE CashAccountId=@CashAccountId AND IsActive=1 AND CurrencyCode=@CurrencyCode AND ((@CashDirection=1 AND AllowsReceipts=1) OR (@CashDirection=2 AND AllowsDisbursements=1))) THROW 51923,N''The cash account is unavailable.'',1;
 DECLARE @existing bigint; SELECT @existing=CashMovementId FROM dbo.CashMovements WITH (UPDLOCK,HOLDLOCK) WHERE SourceOperationId=@SourceOperationId;
 IF @existing IS NOT NULL BEGIN SELECT @existing AS CashMovementId,CAST(1 AS bit) AS IsExisting; RETURN; END;
 EXEC sys.sp_set_session_context @key=N''CashMovementWriter'',@value=1;
 BEGIN TRY
  DECLARE @out table (CashMovementId bigint); INSERT dbo.CashMovements(CashAccountId,AccountingEventId,CashDirection,Amount,OccurredAt,ReferenceNumber,RecipientType,RecipientId,SourceType,SourceId,SourceOperationId,OriginalCashMovementId,CreatedBy,Status)
  OUTPUT inserted.CashMovementId INTO @out VALUES(@CashAccountId,@AccountingEventId,@CashDirection,@Amount,SYSUTCDATETIME(),@ReferenceNumber,@RecipientType,@RecipientId,@SourceType,@SourceId,@SourceOperationId,@OriginalCashMovementId,@CreatedBy,N''Posted'');
  EXEC sys.sp_set_session_context @key=N''CashMovementWriter'',@value=NULL; SELECT CashMovementId,CAST(0 AS bit) AS IsExisting FROM @out;
 END TRY BEGIN CATCH EXEC sys.sp_set_session_context @key=N''CashMovementWriter'',@value=NULL; THROW; END CATCH
END;');

    EXEC(N'
CREATE OR ALTER PROCEDURE dbo.usp_PostFoundationAccountingEvent
 @AccountingEventType tinyint,@PostingAmount decimal(18,2),@SourceType nvarchar(50),@SourceId bigint,@SourceOperationId uniqueidentifier,
 @ReferenceNumber nvarchar(200),@Description nvarchar(1000),@CreatedBy nvarchar(100),@CurrencyCode char(3)=N''YER'',@CashAccountId int=NULL,@RecipientType nvarchar(50)=NULL,@RecipientId bigint=NULL
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF @@TRANCOUNT=0 THROW 51930,N''Foundation accounting events require a caller-owned transaction.'',1;
 DECLARE @debitRole nvarchar(50),@creditRole nvarchar(50),@cashDirection tinyint,@requiresCash bit,@definitionSource nvarchar(50),@debitId int,@creditId int;
 SELECT @debitRole=DebitAccountRole,@creditRole=CreditAccountRole,@cashDirection=CashDirection,@requiresCash=RequiresCashMovement,@definitionSource=SourceType FROM dbo.AccountingEventDefinitions WITH(UPDLOCK,HOLDLOCK) WHERE AccountingEventType=@AccountingEventType AND IsEnabled=1 AND IsBusinessRuntimeEnabled=0;
 IF @definitionSource IS NULL OR @definitionSource<>@SourceType OR @PostingAmount<=0 OR @SourceOperationId IS NULL THROW 51931,N''The event definition is disabled or invalid.'',1;
 SELECT @debitId=LedgerAccountId FROM dbo.AccountRoleMappings WHERE AccountRole=@debitRole AND IsEnabled=1;
 SELECT @creditId=LedgerAccountId FROM dbo.AccountRoleMappings WHERE AccountRole=@creditRole AND IsEnabled=1;
 IF @debitId IS NULL OR @creditId IS NULL THROW 51932,N''Posting is disabled until account mappings are approved.'',1;
 DECLARE @existing bigint; SELECT @existing=AccountingEventId FROM dbo.AccountingEvents WITH(UPDLOCK,HOLDLOCK) WHERE SourceOperationId=@SourceOperationId AND AccountingEventType=@AccountingEventType;
 IF @existing IS NOT NULL BEGIN
  IF EXISTS(SELECT 1 FROM dbo.AccountingEvents WHERE AccountingEventId=@existing AND (PostingAmount<>@PostingAmount OR SourceId<>@SourceId OR SourceType<>@SourceType)) THROW 51933,N''IDEMPOTENCY CONFLICT'',1;
  IF NOT EXISTS(SELECT 1 FROM dbo.FinancialTransactions WHERE AccountingEventId=@existing) OR NOT EXISTS(SELECT 1 FROM dbo.JournalEntries WHERE AccountingEventId=@existing) OR (@requiresCash=1 AND NOT EXISTS(SELECT 1 FROM dbo.CashMovements WHERE AccountingEventId=@existing)) THROW 51934,N''The existing event is incomplete.'',1;
  SELECT @existing AS AccountingEventId,CAST(1 AS bit) AS IsExisting; RETURN;
 END;
 INSERT dbo.AccountingEvents(AccountingEventType,PostingAmount,SourceType,SourceId,SourceOperationId,Status) VALUES(@AccountingEventType,@PostingAmount,@SourceType,@SourceId,@SourceOperationId,N''Posted''); DECLARE @eventId bigint=SCOPE_IDENTITY();
 INSERT dbo.FinancialTransactions(ReferenceNumber,TransactionType,Amount,Description,CreatedAt,AccountingEventId) VALUES(@ReferenceNumber,(SELECT EventName FROM dbo.AccountingEventDefinitions WHERE AccountingEventType=@AccountingEventType),@PostingAmount,@Description,SYSUTCDATETIME(),@eventId);
 INSERT dbo.JournalEntries(ReferenceNumber,Description,EntryDate,CreatedAt,AccountingEventId) VALUES(@ReferenceNumber,@Description,SYSUTCDATETIME(),SYSUTCDATETIME(),@eventId); DECLARE @journalId int=SCOPE_IDENTITY();
 INSERT dbo.JournalEntryLines(JournalEntryId,LedgerAccountId,DebitAmount,CreditAmount,Description) VALUES(@journalId,@debitId,@PostingAmount,0,@Description),(@journalId,@creditId,0,@PostingAmount,@Description);
 IF @requiresCash=1 EXEC dbo.usp_PostFoundationCashMovement @eventId,@CashAccountId,@cashDirection,@PostingAmount,@CurrencyCode,@ReferenceNumber,@RecipientType,@RecipientId,@SourceType,@SourceId,@SourceOperationId,@CreatedBy,NULL;
 SELECT @eventId AS AccountingEventId,CAST(0 AS bit) AS IsExisting;
END;');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;