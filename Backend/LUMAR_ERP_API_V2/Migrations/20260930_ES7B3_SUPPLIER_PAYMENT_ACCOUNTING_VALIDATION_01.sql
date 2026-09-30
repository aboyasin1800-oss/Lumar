SET XACT_ABORT ON;
IF DB_NAME()<>N'LUMAR_ERP_ES_VALIDATION' THROW 52520,N'ES-7B-3 supplier payment accounting validation setup is restricted to LUMAR_ERP_ES_VALIDATION.',1;
BEGIN TRY
    BEGIN TRANSACTION;

    IF NOT EXISTS(SELECT 1 FROM dbo.AccountRoleMappings WHERE AccountRole=N'Cash' AND LedgerAccountId IS NOT NULL AND IsEnabled=1)
        THROW 52521,N'Cash account role mapping is unavailable.',1;
    IF NOT EXISTS(SELECT 1 FROM dbo.AccountRoleMappings WHERE AccountRole=N'SupplierLiability' AND LedgerAccountId IS NOT NULL AND IsEnabled=1)
        THROW 52522,N'SupplierLiability account role mapping is unavailable.',1;

    UPDATE dbo.AccountingEventDefinitions
    SET IsEnabled=1,IsBusinessRuntimeEnabled=0
    WHERE AccountingEventType IN(29,31)
      AND EventName IN(N'SupplierInvoiceImmediatePayment',N'SupplierPayment')
      AND SourceType=N'SupplierPayment'
      AND DebitAccountRole=N'SupplierLiability'
      AND CreditAccountRole=N'Cash'
      AND RequiresCashMovement=1;

    IF (SELECT COUNT(*) FROM dbo.AccountingEventDefinitions WHERE AccountingEventType IN(29,31) AND IsEnabled=1 AND IsBusinessRuntimeEnabled=0)<>2
        THROW 52523,N'Supplier payment accounting event definitions are unavailable.',1;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
