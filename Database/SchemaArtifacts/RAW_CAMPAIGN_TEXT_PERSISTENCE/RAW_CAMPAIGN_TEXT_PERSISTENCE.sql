SET NOCOUNT ON;

IF DB_NAME() <> N'LUMAR_ERP_ES_VALIDATION'
BEGIN
    THROW 50001, 'Artifact must run only on LUMAR_ERP_ES_VALIDATION.', 1;
END

IF OBJECT_ID(N'dbo.CustomerMessages', N'U') IS NULL
BEGIN
    THROW 50002, 'dbo.CustomerMessages does not exist in LUMAR_ERP_ES_VALIDATION.', 1;
END

IF COL_LENGTH(N'dbo.CustomerMessages', N'RawCampaignText') IS NULL
BEGIN
    ALTER TABLE dbo.CustomerMessages ADD RawCampaignText NVARCHAR(MAX) NULL;
END

IF EXISTS (
    SELECT 1
    FROM sys.columns c
    JOIN sys.tables t ON t.object_id = c.object_id
    WHERE t.name = N'CustomerMessages'
      AND SCHEMA_NAME(t.schema_id) = N'dbo'
      AND c.name = N'RawCampaignText'
      AND c.is_nullable <> 1
)
BEGIN
    THROW 50003, 'Verification failed: dbo.CustomerMessages.RawCampaignText must remain nullable.', 1;
END

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns c
    JOIN sys.tables t ON t.object_id = c.object_id
    WHERE t.name = N'CustomerMessages'
      AND SCHEMA_NAME(t.schema_id) = N'dbo'
      AND c.name = N'RawCampaignText'
)
BEGIN
    THROW 50004, 'Verification failed: dbo.CustomerMessages.RawCampaignText was not created.', 1;
END

PRINT 'RAW_CAMPAIGN_TEXT_PERSISTENCE: OK';
