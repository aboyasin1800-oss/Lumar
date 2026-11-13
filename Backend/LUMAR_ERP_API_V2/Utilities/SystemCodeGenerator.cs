using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.Utilities;

public static class SystemCodeGenerator
{
    public static async Task<string> ResolveImportedProductCodePrefixAsync(
        SqlConnection connection,
        SqlTransaction? transaction,
        CancellationToken cancellationToken)
    {
        foreach (var settingName in new[] { "ImportedProductCodePrefix", "ImportedReadyMadeProductCodePrefix", "ProductCodePrefix" })
        {
            var current = await ReadSettingAsync(connection, transaction, settingName, cancellationToken);
            if (!string.IsNullOrWhiteSpace(current))
            {
                return current.Trim();
            }
        }

        const string fallback = "AB-";
        foreach (var settingName in new[] { "ImportedProductCodePrefix", "ImportedReadyMadeProductCodePrefix", "ProductCodePrefix" })
        {
            await EnsureSettingAsync(connection, transaction, settingName, fallback, GetDescription(settingName), cancellationToken);
        }

        return fallback;
    }

    public static async Task<string> ResolvePrefixAsync(
        SqlConnection connection,
        SqlTransaction? transaction,
        string settingName,
        string defaultPrefix,
        CancellationToken cancellationToken)
    {
        var current = await ReadSettingAsync(connection, transaction, settingName, cancellationToken);
        if (!string.IsNullOrWhiteSpace(current))
        {
            return current.Trim();
        }

        var fallback = string.IsNullOrWhiteSpace(defaultPrefix) ? "" : defaultPrefix.Trim();
        await EnsureSettingAsync(connection, transaction, settingName, fallback, GetDescription(settingName), cancellationToken);
        return fallback;
    }

    public static async Task<int> GetNextNumberAsync(
        SqlConnection connection,
        SqlTransaction? transaction,
        string tableName,
        string columnName,
        string settingName,
        string defaultPrefix,
        CancellationToken cancellationToken)
    {
        var prefix = await ResolvePrefixAsync(connection, transaction, settingName, defaultPrefix, cancellationToken);
        var sql = $"SELECT ISNULL(MAX(TRY_CONVERT(int,SUBSTRING({columnName},{prefix.Length + 1},20))),0)+1 FROM {tableName} WITH (TABLOCKX,HOLDLOCK) WHERE {columnName} LIKE @prefix";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@prefix", $"{prefix}%");
        return (int)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    public static async Task<string> GetNextAllocatedCatalogNumberAsync(
        SqlConnection connection,
        SqlTransaction? transaction,
        CancellationToken cancellationToken)
    {
        var prefix = await ResolvePrefixAsync(connection, transaction, "CatalogNumberPrefix", "CAT", cancellationToken);
        var normalizedPrefix = NormalizeGlobalPrefix(prefix);
        var pattern = $"{normalizedPrefix}%";
        const string sql = @"
            SELECT ISNULL(MAX(value), 0) + 1
            FROM (
                SELECT TRY_CONVERT(bigint, SUBSTRING(i.Barcode, @prefixLength + 1, 50)) AS value
                FROM dbo.InventoryItems i WITH (TABLOCKX, HOLDLOCK)
                WHERE i.Barcode IS NOT NULL
                  AND i.Barcode LIKE @pattern
            ) x;";

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@pattern", pattern);
        command.Parameters.AddWithValue("@prefixLength", normalizedPrefix.Length);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        var nextValue = result is DBNull or null ? 1L : Convert.ToInt64(result);
        return $"{normalizedPrefix}{nextValue:D4}";
    }

    public static async Task<string> GetNextAllocatedFabricCodeAsync(
        SqlConnection connection,
        SqlTransaction? transaction,
        CancellationToken cancellationToken)
    {
        var prefix = await ResolvePrefixAsync(connection, transaction, "FabricCodePrefix", "FA", cancellationToken);
        var normalizedPrefix = NormalizeGlobalPrefix(prefix);
        var pattern = $"{normalizedPrefix}%";
        const string sql = @"
            SELECT ISNULL(MAX(value), 0) + 1
            FROM (
                SELECT TRY_CONVERT(bigint, SUBSTRING(i.ItemCode, @prefixLength + 1, 50)) AS value
                FROM dbo.InventoryItems i WITH (TABLOCKX, HOLDLOCK)
                WHERE i.ItemCode IS NOT NULL
                  AND i.ItemCode LIKE @pattern
                UNION ALL
                SELECT TRY_CONVERT(bigint, SUBSTRING(fr.RollCode, @prefixLength + 1, 50)) AS value
                FROM dbo.FabricRolls fr WITH (TABLOCKX, HOLDLOCK)
                WHERE fr.RollCode IS NOT NULL
                  AND fr.RollCode LIKE @pattern
            ) x;";

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@pattern", pattern);
        command.Parameters.AddWithValue("@prefixLength", normalizedPrefix.Length);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        var nextValue = result is DBNull or null ? 1L : Convert.ToInt64(result);
        return $"{normalizedPrefix}{nextValue:D4}";
    }

    public static async Task<string> AllocateNextGlobalCodeAsync(
        SqlConnection connection,
        SqlTransaction? transaction,
        string allocationScope,
        string settingName,
        string defaultPrefix,
        CancellationToken cancellationToken)
    {
        var prefix = await ResolvePrefixAsync(connection, transaction, settingName, defaultPrefix, cancellationToken);
        var normalizedPrefix = NormalizeGlobalPrefix(prefix);

        var scope = allocationScope.Trim();
        var pattern = $"{normalizedPrefix}%";
        var sql = scope.Equals("CatalogNumber", StringComparison.OrdinalIgnoreCase)
            ? @"
                SELECT ISNULL(MAX(value), 0) + 1
                FROM (
                    SELECT TRY_CONVERT(bigint, SUBSTRING(i.Barcode, @prefixLength + 1, 50)) AS value
                    FROM dbo.InventoryItems i WITH (TABLOCKX, HOLDLOCK)
                    WHERE i.Barcode IS NOT NULL
                      AND i.Barcode LIKE @pattern
                ) x;"
            : @"
                SELECT ISNULL(MAX(value), 0) + 1
                FROM (
                    SELECT TRY_CONVERT(bigint, SUBSTRING(i.ItemCode, @prefixLength + 1, 50)) AS value
                    FROM dbo.InventoryItems i WITH (TABLOCKX, HOLDLOCK)
                    WHERE i.ItemCode IS NOT NULL
                      AND i.ItemCode LIKE @pattern
                    UNION ALL
                    SELECT TRY_CONVERT(bigint, SUBSTRING(fr.RollCode, @prefixLength + 1, 50)) AS value
                    FROM dbo.FabricRolls fr WITH (TABLOCKX, HOLDLOCK)
                    WHERE fr.RollCode IS NOT NULL
                      AND fr.RollCode LIKE @pattern
                ) x;";

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@pattern", pattern);
        command.Parameters.AddWithValue("@prefixLength", normalizedPrefix.Length);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        var nextValue = result is DBNull or null ? 1L : Convert.ToInt64(result);
        return $"{normalizedPrefix}{nextValue:D4}";
    }

    private static string NormalizeGlobalPrefix(string? prefix)
    {
        var value = prefix?.Trim() ?? string.Empty;
        return string.IsNullOrWhiteSpace(value) ? "FA" : value.Trim();
    }

    private static async Task<string?> ReadSettingAsync(SqlConnection connection, SqlTransaction? transaction, string settingName, CancellationToken cancellationToken)
    {
        const string sql = "SELECT TOP (1) SettingValue FROM dbo.System_Settings WITH (NOLOCK) WHERE SettingName = @name";
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@name", settingName);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is DBNull or null ? null : value.ToString();
    }

    private static async Task EnsureSettingAsync(
        SqlConnection connection,
        SqlTransaction? transaction,
        string settingName,
        string settingValue,
        string description,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settingName))
        {
            return;
        }

        const string sql = @"
            INSERT INTO dbo.System_Settings (SettingName, SettingValue, Description)
            SELECT @name, @value, @description
            WHERE NOT EXISTS (
                SELECT 1
                FROM dbo.System_Settings WITH (NOLOCK)
                WHERE SettingName = @name
            );";

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@name", settingName);
        command.Parameters.AddWithValue("@value", settingValue ?? string.Empty);
        command.Parameters.AddWithValue("@description", description);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string GetDescription(string settingName) => settingName switch
    {
        "FabricCodePrefix" => "Fabric code prefix used for new fabric identifiers.",
        "CatalogNumberPrefix" => "Catalog number prefix used for new catalog records.",
        "CustomerCodePrefix" => "Customer code prefix used for new customer records.",
        "EmployeeCodePrefix" => "Employee code prefix used for new employee records.",
        "ImportedProductCodePrefix" => "Imported ready-made product code prefix used for canonical product identity.",
        "ImportedReadyMadeProductCodePrefix" => "Imported ready-made product code prefix used for canonical product identity.",
        "OrderCodePrefix" => "Order code prefix used for new order numbers.",
        "PieceTrackingPrefix" => "Piece tracking code prefix used for production pieces.",
        "ProductionTrackingPrefix" => "Production order tracking prefix used for ready-made production orders.",
        "ProductCodePrefix" => "Generic product code prefix used for canonical product identity.",
        "ToolCodePrefix" => "Inventory tool code prefix used for new tool entries.",
        _ => $"Auto-generated code prefix for {settingName}."
    };
}
