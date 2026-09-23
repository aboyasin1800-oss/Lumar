using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.Utilities;

public sealed record ProductionProductTypeIdentity(
    int ProductTypeId,
    string Code,
    string NameAr,
    string? NameEn,
    bool IsActive);

public interface IProductionProductTypeIdentityResolver
{
    Task<ProductionProductTypeIdentity?> ResolveAsync(
        SqlConnection connection,
        SqlTransaction? transaction,
        int? productTypeId,
        string? code,
        string? nameAr,
        string? nameEn,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ProductionProductTypeIdentity>> GetActiveAsync(
        SqlConnection connection,
        SqlTransaction? transaction,
        CancellationToken cancellationToken);
}

public sealed class ProductionProductTypeIdentityResolver : IProductionProductTypeIdentityResolver
{
    public async Task<ProductionProductTypeIdentity?> ResolveAsync(
        SqlConnection connection,
        SqlTransaction? transaction,
        int? productTypeId,
        string? code,
        string? nameAr,
        string? nameEn,
        CancellationToken cancellationToken)
    {
        if (productTypeId.HasValue && productTypeId.Value <= 0)
        {
            return null;
        }

        const string byIdSql = """
            SELECT ProductTypeId, Code, NameAr, NameEn, IsActive
            FROM dbo.PricingProductTypes WITH (NOLOCK)
            WHERE ProductTypeId = @productTypeId AND IsActive = 1;
            """;

        if (productTypeId.HasValue)
        {
            await using var byIdCommand = CreateCommand(connection, transaction, byIdSql);
            byIdCommand.Parameters.AddWithValue("@productTypeId", productTypeId.Value);
            await using var byIdReader = await byIdCommand.ExecuteReaderAsync(cancellationToken);
            return await byIdReader.ReadAsync(cancellationToken)
                ? Map(byIdReader)
                : null;
        }

        var values = new[] { code, nameAr, nameEn }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (values.Length == 0)
        {
            return null;
        }

        const string byTextSql = """
            SELECT ProductTypeId, Code, NameAr, NameEn, IsActive
            FROM dbo.PricingProductTypes WITH (NOLOCK)
            WHERE IsActive = 1
              AND (Code IN (@code, @nameAr, @nameEn)
                   OR NameAr IN (@code, @nameAr, @nameEn)
                   OR NameEn IN (@code, @nameAr, @nameEn));
            """;

        await using var byTextCommand = CreateCommand(connection, transaction, byTextSql);
        byTextCommand.Parameters.AddWithValue("@code", values.ElementAtOrDefault(0) ?? (object)DBNull.Value);
        byTextCommand.Parameters.AddWithValue("@nameAr", values.ElementAtOrDefault(1) ?? (object)DBNull.Value);
        byTextCommand.Parameters.AddWithValue("@nameEn", values.ElementAtOrDefault(2) ?? (object)DBNull.Value);
        await using var reader = await byTextCommand.ExecuteReaderAsync(cancellationToken);
        var matches = new List<ProductionProductTypeIdentity>();
        while (await reader.ReadAsync(cancellationToken))
        {
            matches.Add(Map(reader));
        }

        return matches.Count == 1 ? matches[0] : null;
    }

    public async Task<IReadOnlyList<ProductionProductTypeIdentity>> GetActiveAsync(
        SqlConnection connection,
        SqlTransaction? transaction,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT ProductTypeId, Code, NameAr, NameEn, IsActive
            FROM dbo.PricingProductTypes WITH (NOLOCK)
            WHERE IsActive = 1
            ORDER BY ProductTypeId;
            """;

        await using var command = CreateCommand(connection, transaction, sql);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<ProductionProductTypeIdentity>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(Map(reader));
        }

        return result;
    }

    private static SqlCommand CreateCommand(SqlConnection connection, SqlTransaction? transaction, string sql)
    {
        var command = new SqlCommand(sql, connection);
        if (transaction is not null)
        {
            command.Transaction = transaction;
        }

        return command;
    }

    private static ProductionProductTypeIdentity Map(SqlDataReader reader) =>
        new(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.GetBoolean(4));
}