SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF EXISTS (
    SELECT 1
    FROM dbo.System_Settings
    WHERE SettingName = N'CustomerCodePrefix'
      AND NULLIF(LTRIM(RTRIM(SettingValue)), N'') IS NULL
)
BEGIN
    UPDATE dbo.System_Settings
    SET SettingValue = N'C'
    WHERE SettingName = N'CustomerCodePrefix'
      AND NULLIF(LTRIM(RTRIM(SettingValue)), N'') IS NULL;
END
ELSE IF NOT EXISTS (
    SELECT 1
    FROM dbo.System_Settings
    WHERE SettingName = N'CustomerCodePrefix'
)
BEGIN
    INSERT INTO dbo.System_Settings (SettingName, SettingValue, Description)
    VALUES (N'CustomerCodePrefix', N'C', N'Customer code prefix used for new customer records.');
END

COMMIT TRANSACTION;
