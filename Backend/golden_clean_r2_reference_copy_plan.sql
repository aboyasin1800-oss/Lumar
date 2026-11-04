-- REVIEW-ONLY COPY PLAN FOR LUMAR_ERP_GOLDEN_CLEAN_R2
-- This file is not executed by this task.
-- It documents a safe, ordered, reference-only copy strategy.

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    -- 1. Validate source and target metadata before any bulk copy.
    IF DB_NAME() <> 'LUMAR_ERP_VIP_1'
    BEGIN
        RAISERROR('Source database context must be LUMAR_ERP_VIP_1.',16,1);
    END

    IF EXISTS (SELECT 1 FROM sys.databases WHERE name = 'LUMAR_ERP_GOLDEN_CLEAN_R2')
    BEGIN
        RAISERROR('Target database already exists. This review plan must stop before execution.',16,1);
    END

    -- 2. Copy protected definitions only in foreign-key order.
    --    Table names below are reference points only; actual execution must be validated against the live schema.
    --    Preserved tables: Users, SecurityRoles, SecurityPermissions, RolePermissionMappings, AccountRoleMappings,
    --    System_Settings, MessageTemplates, Messages_Templates, LedgerAccounts, CashAccounts, AccountingEventDefinitions,
    --    PieceWageRates, PricingProductTypes, PricingMeasurementFields, PricingMeasurementProfiles, PricingSizeClasses,
    --    PricingConsumptionRules, PricingMarginPolicies, PricingCostItems, PricingRules, VipLevels,
    --    VipLevelEvaluationCriteria, LoyaltyRules, LoyaltyProgramSettings, LoyaltyPiecePointSettings, InventoryClassCatalog,
    --    InventoryUnitCatalog, EmployeeContractTemplates.

    -- 3. Copy only allowed columns and preserve admin access mapping.
    --    Do not copy password hashes, secrets, tokens, or operational test rows.

    -- 4. Final validation.
    IF EXISTS (
        SELECT 1
        FROM INFORMATION_SCHEMA.TABLES t
        WHERE TABLE_SCHEMA <> 'dbo'
    )
    BEGIN
        RAISERROR('Aborting: unapproved schema objects detected before execution.',16,1);
    END

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
