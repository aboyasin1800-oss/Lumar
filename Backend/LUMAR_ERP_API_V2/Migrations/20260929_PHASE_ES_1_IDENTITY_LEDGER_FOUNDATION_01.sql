SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() NOT IN (N'LUMAR_ERP_TEST', N'LUMAR_ERP_ES_VALIDATION')
    THROW 51800, N'This Phase ES-1 migration is restricted to approved ES validation databases.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.Employees', N'U') IS NULL
       OR OBJECT_ID(N'dbo.Suppliers', N'U') IS NULL
       OR OBJECT_ID(N'dbo.SupplierLedgerEntries', N'U') IS NULL
        THROW 51801, N'Phase ES-1 requires Employees, Suppliers, and SupplierLedgerEntries.', 1;

    IF COL_LENGTH(N'dbo.Employees', N'SalaryType') IS NULL
       OR COL_LENGTH(N'dbo.Employees', N'BasicSalary') IS NULL
        THROW 51802, N'Phase ES-1 requires Employees.SalaryType and Employees.BasicSalary.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM dbo.Employees
        WHERE SalaryType IS NULL
           OR SalaryType NOT IN (N'BasicSalary', N'PieceWage')
           OR (SalaryType = N'BasicSalary' AND BasicSalary <= 0)
           OR (SalaryType = N'PieceWage' AND BasicSalary <> 0)
    )
        THROW 51803, N'Employee salary types must be classified before Phase ES-1; no speculative backfill is permitted.', 1;

    IF OBJECT_ID(N'dbo.EmployeeLedgerEntries', N'U') IS NOT NULL
        THROW 51804, N'EmployeeLedgerEntries already exists.', 1;

    IF EXISTS
    (
        SELECT 1
        FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.Employees')
          AND name = N'CK_Employees_OfficialSalaryType'
    )
        THROW 51805, N'Employees salary-type contract already exists.', 1;

    CREATE TABLE dbo.EmployeeLedgerEntries
    (
        EmployeeLedgerEntryId bigint IDENTITY(1,1) NOT NULL,
        EmployeeId int NOT NULL,
        EntryType nvarchar(50) NOT NULL,
        Amount decimal(18,2) NOT NULL,
        BalanceEffect decimal(18,2) NOT NULL,
        OccurredAt datetime2(7) NOT NULL,
        EffectiveDate date NOT NULL,
        SourceType nvarchar(50) NOT NULL,
        SourceId bigint NOT NULL,
        SourceOperationId uniqueidentifier NOT NULL,
        ReferenceNumber nvarchar(200) NOT NULL,
        OriginalEntryId bigint NULL,
        ReversalReason nvarchar(500) NULL,
        ReversedBy nvarchar(100) NULL,
        ReversedAt datetime2(7) NULL,
        CreatedBy nvarchar(100) NOT NULL,
        CreatedAt datetime2(7) NOT NULL
            CONSTRAINT DF_EmployeeLedgerEntries_CreatedAt DEFAULT SYSUTCDATETIME(),
        Status nvarchar(20) NOT NULL,

        CONSTRAINT PK_EmployeeLedgerEntries PRIMARY KEY (EmployeeLedgerEntryId),
        CONSTRAINT FK_EmployeeLedgerEntries_Employees_EmployeeId
            FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees(EmployeeID),
        CONSTRAINT FK_EmployeeLedgerEntries_OriginalEntry_OriginalEntryId
            FOREIGN KEY (OriginalEntryId) REFERENCES dbo.EmployeeLedgerEntries(EmployeeLedgerEntryId),
        CONSTRAINT CK_EmployeeLedgerEntries_EntryType CHECK
            (EntryType IN (N'SalaryAccrual', N'SeasonalBonus', N'PieceWageAccrual', N'Advance', N'SalariedEmployeeDailyExpense', N'PieceWorkerDailyExpense', N'EmployeePayment', N'Reversal')),
        CONSTRAINT CK_EmployeeLedgerEntries_Amount CHECK (Amount > 0),
        CONSTRAINT CK_EmployeeLedgerEntries_BalanceEffect CHECK (BalanceEffect <> 0),
        CONSTRAINT CK_EmployeeLedgerEntries_Status CHECK (Status IN (N'Posted', N'Reversed')),
        CONSTRAINT CK_EmployeeLedgerEntries_Reversal CHECK
        (
            (EntryType = N'Reversal'
             AND OriginalEntryId IS NOT NULL
             AND ReversalReason IS NOT NULL
             AND ReversedBy IS NOT NULL
             AND ReversedAt IS NOT NULL)
            OR
            (EntryType <> N'Reversal'
             AND OriginalEntryId IS NULL
             AND ReversalReason IS NULL
             AND ReversedBy IS NULL
             AND ReversedAt IS NULL)
        )
    );

    ALTER TABLE dbo.Employees WITH CHECK
        ADD CONSTRAINT CK_Employees_OfficialSalaryType CHECK
        (
            SalaryType IN (N'BasicSalary', N'PieceWage')
            AND ((SalaryType = N'BasicSalary' AND BasicSalary > 0)
                 OR (SalaryType = N'PieceWage' AND BasicSalary = 0))
        );

    ALTER TABLE dbo.SupplierLedgerEntries ADD
        EntryType nvarchar(50) NULL,
        Amount decimal(18,2) NULL,
        BalanceEffect decimal(18,2) NULL,
        OccurredAt datetime2(7) NULL,
        SourceType nvarchar(50) NULL,
        SourceId bigint NULL,
        SourceOperationId uniqueidentifier NULL,
        OriginalEntryId int NULL,
        ReversalReason nvarchar(500) NULL,
        ReversedBy nvarchar(100) NULL,
        ReversedAt datetime2(7) NULL,
        CreatedBy nvarchar(100) NULL,
        Status nvarchar(20) NOT NULL
            CONSTRAINT DF_SupplierLedgerEntries_Status DEFAULT N'Legacy';

    ALTER TABLE dbo.SupplierLedgerEntries WITH CHECK
        ADD CONSTRAINT FK_SupplierLedgerEntries_OriginalEntry_OriginalEntryId
            FOREIGN KEY (OriginalEntryId) REFERENCES dbo.SupplierLedgerEntries(SupplierLedgerEntryId),
            CONSTRAINT CK_SupplierLedgerEntries_FoundationStatus CHECK (Status IN (N'Legacy', N'Posted', N'Reversed')),
            CONSTRAINT CK_SupplierLedgerEntries_FoundationFields CHECK
            (
                Status = N'Legacy'
                OR
                (
                    EntryType IS NOT NULL
                    AND Amount > 0
                    AND BalanceEffect <> 0
                    AND OccurredAt IS NOT NULL
                    AND SourceType IS NOT NULL
                    AND SourceId IS NOT NULL
                    AND SourceOperationId IS NOT NULL
                    AND CreatedBy IS NOT NULL
                    AND
                    (
                        (EntryType = N'Reversal'
                         AND OriginalEntryId IS NOT NULL
                         AND ReversalReason IS NOT NULL
                         AND ReversedBy IS NOT NULL
                         AND ReversedAt IS NOT NULL)
                        OR
                        (EntryType <> N'Reversal'
                         AND OriginalEntryId IS NULL
                         AND ReversalReason IS NULL
                         AND ReversedBy IS NULL
                         AND ReversedAt IS NULL)
                    )
                )
            );

    CREATE UNIQUE INDEX UX_EmployeeLedgerEntries_SourceOperation_EntryType
        ON dbo.EmployeeLedgerEntries(SourceOperationId, EntryType);
    CREATE UNIQUE INDEX UX_EmployeeLedgerEntries_OriginalEntry_Reversal
        ON dbo.EmployeeLedgerEntries(OriginalEntryId)
        WHERE EntryType = N'Reversal';
    CREATE INDEX IX_EmployeeLedgerEntries_Employee_OccurredAt
        ON dbo.EmployeeLedgerEntries(EmployeeId, OccurredAt, EmployeeLedgerEntryId);

    EXEC(N'CREATE UNIQUE INDEX UX_SupplierLedgerEntries_SourceOperation_EntryType ON dbo.SupplierLedgerEntries(SourceOperationId, EntryType) WHERE SourceOperationId IS NOT NULL;');
    EXEC(N'CREATE UNIQUE INDEX UX_SupplierLedgerEntries_OriginalEntry_Reversal ON dbo.SupplierLedgerEntries(OriginalEntryId) WHERE EntryType = N''Reversal'';');
    EXEC(N'CREATE INDEX IX_SupplierLedgerEntries_Supplier_OccurredAt ON dbo.SupplierLedgerEntries(SupplierId, OccurredAt, SupplierLedgerEntryId) WHERE OccurredAt IS NOT NULL;');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;