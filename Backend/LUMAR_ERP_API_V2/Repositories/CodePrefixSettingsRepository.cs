using System.Text.RegularExpressions;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Settings;
using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.Repositories;

public sealed class CodePrefixSettingsRepository(ReadOnlySqlConnectionFactory connections, OperationalSqlConnectionFactory operationalConnections) : ICodePrefixSettingsRepository
{
    private static readonly IReadOnlyDictionary<string, string> Defaults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["FabricCodePrefix"] = "FA",
        ["CatalogNumberPrefix"] = "CAT",
        ["CustomerCodePrefix"] = "C",
        ["EmployeeCodePrefix"] = "MO",
        ["OrderCodePrefix"] = "OR",
        ["PieceTrackingPrefix"] = "TAR",
        ["ProductionTrackingPrefix"] = "TRK",
        ["ToolCodePrefix"] = "AT"
    };

    private static readonly IReadOnlyDictionary<string, string> DisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["FabricCodePrefix"] = "بادئة كود القماش",
        ["CatalogNumberPrefix"] = "بادئة رقم الكتالوج",
        ["CustomerCodePrefix"] = "بادئة كود العميل",
        ["EmployeeCodePrefix"] = "بادئة كود الموظف",
        ["OrderCodePrefix"] = "بادئة رقم الطلب",
        ["PieceTrackingPrefix"] = "بادئة تتبع قطعة التفصيل",
        ["ProductionTrackingPrefix"] = "بادئة تتبع الإنتاج الجاهز",
        ["ToolCodePrefix"] = "بادئة كود الأدوات"
    };

    public async Task<IReadOnlyList<CodePrefixSettingDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var items = new List<CodePrefixSettingDto>();
        foreach (var entry in Defaults.OrderBy(static x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            var key = entry.Key;
            var stored = await ReadSettingValueAsync(key, cancellationToken);
            var value = string.IsNullOrWhiteSpace(stored) ? entry.Value : stored.Trim();
            items.Add(new CodePrefixSettingDto(key, DisplayNames[key], value, entry.Value, BuildExample(key, value)));
        }

        return items;
    }

    public async Task<CodePrefixSettingDto?> GetByKeyAsync(string key, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var normalized = NormalizeKey(key);
        if (!Defaults.ContainsKey(normalized))
        {
            return null;
        }

        var stored = await ReadSettingValueAsync(normalized, cancellationToken);
        var value = string.IsNullOrWhiteSpace(stored) ? Defaults[normalized] : stored.Trim();
        return new CodePrefixSettingDto(normalized, DisplayNames[normalized], value, Defaults[normalized], BuildExample(normalized, value));
    }

    public async Task<IReadOnlyList<CodePrefixSettingDto>> UpsertAsync(IReadOnlyList<CodePrefixSettingValueDto> settings, CancellationToken cancellationToken)
    {
        if (settings is null || settings.Count == 0)
        {
            throw new ArgumentException("يجب إرسال إعدادات التكويد المطلوبة.");
        }

        var validated = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in settings)
        {
            var key = NormalizeKey(item.Key);
            if (!Defaults.ContainsKey(key))
            {
                throw new ArgumentException($"المفتاح غير مسموح: {item.Key}");
            }

            var value = NormalizePrefix(item.Value);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"البادئة لا يمكن أن تكون فارغة: {key}");
            }

            if (!Regex.IsMatch(value, "^[A-Z0-9]+$", RegexOptions.CultureInvariant))
            {
                throw new ArgumentException($"البادئة يجب أن تحتوي على أحرف إنجليزية كبيرة أو أرقام فقط: {key}");
            }

            if (validated.TryGetValue(value, out var existingKey) && !string.Equals(existingKey, key, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"لا يمكن تكرار البادئة بين نوعين مختلفين. '{value}' مستخدمة بالفعل في {existingKey}.");
            }

            validated[key] = value;
        }

        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);

        try
        {
            foreach (var item in validated)
            {
                var key = item.Key;
                var value = item.Value;
                var exists = await ExistsAsync(connection, transaction, key, cancellationToken);
                if (exists)
                {
                    await using var update = new SqlCommand("UPDATE dbo.System_Settings SET SettingValue=@value, Description=@description WHERE SettingName=@key", connection, transaction);
                    update.Parameters.AddWithValue("@key", key);
                    update.Parameters.AddWithValue("@value", value);
                    update.Parameters.AddWithValue("@description", $"Code prefix for {DisplayNames[key]}");
                    await update.ExecuteNonQueryAsync(cancellationToken);
                    continue;
                }

                await using var insert = new SqlCommand("INSERT INTO dbo.System_Settings (SettingName, SettingValue, Description) VALUES (@key, @value, @description)", connection, transaction);
                insert.Parameters.AddWithValue("@key", key);
                insert.Parameters.AddWithValue("@value", value);
                insert.Parameters.AddWithValue("@description", $"Code prefix for {DisplayNames[key]}");
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return await GetAllAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<string?> ReadSettingValueAsync(string key, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("SELECT TOP (1) SettingValue FROM dbo.System_Settings WITH (NOLOCK) WHERE SettingName=@key", connection);
        command.Parameters.AddWithValue("@key", key);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is DBNull or null ? null : value.ToString();
    }

    private static async Task<bool> ExistsAsync(SqlConnection connection, SqlTransaction transaction, string key, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand("SELECT TOP (1) 1 FROM dbo.System_Settings WITH (UPDLOCK,HOLDLOCK) WHERE SettingName=@key", connection, transaction);
        command.Parameters.AddWithValue("@key", key);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static string BuildExample(string key, string value)
    {
        var suffix = key switch
        {
            "FabricCodePrefix" => "0025",
            "CatalogNumberPrefix" => "0001",
            "CustomerCodePrefix" => "0001",
            "EmployeeCodePrefix" => "0001",
            "OrderCodePrefix" => "0001",
            "PieceTrackingPrefix" => "0001",
            "ProductionTrackingPrefix" => "0001",
            "ToolCodePrefix" => "0001",
            _ => "0001"
        };

        return $"{value}{suffix}";
    }

    private static string NormalizeKey(string key)
    {
        return (key ?? string.Empty).Trim();
    }

    private static string NormalizePrefix(string? value)
    {
        return (value ?? string.Empty).Trim().ToUpperInvariant();
    }
}
