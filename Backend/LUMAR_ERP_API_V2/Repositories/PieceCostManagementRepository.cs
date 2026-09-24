using System.Data;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Pricing;
using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.Repositories;

public sealed class PieceCostManagementRepository(
    ReadOnlySqlConnectionFactory readOnlyConnections,
    OperationalSqlConnectionFactory operationalConnections) : IPieceCostManagementRepository
{
    private static readonly string[] MonthlyKeys =
    [
        "MonthlyFixedCostRent",
        "MonthlyFixedCostSalaries",
        "MonthlyFixedCostElectricity",
        "MonthlyFixedCostWater",
        "MonthlyFixedCostInternet",
        "MonthlyFixedCostDepreciation"
    ];

    public async Task<IReadOnlyList<PieceCostManagementDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        await using var connection = readOnlyConnections.Create();
        await connection.OpenAsync(cancellationToken);

        var settings = await ReadSettingsAsync(connection, cancellationToken);
        const string sql = """
            WITH active_matrices AS (
                SELECT CostMatrixId, ProductTypeId,
                       ROW_NUMBER() OVER (PARTITION BY ProductTypeId ORDER BY Version DESC, CostMatrixId DESC) AS rn
                FROM dbo.PricingCostMatrices
                WHERE Channel=N'Tailoring' AND Status=N'Active' AND IsActive=1
            ), matrix_costs AS (
                SELECT am.ProductTypeId,
                       SUM(CASE WHEN ci.Code=N'OP_SEWING' THEN m.UnitCost * m.Quantity ELSE 0 END) AS SewingCost,
                       SUM(CASE WHEN ci.Code=N'OP_CONSUMABLES' THEN m.UnitCost * m.Quantity ELSE 0 END) AS ConsumablesCost,
                       SUM(CASE WHEN ci.Code=N'OP_IRON_PACK' THEN m.UnitCost * m.Quantity ELSE 0 END) AS IroningCost,
                       SUM(CASE WHEN ci.Code=N'OP_FIXED' THEN m.UnitCost * m.Quantity ELSE 0 END) AS FixedCost
                FROM active_matrices am
                INNER JOIN dbo.PricingCostMatrixItems m ON m.CostMatrixId=am.CostMatrixId
                INNER JOIN dbo.PricingCostItems ci ON ci.CostItemId=m.CostItemId AND ci.IsActive=1
                WHERE am.rn=1
                GROUP BY am.ProductTypeId
            )
            SELECT pt.ProductTypeId, pt.Code, pt.NameAr,
                   COALESCE(mc.SewingCost, 0), COALESCE(mc.ConsumablesCost, 0),
                   COALESCE(mc.IroningCost, 0), COALESCE(mc.FixedCost, 0)
            FROM dbo.PricingProductTypes pt
            LEFT JOIN matrix_costs mc ON mc.ProductTypeId=pt.ProductTypeId
            WHERE pt.IsActive=1 AND pt.Category=N'Garment' AND pt.Scope IN (N'Both',N'Tailoring')
            ORDER BY pt.NameAr, pt.ProductTypeId;
            """;
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var results = new List<PieceCostManagementDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new PieceCostManagementDto(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetDecimal(3),
                reader.GetDecimal(4),
                reader.GetDecimal(5),
                reader.GetDecimal(6),
                Value(settings, MonthlyKeys[0]),
                Value(settings, MonthlyKeys[1]),
                Value(settings, MonthlyKeys[2]),
                Value(settings, MonthlyKeys[3]),
                Value(settings, MonthlyKeys[4]),
                Value(settings, MonthlyKeys[5])));
        }

        return results;
    }

    public async Task<PieceCostManagementDto?> UpdateAsync(
        int productTypeId,
        UpdatePieceCostManagementDto request,
        CancellationToken cancellationToken)
    {
        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        string? code = null;
        string? pieceName = null;
        const string productSql = """
            SELECT Code, NameAr
            FROM dbo.PricingProductTypes WITH (UPDLOCK,HOLDLOCK)
            WHERE ProductTypeId=@productTypeId AND IsActive=1
              AND Category=N'Garment' AND Scope IN (N'Both',N'Tailoring');
            """;
        await using (var productCommand = new SqlCommand(productSql, connection, transaction))
        {
            productCommand.Parameters.AddWithValue("@productTypeId", productTypeId);
            await using var reader = await productCommand.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                code = reader.GetString(0);
                pieceName = reader.GetString(1);
            }
        }

        if (pieceName is null || code is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [$"PieceTypeCost.{Uri.EscapeDataString(pieceName)}.Sewing"] = request.SewingCost.ToString(System.Globalization.CultureInfo.InvariantCulture),
            [$"PieceTypeCost.{Uri.EscapeDataString(pieceName)}.ToolsConsumables"] = request.ConsumablesCost.ToString(System.Globalization.CultureInfo.InvariantCulture),
            [$"PieceTypeCost.{Uri.EscapeDataString(pieceName)}.IroningPackaging"] = request.IroningAndPackagingCost.ToString(System.Globalization.CultureInfo.InvariantCulture),
            [$"PieceTypeCost.{Uri.EscapeDataString(pieceName)}.FixedOperating"] = request.FixedOperatingCost.ToString(System.Globalization.CultureInfo.InvariantCulture),
            [MonthlyKeys[0]] = request.MonthlyRent.ToString(System.Globalization.CultureInfo.InvariantCulture),
            [MonthlyKeys[1]] = request.MonthlySalaries.ToString(System.Globalization.CultureInfo.InvariantCulture),
            [MonthlyKeys[2]] = request.MonthlyElectricity.ToString(System.Globalization.CultureInfo.InvariantCulture),
            [MonthlyKeys[3]] = request.MonthlyWater.ToString(System.Globalization.CultureInfo.InvariantCulture),
            [MonthlyKeys[4]] = request.MonthlyInternet.ToString(System.Globalization.CultureInfo.InvariantCulture),
            [MonthlyKeys[5]] = request.MonthlyDepreciation.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

        foreach (var entry in values)
        {
            const string sql = """
                UPDATE dbo.System_Settings SET SettingValue=@value WHERE SettingName=@name;
                IF @@ROWCOUNT=0
                    INSERT INTO dbo.System_Settings (SettingName, SettingValue, Description)
                    VALUES (@name, @value, N'إعدادات إدارة تكاليف القطع');
                """;
            await using var command = new SqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("@name", entry.Key);
            command.Parameters.AddWithValue("@value", entry.Value);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        var saved = await GetAllAsync(cancellationToken);
        return saved.SingleOrDefault(item => item.ProductTypeId == productTypeId);
    }

    private static async Task<Dictionary<string, string>> ReadSettingsAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        var settings = new Dictionary<string, string>(StringComparer.Ordinal);
        const string sql = """
            SELECT SettingName, SettingValue
            FROM dbo.System_Settings
                WHERE SettingName IN (
                    N'MonthlyFixedCostRent', N'MonthlyFixedCostSalaries',
                    N'MonthlyFixedCostElectricity', N'MonthlyFixedCostWater',
                    N'MonthlyFixedCostInternet', N'MonthlyFixedCostDepreciation');
            """;
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (!reader.IsDBNull(0))
            {
                settings[reader.GetString(0)] = reader.IsDBNull(1) ? "0" : reader.GetString(1);
            }
        }

        return settings;
    }

    private static PieceCostManagementDto Map(
        (int Id, string Code, string Name) product,
        IReadOnlyDictionary<string, string> settings)
    {
        var prefix = $"PieceTypeCost.{Uri.EscapeDataString(product.Name)}.";
        return new PieceCostManagementDto(
            product.Id,
            product.Code,
            product.Name,
            Value(settings, prefix + "Sewing"),
            Value(settings, prefix + "ToolsConsumables"),
            Value(settings, prefix + "IroningPackaging"),
            Value(settings, prefix + "FixedOperating"),
            Value(settings, MonthlyKeys[0]),
            Value(settings, MonthlyKeys[1]),
            Value(settings, MonthlyKeys[2]),
            Value(settings, MonthlyKeys[3]),
            Value(settings, MonthlyKeys[4]),
            Value(settings, MonthlyKeys[5]));
    }

    private static decimal Value(IReadOnlyDictionary<string, string> settings, string key) =>
        decimal.TryParse(settings.GetValueOrDefault(key), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : 0m;
}