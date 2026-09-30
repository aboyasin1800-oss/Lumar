SET XACT_ABORT ON;
IF DB_NAME()<>N'LUMAR_ERP_ES_VALIDATION' THROW 52510,N'ES-7B-3 supplier invoice accounting validation setup is restricted to LUMAR_ERP_ES_VALIDATION.',1;
BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @purchaseClearingId int=(SELECT LedgerAccountId FROM dbo.LedgerAccounts WHERE AccountCode=N'1120' AND IsActive=1);
    DECLARE @supplierLiabilityId int=(SELECT LedgerAccountId FROM dbo.LedgerAccounts WHERE AccountCode=N'2100' AND IsActive=1);
    IF @purchaseClearingId IS NULL OR @supplierLiabilityId IS NULL THROW 52511,N'Approved supplier invoice ledger accounts 1120 and 2100 are unavailable.',1;
    IF (SELECT COUNT(*) FROM dbo.LedgerAccounts WHERE AccountCode IN(N'1120',N'2100') AND IsActive=1)<>2 THROW 52512,N'Approved supplier invoice ledger account codes are duplicated or inactive.',1;

    UPDATE dbo.AccountRoleMappings SET LedgerAccountId=@purchaseClearingId,IsEnabled=1 WHERE AccountRole=N'PurchaseClearing';
    UPDATE dbo.AccountRoleMappings SET LedgerAccountId=@supplierLiabilityId,IsEnabled=1 WHERE AccountRole=N'SupplierLiability';
    IF @@ROWCOUNT<>1 THROW 52513,N'SupplierLiability account role mapping is unavailable.',1;
    IF NOT EXISTS(SELECT 1 FROM dbo.AccountRoleMappings WHERE AccountRole=N'PurchaseClearing' AND LedgerAccountId=@purchaseClearingId AND IsEnabled=1) THROW 52514,N'PurchaseClearing account role mapping is unavailable.',1;

    UPDATE dbo.AccountingEventDefinitions SET IsEnabled=1,IsBusinessRuntimeEnabled=0 WHERE AccountingEventType=28 AND EventName=N'SupplierInvoice' AND SourceType=N'SupplierInvoice' AND DebitAccountRole=N'PurchaseClearing' AND CreditAccountRole=N'SupplierLiability';
    IF @@ROWCOUNT<>1 THROW 52515,N'SupplierInvoice accounting event definition is unavailable.',1;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
