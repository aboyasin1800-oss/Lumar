using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.Utilities;

public static class SystemCodeGenerator
{
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
        "OrderCodePrefix" => "Order code prefix used for new order numbers.",
        "PieceTrackingPrefix" => "Piece tracking code prefix used for production pieces.",
        "ProductionTrackingPrefix" => "Production order tracking prefix used for ready-made production orders.",
        "ToolCodePrefix" => "Inventory tool code prefix used for new tool entries.",
        _ => $"Auto-generated code prefix for {settingName}."
    };
}
