:setvar ApplyChanges 1
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;

DECLARE @CurrentDatabase sysname = DB_NAME();
DECLARE @MigrationName nvarchar(200) = N'20261110_R3_GLOBAL_FABRIC_CATALOG_IDENTITY_ALLOCATION_01';
DECLARE @ApplyChanges bit = $(ApplyChanges);

IF @CurrentDatabase NOT IN (N'LUMAR_ERP_ES_VALIDATION_R3', N'LUMAR_ERP_TEST_R3_ROLLS')
BEGIN
    THROW 51100, N'Target database is not approved for this migration.', 1;
END;

IF OBJECT_ID(N'dbo.InventoryItems', N'U') IS NULL
BEGIN
    THROW 51101, N'dbo.InventoryItems is required but was not found.', 1;
END;

IF OBJECT_ID(N'dbo.FabricRolls', N'U') IS NULL
BEGIN
    THROW 51102, N'dbo.FabricRolls is required but was not found.', 1;
END;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @ApplicationLockResult int;
    EXEC @ApplicationLockResult = sys.sp_getapplock
        @Resource = N'LUMAR_GLOBAL_FABRIC_CATALOG_IDENTITY_MIGRATION',
        @LockMode = N'Exclusive',
        @LockOwner = N'Transaction',
        @LockTimeout = 15000;

    IF @ApplicationLockResult < 0
    BEGIN
        THROW 51103, N'Could not obtain the exclusive migration lock.', 1;
    END;

    IF COL_LENGTH(N'dbo.GoodsReceiptItems', N'CatalogNumber') IS NOT NULL
    BEGIN
        IF EXISTS (
            SELECT 1
            FROM sys.indexes
            WHERE object_id = OBJECT_ID(N'dbo.GoodsReceiptItems')
              AND name = N'UX_GoodsReceiptItems_CatalogNumber'
        )
        BEGIN
            DECLARE @CatalogIndexIsUnique bit;
            DECLARE @CatalogIndexHasFilter bit;
            DECLARE @CatalogIndexFilter nvarchar(max);

            SELECT
                @CatalogIndexIsUnique = i.is_unique,
                @CatalogIndexHasFilter = i.has_filter,
                @CatalogIndexFilter = i.filter_definition
            FROM sys.indexes AS i
            WHERE i.object_id = OBJECT_ID(N'dbo.GoodsReceiptItems')
              AND i.name = N'UX_GoodsReceiptItems_CatalogNumber';

            IF @CatalogIndexIsUnique <> 1 OR @CatalogIndexHasFilter <> 1 OR @CatalogIndexFilter IS NULL
            BEGIN
                THROW 51104, N'Existing GoodsReceiptItems.CatalogNumber index does not match the required filtered unique contract.', 1;
            END;
        END;
    END;

    DECLARE @InvalidInventoryItemFaCount bigint = 0;
    DECLARE @InvalidFabricRollFaCount bigint = 0;
    DECLARE @DuplicateInventoryItemFaCount bigint = 0;
    DECLARE @DuplicateFabricRollFaCount bigint = 0;
    DECLARE @FabricItemRollMismatchCount bigint = 0;
    DECLARE @InvalidCatalogCount bigint = 0;
    DECLARE @DuplicateCatalogCount bigint = 0;
    DECLARE @MaxInventoryItemFa bigint = 0;
    DECLARE @MaxFabricRollFa bigint = 0;
    DECLARE @MaxInventoryCatalog bigint = 0;
    DECLARE @MaxGoodsReceiptCatalog bigint = 0;
    DECLARE @GlobalMaxFa bigint = 0;
    DECLARE @GlobalMaxCatalog bigint = 0;
    DECLARE @FabricCurrentValue bigint = 0;
    DECLARE @CatalogCurrentValue bigint = 0;

    SELECT @InvalidInventoryItemFaCount = COUNT_BIG(*)
    FROM dbo.InventoryItems
    WHERE ItemCode LIKE N'FA%'
      AND (
          LEN(ItemCode) <= 2
          OR TRY_CONVERT(bigint, SUBSTRING(ItemCode, 3, 100)) IS NULL
          OR TRY_CONVERT(bigint, SUBSTRING(ItemCode, 3, 100)) < 1
      );

    SELECT @InvalidFabricRollFaCount = COUNT_BIG(*)
    FROM dbo.FabricRolls
    WHERE RollCode LIKE N'FA%'
      AND (
          LEN(RollCode) <= 2
          OR TRY_CONVERT(bigint, SUBSTRING(RollCode, 3, 100)) IS NULL
          OR TRY_CONVERT(bigint, SUBSTRING(RollCode, 3, 100)) < 1
      );

    SELECT @DuplicateInventoryItemFaCount = COUNT_BIG(*)
    FROM (
        SELECT ItemCode
        FROM dbo.InventoryItems
        WHERE ItemCode LIKE N'FA%'
        GROUP BY ItemCode
        HAVING COUNT_BIG(*) > 1
    ) AS DuplicateInventoryItems;

    SELECT @DuplicateFabricRollFaCount = COUNT_BIG(*)
    FROM (
        SELECT RollCode
        FROM dbo.FabricRolls
        WHERE RollCode LIKE N'FA%'
        GROUP BY RollCode
        HAVING COUNT_BIG(*) > 1
    ) AS DuplicateFabricRolls;

    SELECT @FabricItemRollMismatchCount = COUNT_BIG(*)
    FROM dbo.FabricRolls AS fr
    INNER JOIN dbo.InventoryItems AS ii ON ii.InventoryItemID = fr.InventoryItemId
    WHERE (fr.RollCode LIKE N'FA%' OR ii.ItemCode LIKE N'FA%')
      AND ISNULL(fr.RollCode, N'') <> ISNULL(ii.ItemCode, N'');

    SELECT @InvalidCatalogCount = COUNT_BIG(*)
    FROM dbo.InventoryItems
    WHERE Barcode IS NOT NULL
      AND Barcode LIKE N'CAT%'
      AND (
          LTRIM(RTRIM(Barcode)) = N''
          OR LEN(Barcode) <= 3
          OR TRY_CONVERT(bigint, SUBSTRING(Barcode, 4, 100)) IS NULL
          OR TRY_CONVERT(bigint, SUBSTRING(Barcode, 4, 100)) < 1
      );

    SELECT @DuplicateCatalogCount = COUNT_BIG(*)
    FROM (
        SELECT Barcode
        FROM dbo.InventoryItems
        WHERE Barcode IS NOT NULL
          AND Barcode LIKE N'CAT%'
        GROUP BY Barcode
        HAVING COUNT_BIG(*) > 1
    ) AS DuplicateCatalogs;

    IF @InvalidInventoryItemFaCount > 0
    BEGIN
        THROW 51105, N'Invalid FA identity format exists in dbo.InventoryItems.', 1;
    END;

    IF @InvalidFabricRollFaCount > 0
    BEGIN
        THROW 51106, N'Invalid FA identity format exists in dbo.FabricRolls.', 1;
    END;

    IF @DuplicateInventoryItemFaCount > 0
    BEGIN
        THROW 51107, N'Duplicate FA identities exist inside dbo.InventoryItems.', 1;
    END;

    IF @DuplicateFabricRollFaCount > 0
    BEGIN
        THROW 51108, N'Duplicate FA identities exist inside dbo.FabricRolls.', 1;
    END;

    IF @FabricItemRollMismatchCount > 0
    BEGIN
        THROW 51109, N'At least one FabricRoll.RollCode does not match its InventoryItem.ItemCode.', 1;
    END;

    IF @InvalidCatalogCount > 0
    BEGIN
        THROW 51110, N'Invalid CAT identity format exists in dbo.InventoryItems.Barcode.', 1;
    END;

    IF @DuplicateCatalogCount > 0
    BEGIN
        THROW 51111, N'Duplicate CAT identities exist inside dbo.InventoryItems.Barcode.', 1;
    END;

    IF OBJECT_ID(N'dbo.GlobalCodeAllocations', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.GlobalCodeAllocations (
            AllocationScope nvarchar(100) NOT NULL PRIMARY KEY,
            Prefix nvarchar(30) NOT NULL,
            CurrentValue bigint NOT NULL CONSTRAINT DF_GlobalCodeAllocations_CurrentValue DEFAULT (0),
            CreatedAt datetime2(3) NOT NULL CONSTRAINT DF_GlobalCodeAllocations_CreatedAt DEFAULT (SYSUTCDATETIME()),
            UpdatedAt datetime2(3) NOT NULL CONSTRAINT DF_GlobalCodeAllocations_UpdatedAt DEFAULT (SYSUTCDATETIME())
        );
    END;

    SELECT @MaxInventoryItemFa = ISNULL(MAX(TRY_CONVERT(bigint, SUBSTRING(ItemCode, 3, 100))), 0)
    FROM dbo.InventoryItems
    WHERE ItemCode LIKE N'FA%';

    SELECT @MaxFabricRollFa = ISNULL(MAX(TRY_CONVERT(bigint, SUBSTRING(RollCode, 3, 100))), 0)
    FROM dbo.FabricRolls
    WHERE RollCode LIKE N'FA%';

    SELECT @MaxInventoryCatalog = ISNULL(MAX(TRY_CONVERT(bigint, SUBSTRING(Barcode, 4, 100))), 0)
    FROM dbo.InventoryItems
    WHERE Barcode LIKE N'CAT%';

    IF OBJECT_ID(N'dbo.GoodsReceiptItems', N'U') IS NOT NULL
       AND COL_LENGTH(N'dbo.GoodsReceiptItems', N'CatalogNumber') IS NOT NULL
    BEGIN
        EXEC sys.sp_executesql
            N'SELECT @MaxGoodsReceiptCatalog = ISNULL(MAX(TRY_CONVERT(bigint, SUBSTRING(CatalogNumber, 4, 100))), 0)
              FROM dbo.GoodsReceiptItems
              WHERE CatalogNumber LIKE N''CAT%'';',
            N'@MaxGoodsReceiptCatalog bigint OUTPUT',
            @MaxGoodsReceiptCatalog = @MaxGoodsReceiptCatalog OUTPUT;
    END;

    SET @GlobalMaxFa = (
        SELECT MAX(ValueNumber)
        FROM (
            SELECT @MaxInventoryItemFa AS ValueNumber
            UNION ALL
            SELECT @MaxFabricRollFa
        ) AS FabricMaximums
    );

    SET @GlobalMaxCatalog = (
        SELECT MAX(ValueNumber)
        FROM (
            SELECT @MaxInventoryCatalog AS ValueNumber
            UNION ALL
            SELECT @MaxGoodsReceiptCatalog
        ) AS CatalogMaximums
    );

    IF EXISTS (
        SELECT 1
        FROM dbo.GlobalCodeAllocations
        WHERE AllocationScope = N'FabricCode'
          AND Prefix <> N'FA'
    )
    BEGIN
        THROW 51112, N'Existing FabricCode allocation scope uses an incompatible prefix.', 1;
    END;

    IF EXISTS (
        SELECT 1
        FROM dbo.GlobalCodeAllocations
        WHERE AllocationScope = N'CatalogNumber'
          AND Prefix <> N'CAT'
    )
    BEGIN
        THROW 51113, N'Existing CatalogNumber allocation scope uses an incompatible prefix.', 1;
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.GlobalCodeAllocations WITH (UPDLOCK, HOLDLOCK) WHERE AllocationScope = N'FabricCode')
    BEGIN
        INSERT INTO dbo.GlobalCodeAllocations (AllocationScope, Prefix, CurrentValue, CreatedAt, UpdatedAt)
        VALUES (N'FabricCode', N'FA', @GlobalMaxFa, SYSUTCDATETIME(), SYSUTCDATETIME());
    END
    ELSE
    BEGIN
        UPDATE dbo.GlobalCodeAllocations WITH (UPDLOCK, HOLDLOCK)
        SET Prefix = N'FA',
            CurrentValue = CASE WHEN CurrentValue < @GlobalMaxFa THEN @GlobalMaxFa ELSE CurrentValue END,
            UpdatedAt = SYSUTCDATETIME()
        WHERE AllocationScope = N'FabricCode';
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.GlobalCodeAllocations WITH (UPDLOCK, HOLDLOCK) WHERE AllocationScope = N'CatalogNumber')
    BEGIN
        INSERT INTO dbo.GlobalCodeAllocations (AllocationScope, Prefix, CurrentValue, CreatedAt, UpdatedAt)
        VALUES (N'CatalogNumber', N'CAT', @GlobalMaxCatalog, SYSUTCDATETIME(), SYSUTCDATETIME());
    END
    ELSE
    BEGIN
        UPDATE dbo.GlobalCodeAllocations WITH (UPDLOCK, HOLDLOCK)
        SET Prefix = N'CAT',
            CurrentValue = CASE WHEN CurrentValue < @GlobalMaxCatalog THEN @GlobalMaxCatalog ELSE CurrentValue END,
            UpdatedAt = SYSUTCDATETIME()
        WHERE AllocationScope = N'CatalogNumber';
    END;

    SELECT @FabricCurrentValue = CurrentValue
    FROM dbo.GlobalCodeAllocations
    WHERE AllocationScope = N'FabricCode';

    SELECT @CatalogCurrentValue = CurrentValue
    FROM dbo.GlobalCodeAllocations
    WHERE AllocationScope = N'CatalogNumber';

    IF @FabricCurrentValue IS NULL OR @FabricCurrentValue < @GlobalMaxFa
    BEGIN
        THROW 51114, N'FabricCode allocation counter is behind existing FA identities.', 1;
    END;

    IF @CatalogCurrentValue IS NULL OR @CatalogCurrentValue < @GlobalMaxCatalog
    BEGIN
        THROW 51115, N'CatalogNumber allocation counter is behind existing CAT identities.', 1;
    END;

    SELECT
        @MigrationName AS MigrationName,
        @CurrentDatabase AS DatabaseName,
        @ApplyChanges AS ApplyChanges,
        @InvalidInventoryItemFaCount AS InvalidInventoryItemFaCount,
        @InvalidFabricRollFaCount AS InvalidFabricRollFaCount,
        @DuplicateInventoryItemFaCount AS DuplicateInventoryItemFaCount,
        @DuplicateFabricRollFaCount AS DuplicateFabricRollFaCount,
        @FabricItemRollMismatchCount AS FabricItemRollMismatchCount,
        @InvalidCatalogCount AS InvalidCatalogCount,
        @DuplicateCatalogCount AS DuplicateCatalogCount,
        @MaxInventoryItemFa AS MaxInventoryItemFa,
        @MaxFabricRollFa AS MaxFabricRollFa,
        @MaxInventoryCatalog AS MaxInventoryCatalog,
        @MaxGoodsReceiptCatalog AS MaxGoodsReceiptCatalog,
        @GlobalMaxFa AS GlobalMaxExistingFa,
        @GlobalMaxCatalog AS GlobalMaxExistingCatalog,
        @FabricCurrentValue AS FabricCurrentValue,
        N'FA' + CONVERT(nvarchar(30), @FabricCurrentValue + 1) AS FirstExpectedNewFabricCode,
        @CatalogCurrentValue AS CatalogCurrentValue,
        N'CAT' + CONVERT(nvarchar(30), @CatalogCurrentValue + 1) AS FirstExpectedNewCatalogNumber;

    SELECT AllocationScope, Prefix, CurrentValue, CreatedAt, UpdatedAt
    FROM dbo.GlobalCodeAllocations
    ORDER BY AllocationScope;

    IF @ApplyChanges = 1
    BEGIN
        COMMIT TRANSACTION;
        SELECT N'APPLIED' AS MigrationStatus, @CurrentDatabase AS DatabaseName, @MigrationName AS MigrationName;
    END
    ELSE
    BEGIN
        ROLLBACK TRANSACTION;
        SELECT N'PREVIEW_ONLY_ROLLED_BACK' AS MigrationStatus, @CurrentDatabase AS DatabaseName, @MigrationName AS MigrationName;
    END;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
