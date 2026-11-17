SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRAN;

    IF OBJECT_ID('dbo.PricingProductTypes', 'U') IS NULL
        OR OBJECT_ID('dbo.PricingMeasurementProfiles', 'U') IS NULL
        OR OBJECT_ID('dbo.PricingMeasurementFields', 'U') IS NULL
        OR OBJECT_ID('dbo.PricingSizeClasses', 'U') IS NULL
        OR OBJECT_ID('dbo.PricingConsumptionRules', 'U') IS NULL
        OR OBJECT_ID('dbo.PricingCostItems', 'U') IS NULL
        OR OBJECT_ID('dbo.PricingCostMatrices', 'U') IS NULL
        OR OBJECT_ID('dbo.PricingCostMatrixItems', 'U') IS NULL
        OR OBJECT_ID('dbo.PricingMarginPolicies', 'U') IS NULL
    BEGIN
        RAISERROR('Required target table is missing.', 16, 1);
    END;

    IF EXISTS (SELECT 1 FROM dbo.PricingProductTypes WHERE ProductTypeId BETWEEN 1 AND 13)
        OR EXISTS (SELECT 1 FROM dbo.PricingMeasurementProfiles WHERE ProductTypeId BETWEEN 1 AND 13)
        OR EXISTS (SELECT 1 FROM dbo.PricingMeasurementFields WHERE MeasurementProfileId IN (SELECT MeasurementProfileId FROM dbo.PricingMeasurementProfiles WHERE ProductTypeId BETWEEN 1 AND 13))
        OR EXISTS (SELECT 1 FROM dbo.PricingSizeClasses WHERE ProductTypeId BETWEEN 1 AND 13)
        OR EXISTS (SELECT 1 FROM dbo.PricingConsumptionRules WHERE ProductTypeId BETWEEN 1 AND 13)
        OR EXISTS (SELECT 1 FROM dbo.PricingCostMatrices WHERE ProductTypeId BETWEEN 1 AND 13)
        OR EXISTS (SELECT 1 FROM dbo.PricingCostMatrixItems WHERE CostMatrixId IN (SELECT CostMatrixId FROM dbo.PricingCostMatrices WHERE ProductTypeId BETWEEN 1 AND 13))
        OR EXISTS (SELECT 1 FROM dbo.PricingMarginPolicies WHERE ProductTypeId BETWEEN 1 AND 13)
    BEGIN
        RAISERROR('Target already contains approved product-type reference data. Refusing to overwrite.', 16, 1);
    END;

    DECLARE @sourceApprovedCount INT = (SELECT COUNT(*) FROM [LUMAR_ERP_ES_VALIDATION].[dbo].[PricingProductTypes] WHERE ProductTypeId BETWEEN 1 AND 13);
    IF @sourceApprovedCount <> 13
    BEGIN
        RAISERROR('Source approved product-type count does not match the required 13 rows.', 16, 1);
    END;

    SET IDENTITY_INSERT dbo.PricingProductTypes ON;
    INSERT INTO dbo.PricingProductTypes (
        ProductTypeId, Code, NameAr, NameEn, Category, Scope, IsActive, CreatedAt, UpdatedAt
    )
    SELECT
        ProductTypeId, Code, NameAr, NameEn, Category, Scope, IsActive, CreatedAt, UpdatedAt
    FROM [LUMAR_ERP_ES_VALIDATION].[dbo].[PricingProductTypes]
    WHERE ProductTypeId BETWEEN 1 AND 13;
    SET IDENTITY_INSERT dbo.PricingProductTypes OFF;

    SET IDENTITY_INSERT dbo.PricingMeasurementProfiles ON;
    INSERT INTO dbo.PricingMeasurementProfiles (
        MeasurementProfileId, ProductTypeId, Name, Version, Status, EffectiveFrom, EffectiveTo, IsActive, ChangeReason, CreatedBy, CreatedAt, UpdatedAt
    )
    SELECT
        MeasurementProfileId, ProductTypeId, Name, Version, Status, EffectiveFrom, EffectiveTo, IsActive, ChangeReason, CreatedBy, CreatedAt, UpdatedAt
    FROM [LUMAR_ERP_ES_VALIDATION].[dbo].[PricingMeasurementProfiles]
    WHERE ProductTypeId BETWEEN 1 AND 13;
    SET IDENTITY_INSERT dbo.PricingMeasurementProfiles OFF;

    SET IDENTITY_INSERT dbo.PricingSizeClasses ON;
    INSERT INTO dbo.PricingSizeClasses (
        SizeClassId, ProductTypeId, Code, NameAr, MeasurementCode, MinimumValue, MaximumValue, Sequence, Version, Status, EffectiveFrom, EffectiveTo, IsActive, ChangeReason, CreatedBy, CreatedAt, UpdatedAt
    )
    SELECT
        SizeClassId, ProductTypeId, Code, NameAr, MeasurementCode, MinimumValue, MaximumValue, Sequence, Version, Status, EffectiveFrom, EffectiveTo, IsActive, ChangeReason, CreatedBy, CreatedAt, UpdatedAt
    FROM [LUMAR_ERP_ES_VALIDATION].[dbo].[PricingSizeClasses]
    WHERE ProductTypeId BETWEEN 1 AND 13;
    SET IDENTITY_INSERT dbo.PricingSizeClasses OFF;

    SET IDENTITY_INSERT dbo.PricingMeasurementFields ON;
    INSERT INTO dbo.PricingMeasurementFields (
        MeasurementFieldId, MeasurementProfileId, Code, NameAr, Unit, IsRequired, Sequence
    )
    SELECT
        m.MeasurementFieldId, m.MeasurementProfileId, m.Code, m.NameAr, m.Unit, m.IsRequired, m.Sequence
    FROM [LUMAR_ERP_ES_VALIDATION].[dbo].[PricingMeasurementFields] AS m
    INNER JOIN [LUMAR_ERP_ES_VALIDATION].[dbo].[PricingMeasurementProfiles] AS p
        ON p.MeasurementProfileId = m.MeasurementProfileId
    WHERE p.ProductTypeId BETWEEN 1 AND 13;
    SET IDENTITY_INSERT dbo.PricingMeasurementFields OFF;

    SET IDENTITY_INSERT dbo.PricingConsumptionRules ON;
    INSERT INTO dbo.PricingConsumptionRules (
        ConsumptionRuleId, ProductTypeId, SizeClassId, Name, RuleType, Formula, ResultUnit, FabricWidth, FabricWidthUnit, InheritsFromProductTypeId, Priority, Version, Status, EffectiveFrom, EffectiveTo, IsActive, ChangeReason, CreatedBy, CreatedAt, UpdatedAt
    )
    SELECT
        ConsumptionRuleId, ProductTypeId, SizeClassId, Name, RuleType, Formula, ResultUnit, FabricWidth, FabricWidthUnit, InheritsFromProductTypeId, Priority, Version, Status, EffectiveFrom, EffectiveTo, IsActive, ChangeReason, CreatedBy, CreatedAt, UpdatedAt
    FROM [LUMAR_ERP_ES_VALIDATION].[dbo].[PricingConsumptionRules]
    WHERE ProductTypeId BETWEEN 1 AND 13;
    SET IDENTITY_INSERT dbo.PricingConsumptionRules OFF;

    SET IDENTITY_INSERT dbo.PricingCostItems ON;
    INSERT INTO dbo.PricingCostItems (
        CostItemId, Code, NameAr, Category, CalculationMethod, Unit, IsActive, CreatedAt
    )
    SELECT DISTINCT
        c.CostItemId, c.Code, c.NameAr, c.Category, c.CalculationMethod, c.Unit, c.IsActive, c.CreatedAt
    FROM [LUMAR_ERP_ES_VALIDATION].[dbo].[PricingCostItems] AS c
    INNER JOIN [LUMAR_ERP_ES_VALIDATION].[dbo].[PricingCostMatrixItems] AS mi
        ON mi.CostItemId = c.CostItemId
    INNER JOIN [LUMAR_ERP_ES_VALIDATION].[dbo].[PricingCostMatrices] AS m
        ON m.CostMatrixId = mi.CostMatrixId
    WHERE m.ProductTypeId BETWEEN 1 AND 13;
    SET IDENTITY_INSERT dbo.PricingCostItems OFF;

    SET IDENTITY_INSERT dbo.PricingCostMatrices ON;
    INSERT INTO dbo.PricingCostMatrices (
        CostMatrixId, ProductTypeId, Name, Channel, Version, Status, EffectiveFrom, EffectiveTo, IsActive, ChangeReason, CreatedBy, CreatedAt, UpdatedAt
    )
    SELECT
        CostMatrixId, ProductTypeId, Name, Channel, Version, Status, EffectiveFrom, EffectiveTo, IsActive, ChangeReason, CreatedBy, CreatedAt, UpdatedAt
    FROM [LUMAR_ERP_ES_VALIDATION].[dbo].[PricingCostMatrices]
    WHERE ProductTypeId BETWEEN 1 AND 13;
    SET IDENTITY_INSERT dbo.PricingCostMatrices OFF;

    SET IDENTITY_INSERT dbo.PricingCostMatrixItems ON;
    INSERT INTO dbo.PricingCostMatrixItems (
        CostMatrixItemId, CostMatrixId, CostItemId, Quantity, UnitCost, AllocationPercentage, Sequence
    )
    SELECT
        mi.CostMatrixItemId, mi.CostMatrixId, mi.CostItemId, mi.Quantity, mi.UnitCost, mi.AllocationPercentage, mi.Sequence
    FROM [LUMAR_ERP_ES_VALIDATION].[dbo].[PricingCostMatrixItems] AS mi
    INNER JOIN [LUMAR_ERP_ES_VALIDATION].[dbo].[PricingCostMatrices] AS m
        ON m.CostMatrixId = mi.CostMatrixId
    WHERE m.ProductTypeId BETWEEN 1 AND 13;
    SET IDENTITY_INSERT dbo.PricingCostMatrixItems OFF;

    SET IDENTITY_INSERT dbo.PricingMarginPolicies ON;
    INSERT INTO dbo.PricingMarginPolicies (
        MarginPolicyId, ProductTypeId, Name, Channel, TargetMarginPercentage, MinimumMarginPercentage, MaximumDiscountPercentage, MarkupPercentage, RoundingIncrement, Priority, Version, Status, EffectiveFrom, EffectiveTo, IsActive, ChangeReason, CreatedBy, CreatedAt, UpdatedAt
    )
    SELECT
        MarginPolicyId, ProductTypeId, Name, Channel, TargetMarginPercentage, MinimumMarginPercentage, MaximumDiscountPercentage, MarkupPercentage, RoundingIncrement, Priority, Version, Status, EffectiveFrom, EffectiveTo, IsActive, ChangeReason, CreatedBy, CreatedAt, UpdatedAt
    FROM [LUMAR_ERP_ES_VALIDATION].[dbo].[PricingMarginPolicies]
    WHERE ProductTypeId BETWEEN 1 AND 13;
    SET IDENTITY_INSERT dbo.PricingMarginPolicies OFF;

    COMMIT;

    SELECT 'RESTORE_OK' AS status;
    SELECT COUNT(*) AS product_types_restored FROM dbo.PricingProductTypes WHERE ProductTypeId BETWEEN 1 AND 13;
    SELECT COUNT(*) AS measurement_profiles_restored FROM dbo.PricingMeasurementProfiles WHERE ProductTypeId BETWEEN 1 AND 13;
    SELECT COUNT(*) AS measurement_fields_restored FROM dbo.PricingMeasurementFields WHERE MeasurementProfileId IN (SELECT MeasurementProfileId FROM dbo.PricingMeasurementProfiles WHERE ProductTypeId BETWEEN 1 AND 13);
    SELECT COUNT(*) AS size_classes_restored FROM dbo.PricingSizeClasses WHERE ProductTypeId BETWEEN 1 AND 13;
    SELECT COUNT(*) AS consumption_rules_restored FROM dbo.PricingConsumptionRules WHERE ProductTypeId BETWEEN 1 AND 13;
    SELECT COUNT(*) AS cost_items_restored FROM dbo.PricingCostItems;
    SELECT COUNT(*) AS cost_matrices_restored FROM dbo.PricingCostMatrices WHERE ProductTypeId BETWEEN 1 AND 13;
    SELECT COUNT(*) AS cost_matrix_items_restored FROM dbo.PricingCostMatrixItems WHERE CostMatrixId IN (SELECT CostMatrixId FROM dbo.PricingCostMatrices WHERE ProductTypeId BETWEEN 1 AND 13);
    SELECT COUNT(*) AS margin_policies_restored FROM dbo.PricingMarginPolicies WHERE ProductTypeId BETWEEN 1 AND 13;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK;
    THROW;
END CATCH;
