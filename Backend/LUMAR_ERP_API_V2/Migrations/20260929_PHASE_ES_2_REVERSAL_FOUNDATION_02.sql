SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() NOT IN (N'LUMAR_ERP_TEST', N'LUMAR_ERP_ES_VALIDATION')
    THROW 51940, N'This Phase ES-2 reversal migration is restricted to approved ES validation databases.', 1;

IF OBJECT_ID(N'dbo.usp_PostFoundationAccountingEvent', N'P') IS NULL
    THROW 51941, N'Phase ES-2 posting foundation is required.', 1;

EXEC(N'
CREATE OR ALTER PROCEDURE dbo.usp_ReverseFoundationAccountingEvent
 @OriginalAccountingEventId bigint,@SourceOperationId uniqueidentifier,@ReferenceNumber nvarchar(200),@Reason nvarchar(500),@ReversedBy nvarchar(100)
AS
BEGIN
 SET NOCOUNT ON; SET XACT_ABORT ON;
 IF @@TRANCOUNT=0 THROW 51942,N''Foundation reversals require a caller-owned transaction.'',1;
 IF @SourceOperationId IS NULL OR @Reason IS NULL OR @ReversedBy IS NULL THROW 51943,N''The reversal is incomplete.'',1;
 DECLARE @amount decimal(18,2),@originalType tinyint,@originalStatus nvarchar(20),@originalSourceType nvarchar(50),@originalSourceId bigint;
 SELECT @amount=PostingAmount,@originalType=AccountingEventType,@originalStatus=Status,@originalSourceType=SourceType,@originalSourceId=SourceId FROM dbo.AccountingEvents WITH(UPDLOCK,HOLDLOCK) WHERE AccountingEventId=@OriginalAccountingEventId;
 IF @amount IS NULL OR (@originalType NOT BETWEEN 20 AND 32 AND @originalType NOT IN(7,9)) OR @originalStatus<>N''Posted'' THROW 51944,N''The original event cannot be reversed.'',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.AccountingEventDefinitions WHERE AccountingEventType=33 AND IsEnabled=1 AND IsBusinessRuntimeEnabled=0) THROW 51945,N''The reversal definition is disabled.'',1;
 IF EXISTS(SELECT 1 FROM dbo.AccountingEvents WHERE OriginalAccountingEventId=@OriginalAccountingEventId) THROW 51946,N''The original event has already been reversed.'',1;
 DECLARE @existing bigint; SELECT @existing=AccountingEventId FROM dbo.AccountingEvents WITH(UPDLOCK,HOLDLOCK) WHERE SourceOperationId=@SourceOperationId AND AccountingEventType=33;
 IF @existing IS NOT NULL BEGIN SELECT @existing AS AccountingEventId,CAST(1 AS bit) AS IsExisting; RETURN; END;
 INSERT dbo.AccountingEvents(AccountingEventType,PostingAmount,SourceType,SourceId,SourceOperationId,OriginalAccountingEventId,ReversalReason,ReversedBy,ReversedAt,Status) VALUES(33,@amount,N''AccountingEvent'',@OriginalAccountingEventId,@SourceOperationId,@OriginalAccountingEventId,@Reason,@ReversedBy,SYSUTCDATETIME(),N''Posted''); DECLARE @eventId bigint=SCOPE_IDENTITY();
 INSERT dbo.FinancialTransactions(ReferenceNumber,TransactionType,Amount,Description,CreatedAt,AccountingEventId) VALUES(@ReferenceNumber,N''Reversal'',@amount,@Reason,SYSUTCDATETIME(),@eventId);
 INSERT dbo.JournalEntries(ReferenceNumber,Description,EntryDate,CreatedAt,AccountingEventId) VALUES(@ReferenceNumber,@Reason,SYSUTCDATETIME(),SYSUTCDATETIME(),@eventId); DECLARE @journalId int=SCOPE_IDENTITY();
 INSERT dbo.JournalEntryLines(JournalEntryId,LedgerAccountId,DebitAmount,CreditAmount,Description) SELECT @journalId,LedgerAccountId,CreditAmount,DebitAmount,@Reason FROM dbo.JournalEntryLines WHERE JournalEntryId=(SELECT JournalEntryId FROM dbo.JournalEntries WHERE AccountingEventId=@OriginalAccountingEventId);
 DECLARE @originalCashId bigint,@cashAccountId int,@cashDirection tinyint,@recipientType nvarchar(50),@recipientId bigint;
 SELECT @originalCashId=CashMovementId,@cashAccountId=CashAccountId,@cashDirection=CashDirection,@recipientType=RecipientType,@recipientId=RecipientId FROM dbo.CashMovements WITH(UPDLOCK,HOLDLOCK) WHERE AccountingEventId=@OriginalAccountingEventId;
 DECLARE @reversalCashDirection tinyint=CASE WHEN @cashDirection=1 THEN 2 ELSE 1 END;
 IF @originalCashId IS NOT NULL EXEC dbo.usp_PostFoundationCashMovement @AccountingEventId=@eventId,@CashAccountId=@cashAccountId,@CashDirection=@reversalCashDirection,@Amount=@amount,@CurrencyCode=N''YER'',@ReferenceNumber=@ReferenceNumber,@RecipientType=@recipientType,@RecipientId=@recipientId,@SourceType=N''AccountingEvent'',@SourceId=@OriginalAccountingEventId,@SourceOperationId=@SourceOperationId,@CreatedBy=@ReversedBy,@OriginalCashMovementId=@originalCashId;
 UPDATE dbo.AccountingEvents SET Status=N''Reversed'' WHERE AccountingEventId=@OriginalAccountingEventId;
 UPDATE dbo.CashMovements SET Status=N''Reversed'' WHERE CashMovementId=@originalCashId;
 SELECT @eventId AS AccountingEventId,CAST(0 AS bit) AS IsExisting;
END;');