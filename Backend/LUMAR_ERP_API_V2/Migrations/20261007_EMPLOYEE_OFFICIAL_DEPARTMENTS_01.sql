SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @official TABLE (DepartmentCode nvarchar(50) NOT NULL, DepartmentName nvarchar(200) NOT NULL, Description nvarchar(500) NULL);

    INSERT INTO @official (DepartmentCode, DepartmentName, Description)
    VALUES
        (N'SEW', N'خياط', N'القسم الرسمي - خياط'),
        (N'CUT', N'قصاص', N'القسم الرسمي - قصاص'),
        (N'ACC', N'محاسب', N'القسم الرسمي - محاسب'),
        (N'IRON', N'كواي', N'القسم الرسمي - كواي'),
        (N'QA', N'جودة', N'القسم الرسمي - جودة'),
        (N'ASM', N'تجميع', N'القسم الرسمي - تجميع'),
        (N'RECEPTION', N'استقبال', N'القسم الرسمي - استقبال'),
        (N'GM', N'مدير عام', N'القسم الرسمي - مدير عام'),
        (N'GEN', N'القسم العام', N'القسم الرسمي - القسم العام');

    MERGE dbo.Departments AS target
    USING @official AS source
        ON target.DepartmentCode = source.DepartmentCode
    WHEN MATCHED THEN
        UPDATE SET
            target.DepartmentName = source.DepartmentName,
            target.Description = source.Description,
            target.IsActive = 1,
            target.UpdatedAt = SYSUTCDATETIME()
    WHEN NOT MATCHED THEN
        INSERT (DepartmentCode, DepartmentName, Description, IsActive, CreatedAt, UpdatedAt)
        VALUES (source.DepartmentCode, source.DepartmentName, source.Description, 1, SYSUTCDATETIME(), SYSUTCDATETIME());

    UPDATE d
    SET d.IsActive = 0,
        d.UpdatedAt = SYSUTCDATETIME()
    FROM dbo.Departments AS d
    LEFT JOIN @official AS o ON o.DepartmentCode = d.DepartmentCode
    WHERE d.DepartmentCode IS NOT NULL
      AND o.DepartmentCode IS NULL
      AND d.IsActive = 1;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
