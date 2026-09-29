SET XACT_ABORT ON;
IF DB_NAME() NOT IN(N'LUMAR_ERP_TEST',N'LUMAR_ERP_ES_VALIDATION') THROW 52340,N'ES-5F is restricted to approved ES validation databases.',1;
BEGIN TRY BEGIN TRANSACTION;
IF NOT EXISTS(SELECT 1 FROM dbo.AccountRoleMappings WHERE AccountRole=N'SupplierAdvance') THROW 52341,N'SupplierAdvance mapping is required.',1;
UPDATE dbo.AccountingEventDefinitions SET DebitAccountRole=N'SupplierLiability',CreditAccountRole=N'SupplierAdvance',RequiresCashMovement=0,CashDirection=NULL,IsEnabled=1,IsBusinessRuntimeEnabled=0 WHERE AccountingEventType=32 AND EventName=N'SupplierPaymentAllocation';
COMMIT TRANSACTION; END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;