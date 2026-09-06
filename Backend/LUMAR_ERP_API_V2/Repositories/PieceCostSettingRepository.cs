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
        const string sql = """
            WITH ActiveMatrices AS
            (
                SELECT m.CostMatrixId, m.ProductTypeId, m.ChangeReason,
                       ROW_NUMBER() OVER (PARTITION BY m.ProductTypeId ORDER BY m.Version DESC, m.CostMatrixId DESC) AS RowNumber
                FROM dbo.PricingCostMatrices m
                WHERE m.Channel=N'Tailoring' AND m.Status=N'Active' AND m.IsActive=1
            ), CostValues AS
            (
                SELECT mi.CostMatrixId,
                       SUM(CASE WHEN ci.Code=@sewingCode THEN mi.Quantity * mi.UnitCost ELSE 0 END) AS SewingCost,
                       SUM(CASE WHEN ci.Code=@consumablesCode THEN mi.Quantity * mi.UnitCost ELSE 0 END) AS ConsumablesCost,
                       SUM(CASE WHEN ci.Code=@ironingCode THEN mi.Quantity * mi.UnitCost ELSE 0 END) AS IroningCost,
                       SUM(CASE WHEN ci.Code=@fixedCode THEN mi.Quantity * mi.UnitCost ELSE 0 END) AS FixedCost
                FROM dbo.PricingCostMatrixItems mi
                JOIN dbo.PricingCostItems ci ON ci.CostItemId=mi.CostItemId
                WHERE ci.Code IN (@sewingCode,@consumablesCode,@ironingCode,@fixedCode)
                GROUP BY mi.CostMatrixId
            )
            SELECT pt.ProductTypeId, pt.Code, pt.NameAr,
                   COALESCE(cv.SewingCost,0), COALESCE(cv.ConsumablesCost,0),
                   COALESCE(cv.IroningCost,0), COALESCE(cv.FixedCost,0),
                   am.ChangeReason, CASE WHEN cv.CostMatrixId IS NULL THEN CAST(0 AS bit) ELSE CAST(1 AS bit) END
            FROM dbo.PricingProductTypes pt
            LEFT JOIN ActiveMatrices am ON am.ProductTypeId=pt.ProductTypeId AND am.RowNumber=1
            LEFT JOIN CostValues cv ON cv.CostMatrixId=am.CostMatrixId
            WHERE pt.IsActive=1 AND pt.Category=N'Garment' AND pt.Scope IN (N'Both',N'Tailoring')
            ORDER BY pt.NameAr, pt.ProductTypeId
            """;
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        AddCodes(command);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var results = new List<PieceCostSettingDto>();
        while (await reader.ReadAsync(cancellationToken)) results.Add(Map(reader));
        return results;
    }

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