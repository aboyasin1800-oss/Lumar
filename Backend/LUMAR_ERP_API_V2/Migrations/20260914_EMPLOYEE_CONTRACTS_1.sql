SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID('dbo.EmployeePieceRateAssignments', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmployeePieceRateAssignments
    (
        EmployeePieceRateAssignmentId int IDENTITY(1,1) NOT NULL,
        EmployeeId int NOT NULL,
        PieceType nvarchar(120) NOT NULL,
        Stage nvarchar(120) NOT NULL,
        Rate decimal(18,2) NOT NULL,
        EffectiveFrom datetime2 NULL,
        EffectiveTo datetime2 NULL,
        IsActive bit NOT NULL CONSTRAINT DF_EmployeePieceRateAssignments_IsActive DEFAULT (1),
        CreatedAt datetime2 NOT NULL CONSTRAINT DF_EmployeePieceRateAssignments_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_EmployeePieceRateAssignments PRIMARY KEY (EmployeePieceRateAssignmentId),
        CONSTRAINT FK_EmployeePieceRateAssignments_Employees FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees(EmployeeID),
        CONSTRAINT CK_EmployeePieceRateAssignments_Rate CHECK (Rate > 0)
    );

    CREATE INDEX IX_EmployeePieceRateAssignments_EmployeeId ON dbo.EmployeePieceRateAssignments(EmployeeId);
    CREATE INDEX IX_EmployeePieceRateAssignments_EmployeeId_PieceType ON dbo.EmployeePieceRateAssignments(EmployeeId, PieceType, Stage);
END;

IF OBJECT_ID('dbo.EmployeeContractTemplates', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmployeeContractTemplates
    (
        ContractTemplateId int IDENTITY(1,1) NOT NULL,
        TemplateName nvarchar(200) NOT NULL,
        ContractType nvarchar(80) NOT NULL CONSTRAINT DF_EmployeeContractTemplates_ContractType DEFAULT N'Standard',
        IsActive bit NOT NULL CONSTRAINT DF_EmployeeContractTemplates_IsActive DEFAULT (1),
        TemplateText nvarchar(max) NOT NULL,
        CreatedAt datetime2 NOT NULL CONSTRAINT DF_EmployeeContractTemplates_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt datetime2 NULL,
        CONSTRAINT PK_EmployeeContractTemplates PRIMARY KEY (ContractTemplateId)
    );

    CREATE INDEX IX_EmployeeContractTemplates_IsActive ON dbo.EmployeeContractTemplates(IsActive);
END;

IF OBJECT_ID('dbo.EmployeeContracts', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmployeeContracts
    (
        EmployeeContractId int IDENTITY(1,1) NOT NULL,
        EmployeeId int NOT NULL,
        ContractTemplateId int NULL,
        ContractNumber nvarchar(120) NULL,
        ContractType nvarchar(80) NULL,
        ContractStatus nvarchar(50) NOT NULL CONSTRAINT DF_EmployeeContracts_ContractStatus DEFAULT N'Active',
        ContractStartDate datetime2 NULL,
        ContractEndDate datetime2 NULL,
        ContractSignedDate datetime2 NULL,
        ContractNotes nvarchar(max) NULL,
        ContractFilePath nvarchar(500) NULL,
        ContractText nvarchar(max) NULL,
        CreatedAt datetime2 NOT NULL CONSTRAINT DF_EmployeeContracts_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt datetime2 NULL,
        CONSTRAINT PK_EmployeeContracts PRIMARY KEY (EmployeeContractId),
        CONSTRAINT FK_EmployeeContracts_Employees FOREIGN KEY (EmployeeId) REFERENCES dbo.Employees(EmployeeID),
        CONSTRAINT FK_EmployeeContracts_ContractTemplates FOREIGN KEY (ContractTemplateId) REFERENCES dbo.EmployeeContractTemplates(ContractTemplateId),
        CONSTRAINT UQ_EmployeeContracts_EmployeeId UNIQUE (EmployeeId),
        CONSTRAINT CK_EmployeeContracts_Status CHECK (ContractStatus IN (N'Active', N'Terminated', N'Suspended', N'Expired'))
    );

    CREATE INDEX IX_EmployeeContracts_EmployeeId ON dbo.EmployeeContracts(EmployeeId);
    CREATE INDEX IX_EmployeeContracts_Status ON dbo.EmployeeContracts(ContractStatus);
    CREATE INDEX IX_EmployeeContracts_ContractTemplateId ON dbo.EmployeeContracts(ContractTemplateId);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.EmployeeContractTemplates)
BEGIN
    INSERT INTO dbo.EmployeeContractTemplates (TemplateName, ContractType, IsActive, TemplateText, CreatedAt, UpdatedAt)
    VALUES (
        N'قالب عقد موظف أساسي',
        N'Standard',
        1,
        N'عقد عمل

تم الاتفاق بين:

الطرف الأول:
{OrganizationName}

والطرف الثاني:

{EmployeeName}

رقم الهوية:
{NationalId}

رقم الموظف:
{EmployeeCode}

على أن يعمل الطرف الثاني لدى الطرف الأول بوظيفة:

{JobTitle}

ضمن قسم:

{Department}

اعتباراً من تاريخ:

{ContractStartDate}

وذلك وفق الشروط التالية:

1- يلتزم الموظف بأداء المهام الوظيفية الموكلة إليه وفق أنظمة المؤسسة.

2- يلتزم الموظف بالمحافظة على ممتلكات المؤسسة وأسرار العمل والبيانات الخاصة بالعملاء.

3- يلتزم الموظف بساعات الدوام والتعليمات والإجراءات المعتمدة.

4- للمؤسسة الحق في متابعة الأداء والانضباط والإنتاجية وفق السياسات المعتمدة.

5- يحق للمؤسسة اتخاذ الإجراءات النظامية في حالات الإهمال أو المخالفات الجسيمة أو الإضرار المتعمد.

6- يتم احتساب الاستحقاقات المالية وفق نوع الأجر المحدد للموظف.

7- إذا كان الموظف براتب أساسي فإن استحقاقه يعتمد على الراتب الأساسي المعتمد له.

8- إذا كان الموظف بالأجر بالقطعة فإن استحقاقه يعتمد على الإنتاج الفعلي المسجل في النظام وفق أسعار القطع المعتمدة له.

9- السلف تخصم تلقائياً من مستحقات الموظف المالية.

10- المصروفات اليومية لموظف الأجر بالقطعة تخصم من استحقاقه.

11- المصروفات اليومية لموظف الراتب الأساسي لا تخصم من راتبه وتعتبر على حساب المؤسسة ما لم يقرر خلاف ذلك لاحقاً.

12- ينتهي هذا العقد بانتهاء مدته أو وفق الإجراءات النظامية المعتمدة من المؤسسة.

توقيع المؤسسة:

____________________

توقيع الموظف:

____________________

التاريخ:

____________________',
        SYSUTCDATETIME(),
        SYSUTCDATETIME()
    );
END;

COMMIT TRANSACTION;
