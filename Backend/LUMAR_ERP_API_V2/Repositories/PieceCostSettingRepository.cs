using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Pricing;
using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.Repositories;

public sealed class PieceCostSettingRepository(ReadOnlySqlConnectionFactory connections, OperationalSqlConnectionFactory operationalConnections) : IPieceCostSettingRepository
{
    private const string SewingCode = "OP_SEWING";
    private const string ConsumablesCode = "OP_CONSUMABLES";
    private const string IroningCode = "OP_IRON_PACK";
    private const string FixedCode = "OP_FIXED";

    public async Task<IReadOnlyList<PieceCostSettingDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);

        var products = new List<(int Id, string Code, string Name)>();
        const string productsSql = """
            SELECT ProductTypeId, Code, NameAr
            FROM dbo.PricingProductTypes
            WHERE IsActive=1 AND Category=N'Garment' AND Scope IN (N'Both',N'Tailoring')
            ORDER BY NameAr, ProductTypeId;
            """;
        await using (var productsCommand = new SqlCommand(productsSql, connection))
        await using (var productsReader = await productsCommand.ExecuteReaderAsync(cancellationToken))
        {
            while (await productsReader.ReadAsync(cancellationToken))
            {
                products.Add((productsReader.GetInt32(0), productsReader.GetString(1), productsReader.GetString(2)));
            }
        }

        var settings = new Dictionary<string, string>(StringComparer.Ordinal);
        const string settingsSql = """
            SELECT SettingName, SettingValue
            FROM dbo.System_Settings
            WHERE SettingName LIKE N'PieceTypeCost.%';
            """;
        await using (var settingsCommand = new SqlCommand(settingsSql, connection))
        await using (var settingsReader = await settingsCommand.ExecuteReaderAsync(cancellationToken))
        {
            while (await settingsReader.ReadAsync(cancellationToken))
            {
                if (!settingsReader.IsDBNull(0))
                {
                    settings[settingsReader.GetString(0)] = settingsReader.IsDBNull(1)
                        ? "0"
                        : settingsReader.GetString(1);
                }
            }
        }

        var results = new List<PieceCostSettingDto>(products.Count);
        foreach (var product in products)
        {
            var prefix = $"PieceTypeCost.{Uri.EscapeDataString(product.Name)}.";
            var sewing = Value(settings, prefix + "Sewing");
            var consumables = Value(settings, prefix + "ToolsConsumables");
            var ironing = Value(settings, prefix + "IroningPackaging");
            var fixedCost = Value(settings, prefix + "FixedOperating");
            var total = sewing + consumables + ironing + fixedCost;
            results.Add(new PieceCostSettingDto(
                product.Id,
                product.Code,
                product.Name,
                sewing,
                consumables,
                ironing,
                fixedCost,
                null,
                total,
                total != 0m));
        }

        return results;
    }

    private static decimal Value(IReadOnlyDictionary<string, string> settings, string key) =>
        decimal.TryParse(settings.GetValueOrDefault(key), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : 0m;

    public async Task<PieceCostSettingDto?> UpsertAsync(int productTypeId, UpsertPieceCostSettingDto setting, CancellationToken cancellationToken)
    {
        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        try
        {
            string? pieceName;
            await using (var productCommand = new SqlCommand("SELECT NameAr FROM dbo.PricingProductTypes WITH (UPDLOCK,HOLDLOCK) WHERE ProductTypeId=@productTypeId AND IsActive=1 AND Category=N'Garment' AND Scope IN (N'Both',N'Tailoring')", connection, transaction))
            {
                productCommand.Parameters.AddWithValue("@productTypeId", productTypeId);
                pieceName = await productCommand.ExecuteScalarAsync(cancellationToken) as string;
            }
            if (pieceName is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }

            const string ensureItemsSql = """
                IF NOT EXISTS (SELECT 1 FROM dbo.PricingCostItems WITH (UPDLOCK,HOLDLOCK) WHERE Code=@sewingCode)
                    INSERT INTO dbo.PricingCostItems (Code,NameAr,Category,CalculationMethod,Unit,IsActive,CreatedAt) VALUES (@sewingCode,N'تكلفة الخياطة',N'Operating',N'Fixed',N'Piece',1,SYSDATETIME());
                IF NOT EXISTS (SELECT 1 FROM dbo.PricingCostItems WITH (UPDLOCK,HOLDLOCK) WHERE Code=@consumablesCode)
                    INSERT INTO dbo.PricingCostItems (Code,NameAr,Category,CalculationMethod,Unit,IsActive,CreatedAt) VALUES (@consumablesCode,N'تكلفة الأدوات والمستهلكات',N'Operating',N'Fixed',N'Piece',1,SYSDATETIME());
                IF NOT EXISTS (SELECT 1 FROM dbo.PricingCostItems WITH (UPDLOCK,HOLDLOCK) WHERE Code=@ironingCode)
                    INSERT INTO dbo.PricingCostItems (Code,NameAr,Category,CalculationMethod,Unit,IsActive,CreatedAt) VALUES (@ironingCode,N'تكلفة الكي والتغليف',N'Operating',N'Fixed',N'Piece',1,SYSDATETIME());
                IF NOT EXISTS (SELECT 1 FROM dbo.PricingCostItems WITH (UPDLOCK,HOLDLOCK) WHERE Code=@fixedCode)
                    INSERT INTO dbo.PricingCostItems (Code,NameAr,Category,CalculationMethod,Unit,IsActive,CreatedAt) VALUES (@fixedCode,N'تكلفة التشغيل الثابتة',N'Operating',N'Fixed',N'Piece',1,SYSDATETIME());
                """;
            await using (var ensureCommand = new SqlCommand(ensureItemsSql, connection, transaction))
            {
                AddCodes(ensureCommand);
                await ensureCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            int? matrixId;
            await using (var findCommand = new SqlCommand("SELECT TOP (1) CostMatrixId FROM dbo.PricingCostMatrices WITH (UPDLOCK,HOLDLOCK) WHERE ProductTypeId=@productTypeId AND Channel=N'Tailoring' AND Status=N'Active' AND IsActive=1 ORDER BY Version DESC,CostMatrixId DESC", connection, transaction))
            {
                findCommand.Parameters.AddWithValue("@productTypeId", productTypeId);
                matrixId = await findCommand.ExecuteScalarAsync(cancellationToken) as int?;
            }

            if (matrixId is null)
            {
                const string insertMatrixSql = """
                    DECLARE @version int=ISNULL((SELECT MAX(Version) FROM dbo.PricingCostMatrices WITH (UPDLOCK,HOLDLOCK) WHERE ProductTypeId=@productTypeId AND Channel=N'Tailoring'),0)+1;
                    INSERT INTO dbo.PricingCostMatrices (ProductTypeId,Name,Channel,Version,Status,EffectiveFrom,EffectiveTo,IsActive,ChangeReason,CreatedBy,CreatedAt,UpdatedAt)
                    OUTPUT INSERTED.CostMatrixId
                    VALUES (@productTypeId,N'إعداد تكاليف '+@pieceName,N'Tailoring',@version,N'Active',SYSDATETIME(),NULL,1,@notes,N'LUMAR ERP',SYSDATETIME(),NULL);
                    """;
                await using var insertCommand = new SqlCommand(insertMatrixSql, connection, transaction);
                insertCommand.Parameters.AddWithValue("@productTypeId", productTypeId);
                insertCommand.Parameters.AddWithValue("@pieceName", pieceName);
                AddNullable(insertCommand, "@notes", setting.Notes?.Trim());
                matrixId = (int)(await insertCommand.ExecuteScalarAsync(cancellationToken))!;
            }
            else
            {
                await using var updateCommand = new SqlCommand("UPDATE dbo.PricingCostMatrices SET ChangeReason=@notes,UpdatedAt=SYSDATETIME() WHERE CostMatrixId=@matrixId", connection, transaction);
                updateCommand.Parameters.AddWithValue("@matrixId", matrixId.Value);
                AddNullable(updateCommand, "@notes", setting.Notes?.Trim());
                await updateCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            const string replaceItemsSql = """
                DELETE mi FROM dbo.PricingCostMatrixItems mi JOIN dbo.PricingCostItems ci ON ci.CostItemId=mi.CostItemId WHERE mi.CostMatrixId=@matrixId AND ci.Code IN (@sewingCode,@consumablesCode,@ironingCode,@fixedCode);
                INSERT INTO dbo.PricingCostMatrixItems (CostMatrixId,CostItemId,Quantity,UnitCost,AllocationPercentage,Sequence)
                SELECT @matrixId,CostItemId,CAST(1 AS decimal(18,4)),
                       CASE Code WHEN @sewingCode THEN @sewingCost WHEN @consumablesCode THEN @consumablesCost WHEN @ironingCode THEN @ironingCost ELSE @fixedCost END,
                       CAST(0 AS decimal(18,4)),
                       CASE Code WHEN @sewingCode THEN 1 WHEN @consumablesCode THEN 2 WHEN @ironingCode THEN 3 ELSE 4 END
                FROM dbo.PricingCostItems WHERE Code IN (@sewingCode,@consumablesCode,@ironingCode,@fixedCode);
                """;
            await using (var replaceCommand = new SqlCommand(replaceItemsSql, connection, transaction))
            {
                replaceCommand.Parameters.AddWithValue("@matrixId", matrixId.Value);
                replaceCommand.Parameters.AddWithValue("@sewingCost", setting.SewingCost);
                replaceCommand.Parameters.AddWithValue("@consumablesCost", setting.ConsumablesCost);
                replaceCommand.Parameters.AddWithValue("@ironingCost", setting.IroningAndPackagingCost);
                replaceCommand.Parameters.AddWithValue("@fixedCost", setting.FixedOperatingCost);
                AddCodes(replaceCommand);
                await replaceCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return (await GetAllAsync(cancellationToken)).Single(item => item.ProductTypeId == productTypeId);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static PieceCostSettingDto Map(SqlDataReader reader)
    {
        var sewing = reader.GetDecimal(3);
        var consumables = reader.GetDecimal(4);
        var ironing = reader.GetDecimal(5);
        var fixedCost = reader.GetDecimal(6);
        return new PieceCostSettingDto(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), sewing, consumables, ironing, fixedCost, reader.NullableString("ChangeReason"), sewing + consumables + ironing + fixedCost, reader.GetBoolean(8));
    }

    private static void AddCodes(SqlCommand command)
    {
        command.Parameters.AddWithValue("@sewingCode", SewingCode);
        command.Parameters.AddWithValue("@consumablesCode", ConsumablesCode);
        command.Parameters.AddWithValue("@ironingCode", IroningCode);
        command.Parameters.AddWithValue("@fixedCode", FixedCode);
    }

    private static void AddNullable(SqlCommand command, string name, string? value) => command.Parameters.AddWithValue(name, string.IsNullOrWhiteSpace(value) ? DBNull.Value : value);
}