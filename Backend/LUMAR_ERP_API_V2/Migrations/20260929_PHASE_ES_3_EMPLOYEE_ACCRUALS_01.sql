SET XACT_ABORT ON;
IF DB_NAME()<>N'LUMAR_ERP_TEST' THROW 52100,N'ES-3 is restricted to LUMAR_ERP_TEST.',1;
BEGIN TRY BEGIN TRANSACTION;
IF OBJECT_ID(N'dbo.EmployeeDailyAccruals',N'U') IS NOT NULL THROW 52101,N'ES-3 already exists.',1;
ALTER TABLE dbo.EmployeeLedgerEntries DROP CONSTRAINT CK_EmployeeLedgerEntries_BalanceEffect;
ALTER TABLE dbo.EmployeeLedgerEntries ADD CONSTRAINT CK_EmployeeLedgerEntries_BalanceEffect CHECK(BalanceEffect<>0 OR EntryType=N'SalariedEmployeeDailyExpense');
CREATE TABLE dbo.EmployeeDailyAccruals(
 EmployeeDailyAccrualId bigint IDENTITY PRIMARY KEY,EmployeeId int NOT NULL,AccrualDate date NOT NULL,EligibilityId bigint NOT NULL,Amount decimal(18,2) NOT NULL,SourceOperationId uniqueidentifier NOT NULL,CreatedBy nvarchar(100) NOT NULL,CreatedAt datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
 CONSTRAINT FK_EmployeeDailyAccruals_Employee FOREIGN KEY(EmployeeId) REFERENCES dbo.Employees(EmployeeID),CONSTRAINT FK_EmployeeDailyAccruals_Eligibility FOREIGN KEY(EligibilityId) REFERENCES dbo.EmployeeDailyEligibility(EmployeeDailyEligibilityId),CONSTRAINT UQ_EmployeeDailyAccruals_EmployeeDate UNIQUE(EmployeeId,AccrualDate),CONSTRAINT UQ_EmployeeDailyAccruals_Operation UNIQUE(SourceOperationId),CONSTRAINT CK_EmployeeDailyAccruals_Amount CHECK(Amount>0));
CREATE TABLE dbo.EmployeeSeasonalBonuses(
 EmployeeSeasonalBonusId bigint IDENTITY PRIMARY KEY,EmployeeId int NOT NULL,Season nvarchar(50) NOT NULL,SeasonYear int NOT NULL,Amount decimal(18,2) NOT NULL,SourceOperationId uniqueidentifier NOT NULL,CreatedBy nvarchar(100) NOT NULL,CreatedAt datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
 CONSTRAINT FK_EmployeeSeasonalBonuses_Employee FOREIGN KEY(EmployeeId) REFERENCES dbo.Employees(EmployeeID),CONSTRAINT UQ_EmployeeSeasonalBonuses_EmployeeSeason UNIQUE(EmployeeId,Season,SeasonYear),CONSTRAINT UQ_EmployeeSeasonalBonuses_Operation UNIQUE(SourceOperationId),CONSTRAINT CK_EmployeeSeasonalBonuses_Amount CHECK(Amount>0));
CREATE TABLE dbo.EmployeeAdvances(
 EmployeeAdvanceId bigint IDENTITY PRIMARY KEY,EmployeeId int NOT NULL,AdvanceDate date NOT NULL,Amount decimal(18,2) NOT NULL,CashAccountId int NOT NULL,SourceOperationId uniqueidentifier NOT NULL,CreatedBy nvarchar(100) NOT NULL,CreatedAt datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
 CONSTRAINT FK_EmployeeAdvances_Employee FOREIGN KEY(EmployeeId) REFERENCES dbo.Employees(EmployeeID),CONSTRAINT FK_EmployeeAdvances_Cash FOREIGN KEY(CashAccountId) REFERENCES dbo.CashAccounts(CashAccountId),CONSTRAINT UQ_EmployeeAdvances_Operation UNIQUE(SourceOperationId),CONSTRAINT CK_EmployeeAdvances_Amount CHECK(Amount>0));
CREATE TABLE dbo.EmployeeDailyExpenses(
 EmployeeDailyExpenseId bigint IDENTITY PRIMARY KEY,EmployeeId int NOT NULL,ExpenseDate date NOT NULL,Amount decimal(18,2) NOT NULL,CashAccountId int NOT NULL,SourceOperationId uniqueidentifier NOT NULL,CreatedBy nvarchar(100) NOT NULL,CreatedAt datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
 CONSTRAINT FK_EmployeeDailyExpenses_Employee FOREIGN KEY(EmployeeId) REFERENCES dbo.Employees(EmployeeID),CONSTRAINT FK_EmployeeDailyExpenses_Cash FOREIGN KEY(CashAccountId) REFERENCES dbo.CashAccounts(CashAccountId),CONSTRAINT UQ_EmployeeDailyExpenses_Operation UNIQUE(SourceOperationId),CONSTRAINT CK_EmployeeDailyExpenses_Amount CHECK(Amount>0));
COMMIT TRANSACTION; END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;