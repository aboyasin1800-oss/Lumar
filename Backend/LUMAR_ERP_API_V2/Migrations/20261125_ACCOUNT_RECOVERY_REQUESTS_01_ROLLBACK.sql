SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() NOT IN (N'LUMAR_ERP_TEST', N'LUMAR_ERP_ES_VALIDATION', N'LUMAR_ERP_ES_VALIDATION_R3')
    THROW 53002, N'Account recovery rollback is restricted to approved development and validation databases.', 1;

IF EXISTS (SELECT 1 FROM dbo.AccountRecoveryRequests WHERE RequestStatus IN(N'Approved',N'Completed'))
    THROW 53003, N'Cannot roll back while approved or completed account recovery requests exist.', 1;

DROP TABLE IF EXISTS dbo.AccountRecoveryRequests;
