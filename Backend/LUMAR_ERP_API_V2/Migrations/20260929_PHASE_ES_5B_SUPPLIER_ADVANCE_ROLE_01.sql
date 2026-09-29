SET XACT_ABORT ON;
IF DB_NAME()<>N'LUMAR_ERP_TEST' THROW 52300,N'ES-5B is restricted to LUMAR_ERP_TEST.',1;
BEGIN TRY BEGIN TRANSACTION;
IF EXISTS(SELECT 1 FROM dbo.AccountRoleMappings WHERE AccountRole=N'SupplierAdvance') THROW 52301,N'SupplierAdvance role already exists.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.AccountingEventDefinitions WHERE AccountingEventType=30 AND EventName=N'SupplierAdvancePayment') THROW 52302,N'Event 30 is required.',1;
INSERT dbo.AccountRoleMappings(AccountRole,LedgerAccountId,IsEnabled) VALUES(N'SupplierAdvance',NULL,0);
UPDATE dbo.AccountingEventDefinitions SET DebitAccountRole=N'SupplierAdvance',CreditAccountRole=N'Cash',CashDirection=2,RequiresCashMovement=1,IsEnabled=0,IsBusinessRuntimeEnabled=0 WHERE AccountingEventType=30;
COMMIT TRANSACTION; END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;