using System.Globalization;
using LUMAR_ERP_API_V2.Data;
using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.Repositories;

public sealed class PricingProfitSettingsRepository(OperationalSqlConnectionFactory connections) : IPricingProfitSettingsRepository
{
    private const string GlobalKey = "Pricing.GlobalProfitPercentage";
    private const string ProductPrefix = "Pricing.ProductType.";
    private const string ProductSuffix = ".ProfitPercentage";

    public async Task<IReadOnlyDictionary<string, decimal>> GetAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT SettingName, SettingValue
            FROM dbo.System_Settings WITH (NOLOCK)
            WHERE SettingName = @globalKey
               OR (SettingName LIKE @productPrefix AND SettingName LIKE @productSuffix)
            ORDER BY SettingName, SettingID
            """;

        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@globalKey", GlobalKey);
        command.Parameters.AddWithValue("@productPrefix", ProductPrefix + "%");
        command.Parameters.AddWithValue("@productSuffix", "%" + ProductSuffix);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new Dictionary<string, decimal>(StringComparer.Ordinal);
        while (await reader.ReadAsync(cancellationToken))
        {
            var key = reader.GetString(0);
            if (result.ContainsKey(key))
            {
                throw new InvalidOperationException($"يوجد تكرار في مفتاح إعداد الربح: {key}.");
            }

            var raw = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) || value < 0)
            {
                throw new InvalidOperationException($"قيمة إعداد الربح غير صالحة للمفتاح: {key}.");
            }

            result[key] = value;
        }

        return result;
    }

    public Task SetGlobalAsync(decimal value, CancellationToken cancellationToken) =>
        UpsertAsync(GlobalKey, value, "نسبة الربح العامة لمحرك التسعير.", cancellationToken);

    public Task SetProductTypeAsync(int productTypeId, decimal value, CancellationToken cancellationToken) =>
        UpsertAsync(
            $"{ProductPrefix}{productTypeId}{ProductSuffix}",
            value,
            $"نسبة ربح نوع القطعة رقم {productTypeId} لمحرك التسعير.",
            cancellationToken);

    private async Task UpsertAsync(string key, decimal value, string description, CancellationToken cancellationToken)
    {
        const string countSql = "SELECT COUNT(*) FROM dbo.System_Settings WITH (UPDLOCK, HOLDLOCK) WHERE SettingName=@key";
        const string updateSql = "UPDATE dbo.System_Settings SET SettingValue=@value, Description=@description WHERE SettingName=@key";
        const string insertSql = "INSERT INTO dbo.System_Settings (SettingName, SettingValue, Description) VALUES (@key, @value, @description)";

        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await using var count = new SqlCommand(countSql, connection, transaction);
            count.Parameters.AddWithValue("@key", key);
            var existing = Convert.ToInt32(await count.ExecuteScalarAsync(cancellationToken));
            if (existing > 1)
            {
                throw new InvalidOperationException($"يوجد تكرار في مفتاح إعداد الربح: {key}.");
            }

            await using var command = new SqlCommand(existing == 1 ? updateSql : insertSql, connection, transaction);
            command.Parameters.AddWithValue("@key", key);
            command.Parameters.AddWithValue("@value", value.ToString(CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("@description", description);
            await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
