SET NOCOUNT ON;

-- WARNING: This rollback must not be executed after new operational data is stored in dbo.CustomerMessages.RawCampaignText.
-- Deleting the column would permanently lose business-authored raw campaign text and break the ManualCampaign contract.
-- Only run this rollback in a deliberate, recovery-approved environment with a verified backup and explicit owner approval.

IF DB_NAME() <> N'LUMAR_ERP_ES_VALIDATION'
BEGIN
    THROW 50001, 'Rollback artifact must run only on LUMAR_ERP_ES_VALIDATION.', 1;
END

IF COL_LENGTH(N'dbo.CustomerMessages', N'RawCampaignText') IS NOT NULL
BEGIN
    ALTER TABLE dbo.CustomerMessages DROP COLUMN RawCampaignText;
END

PRINT 'RAW_CAMPAIGN_TEXT_PERSISTENCE_ROLLBACK: OK';
