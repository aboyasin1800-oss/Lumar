using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Loyalty;
using LUMAR_ERP_API_V2.Utilities;
using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.Repositories;

public sealed class PiecePointSettingsRepository(ReadOnlySqlConnectionFactory connections, OperationalSqlConnectionFactory operationalConnections) : IPiecePointSettingsRepository
{
    public async Task<IReadOnlyList<ProductLoyaltyPointSettingDto>> GetProductLoyaltyPointSettingsAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT pt.ProductTypeId, pt.Code, pt.NameAr, pt.Category,
                   s.LoyaltyPiecePointSettingId, s.Points, s.IsActive
            FROM dbo.PricingProductTypes pt WITH (NOLOCK)
            LEFT JOIN dbo.LoyaltyPiecePointSettings s WITH (NOLOCK) ON s.ProductTypeId = pt.ProductTypeId
            WHERE pt.IsActive = 1
            ORDER BY pt.NameAr, pt.ProductTypeId;";
        return await QueryAsync(sql, reader => new ProductLoyaltyPointSettingDto(
            reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.NullableString("Category"),
            reader.IsDBNull(4) ? null : reader.GetInt32(4), reader.IsDBNull(5) ? null : reader.GetDecimal(5),
            reader.IsDBNull(6) ? null : reader.GetBoolean(6), !reader.IsDBNull(4)), cancellationToken);
    }

    public async Task<ProductLoyaltyPointSettingDto?> UpsertProductLoyaltyPointSettingAsync(int productTypeId, UpdateProductLoyaltyPointSettingDto request, CancellationToken cancellationToken)
    {
        if (productTypeId <= 0) throw new ArgumentException("ProductTypeId must be positive.");
        if (request.Points < 0m) throw new ArgumentException("Points cannot be negative.");
        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        const string sql = @"
            DECLARE @Code nvarchar(100), @NameAr nvarchar(200);
            SELECT @Code = Code, @NameAr = NameAr FROM dbo.PricingProductTypes WITH (UPDLOCK, HOLDLOCK) WHERE ProductTypeId = @productTypeId AND IsActive = 1;
            IF @Code IS NULL THROW 51001, 'Active ProductTypeId was not found.', 1;
            IF EXISTS (SELECT 1 FROM dbo.LoyaltyPiecePointSettings WHERE ProductTypeId = @productTypeId)
                UPDATE dbo.LoyaltyPiecePointSettings SET Points = @points, IsActive = @isActive, UpdatedAtUtc = SYSUTCDATETIME() WHERE ProductTypeId = @productTypeId;
            ELSE IF EXISTS (SELECT 1 FROM dbo.LoyaltyPiecePointSettings WHERE PieceCode = @Code AND ProductTypeId IS NULL)
                UPDATE dbo.LoyaltyPiecePointSettings
                SET ProductTypeId = @productTypeId, PieceName = @NameAr, Points = @points, IsActive = @isActive, UpdatedAtUtc = SYSUTCDATETIME()
                WHERE PieceCode = @Code AND ProductTypeId IS NULL;
            ELSE IF EXISTS (SELECT 1 FROM dbo.LoyaltyPiecePointSettings WHERE PieceCode = @Code)
                THROW 51003, 'PieceCode is already linked to another official product type.', 1;
            ELSE
                INSERT INTO dbo.LoyaltyPiecePointSettings (PieceCode, PieceName, Points, IsActive, CreatedAtUtc, UpdatedAtUtc, ProductTypeId)
                VALUES (@Code, @NameAr, @points, @isActive, SYSUTCDATETIME(), SYSUTCDATETIME(), @productTypeId);";
        await using (var command = new SqlCommand(sql, connection, transaction))
        {
            command.Parameters.AddWithValue("@productTypeId", productTypeId);
            command.Parameters.AddWithValue("@points", request.Points);
            command.Parameters.AddWithValue("@isActive", request.IsActive);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return (await GetProductLoyaltyPointSettingsAsync(cancellationToken)).SingleOrDefault(x => x.ProductTypeId == productTypeId);
    }

    public async Task<IReadOnlyList<ReadyMadeProductTypeLoyaltyPointSettingDto>> GetReadyMadeProductTypeLoyaltyPointSettingsAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT pt.ProductTypeId, pt.Code, pt.NameAr, pt.Category,
                   s.ProductTypeId, s.Points, s.IsActive
            FROM dbo.PricingProductTypes pt WITH (NOLOCK)
            LEFT JOIN dbo.LoyaltyReadyMadeProductTypePointSettings s WITH (NOLOCK) ON s.ProductTypeId = pt.ProductTypeId
            WHERE pt.IsActive = 1
            ORDER BY pt.NameAr, pt.ProductTypeId;";
        return await QueryAsync(sql, reader => new ReadyMadeProductTypeLoyaltyPointSettingDto(
            reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.NullableString("Category"),
            reader.IsDBNull(4) ? null : reader.GetInt32(4), reader.IsDBNull(5) ? null : reader.GetDecimal(5),
            reader.IsDBNull(6) ? null : reader.GetBoolean(6), !reader.IsDBNull(4)), cancellationToken);
    }

    public async Task<ReadyMadeProductTypeLoyaltyPointSettingDto?> UpsertReadyMadeProductTypeLoyaltyPointSettingAsync(int productTypeId, UpdateReadyMadeProductTypeLoyaltyPointSettingDto request, CancellationToken cancellationToken)
    {
        if (productTypeId <= 0) throw new ArgumentException("ProductTypeId must be positive.");
        if (request.Points < 0m) throw new ArgumentException("Points cannot be negative.");
        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        const string sql = @"
            IF NOT EXISTS (SELECT 1 FROM dbo.PricingProductTypes WITH (UPDLOCK, HOLDLOCK) WHERE ProductTypeId = @productTypeId AND IsActive = 1)
                THROW 51004, 'Active ProductTypeId was not found.', 1;
            IF EXISTS (SELECT 1 FROM dbo.LoyaltyReadyMadeProductTypePointSettings WHERE ProductTypeId = @productTypeId)
                UPDATE dbo.LoyaltyReadyMadeProductTypePointSettings SET Points = @points, IsActive = @isActive, UpdatedAtUtc = SYSUTCDATETIME() WHERE ProductTypeId = @productTypeId;
            ELSE
                INSERT INTO dbo.LoyaltyReadyMadeProductTypePointSettings (ProductTypeId, Points, IsActive, CreatedAtUtc, UpdatedAtUtc)
                VALUES (@productTypeId, @points, @isActive, SYSUTCDATETIME(), SYSUTCDATETIME());";
        await using (var command = new SqlCommand(sql, connection, transaction))
        {
            command.Parameters.AddWithValue("@productTypeId", productTypeId);
            command.Parameters.AddWithValue("@points", request.Points);
            command.Parameters.AddWithValue("@isActive", request.IsActive);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return (await GetReadyMadeProductTypeLoyaltyPointSettingsAsync(cancellationToken)).SingleOrDefault(x => x.ProductTypeId == productTypeId);
    }

    public async Task<IReadOnlyList<ImportedProductLoyaltyPointSettingDto>> GetImportedProductLoyaltyPointSettingsAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT p.ImportedReadyMadeProductId, p.ProductName, p.ProductType, p.ProductCode, p.IsActive,
                   s.ImportedReadyMadeProductId, s.Points, s.IsActive
            FROM dbo.ImportedReadyMadeProducts p WITH (NOLOCK)
            LEFT JOIN dbo.LoyaltyImportedReadyMadeProductPointSettings s WITH (NOLOCK)
                ON s.ImportedReadyMadeProductId = p.ImportedReadyMadeProductId
            ORDER BY p.ProductName, p.ImportedReadyMadeProductId;";
        return await QueryAsync(sql, reader => new ImportedProductLoyaltyPointSettingDto(
            reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetBoolean(4),
            reader.IsDBNull(5) ? null : reader.GetInt32(5), reader.IsDBNull(6) ? null : reader.GetDecimal(6),
            reader.IsDBNull(7) ? null : reader.GetBoolean(7), !reader.IsDBNull(5)), cancellationToken);
    }

    public async Task<ImportedProductLoyaltyPointSettingDto?> UpsertImportedReadyMadeProductPointSettingAsync(int importedReadyMadeProductId, UpdateImportedProductLoyaltyPointSettingDto request, CancellationToken cancellationToken)
    {
        if (importedReadyMadeProductId <= 0) throw new ArgumentException("ImportedReadyMadeProductId must be positive.");
        if (request.Points < 0m) throw new ArgumentException("Points cannot be negative.");
        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        const string sql = @"
            IF NOT EXISTS (SELECT 1 FROM dbo.ImportedReadyMadeProducts WITH (UPDLOCK, HOLDLOCK) WHERE ImportedReadyMadeProductId = @id AND IsActive = 1)
                THROW 51002, 'Active ImportedReadyMadeProductId was not found.', 1;
            IF EXISTS (SELECT 1 FROM dbo.LoyaltyImportedReadyMadeProductPointSettings WHERE ImportedReadyMadeProductId = @id)
                UPDATE dbo.LoyaltyImportedReadyMadeProductPointSettings SET Points = @points, IsActive = @isActive, UpdatedAtUtc = SYSUTCDATETIME() WHERE ImportedReadyMadeProductId = @id;
            ELSE
                INSERT INTO dbo.LoyaltyImportedReadyMadeProductPointSettings (ImportedReadyMadeProductId, Points, IsActive, CreatedAtUtc, UpdatedAtUtc)
                VALUES (@id, @points, @isActive, SYSUTCDATETIME(), SYSUTCDATETIME());";
        await using (var command = new SqlCommand(sql, connection, transaction))
        {
            command.Parameters.AddWithValue("@id", importedReadyMadeProductId);
            command.Parameters.AddWithValue("@points", request.Points);
            command.Parameters.AddWithValue("@isActive", request.IsActive);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return (await GetImportedProductLoyaltyPointSettingsAsync(cancellationToken)).SingleOrDefault(x => x.ImportedReadyMadeProductId == importedReadyMadeProductId);
    }

    public Task<ImportedProductLoyaltyPointSettingDto?> UpsertImportedProductLoyaltyPointSettingAsync(int importedReadyMadeProductId, UpdateImportedProductLoyaltyPointSettingDto request, CancellationToken cancellationToken)
        => UpsertImportedReadyMadeProductPointSettingAsync(importedReadyMadeProductId, request, cancellationToken);

    public async Task<LoyaltyPiecePointSettingDto?> GetActiveProductPointSettingAsync(int productTypeId, CancellationToken cancellationToken)
        => await GetOfficialPointSettingAsync("ProductTypeId", productTypeId, cancellationToken);

    public async Task<LoyaltyPiecePointSettingDto?> GetActiveReadyMadeProductTypePointSettingAsync(int productTypeId, CancellationToken cancellationToken)
    {
        const string sql = @"SELECT s.ProductTypeId, pt.Code, pt.NameAr, s.Points, s.IsActive
            FROM dbo.LoyaltyReadyMadeProductTypePointSettings s
            INNER JOIN dbo.PricingProductTypes pt ON pt.ProductTypeId = s.ProductTypeId
            WHERE s.ProductTypeId = @id AND s.IsActive = 1 AND pt.IsActive = 1";
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", productTypeId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new LoyaltyPiecePointSettingDto(0, reader.GetString(1), reader.GetString(2), reader.GetDecimal(3), reader.GetBoolean(4), DateTime.UtcNow, DateTime.UtcNow, reader.GetInt32(0));
    }

    public async Task<LoyaltyPiecePointSettingDto?> GetActiveImportedPointSettingAsync(int importedReadyMadeProductId, CancellationToken cancellationToken)
    {
        const string sql = @"SELECT s.ImportedReadyMadeProductId, s.Points, s.IsActive FROM dbo.LoyaltyImportedReadyMadeProductPointSettings s WHERE s.ImportedReadyMadeProductId = @id AND s.IsActive = 1";
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", importedReadyMadeProductId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new LoyaltyPiecePointSettingDto(0, $"IMPORTED:{reader.GetInt32(0)}", $"IMPORTED:{reader.GetInt32(0)}", reader.GetDecimal(1), reader.GetBoolean(2), DateTime.UtcNow, DateTime.UtcNow);
    }

    private async Task<LoyaltyPiecePointSettingDto?> GetOfficialPointSettingAsync(string keyColumn, int key, CancellationToken cancellationToken)
    {
        if (keyColumn != "ProductTypeId") throw new ArgumentException("Unsupported loyalty source key.");
        const string sql = @"SELECT LoyaltyPiecePointSettingId, PieceCode, PieceName, Points, IsActive, CreatedAtUtc, UpdatedAtUtc, ProductTypeId FROM dbo.LoyaltyPiecePointSettings WHERE ProductTypeId = @id AND IsActive = 1";
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", key);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new LoyaltyPiecePointSettingDto(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetDecimal(3), reader.GetBoolean(4), reader.GetDateTime(5), reader.GetDateTime(6), reader.GetInt32(7));
    }
    public async Task<LoyaltyPiecePointSettingDto?> GetActivePieceSettingAsync(string pieceCode, CancellationToken cancellationToken)
    {
        const string sql = "SELECT LoyaltyPiecePointSettingId, PieceCode, PieceName, Points, IsActive, CreatedAtUtc, UpdatedAtUtc FROM dbo.LoyaltyPiecePointSettings WHERE PieceCode = @pieceCode AND IsActive = 1";
        var rows = await QueryAsync(sql, pieceCode, reader => new LoyaltyPiecePointSettingDto(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetDecimal(3),
            reader.GetBoolean(4),
            reader.GetDateTime(5),
            reader.GetDateTime(6)), cancellationToken);
        return rows.SingleOrDefault();
    }

    public async Task<LoyaltyProgramSettingsDto?> GetProgramSettingsAsync(CancellationToken cancellationToken)
    {
        const string sql = "SELECT LoyaltyProgramSettingId, IsEnabled, PointsPerPiece, PointMonetaryValue, EffectiveFromUtc, UpdatedAtUtc, AllowRedemption, MinimumRedemptionPoints, MaximumRedemptionPoints, LoyaltyFreezeEnabled, GracePeriodDays, WarningPeriodDays, ManualReactivationEnabled, PurchaseReactivationEnabled FROM dbo.LoyaltyProgramSettings ORDER BY EffectiveFromUtc DESC, LoyaltyProgramSettingId DESC";
        var rows = await QueryAsync(sql, reader => new LoyaltyProgramSettingsDto(
            reader.GetInt32(0), reader.GetBoolean(1), reader.GetDecimal(2), reader.GetDecimal(3),
            reader.GetDateTime(4), reader.GetDateTime(5), reader.GetBoolean(6), reader.GetDecimal(7),
            reader.GetDecimal(8), reader.GetBoolean(9), reader.GetInt32(10), reader.GetInt32(11),
            reader.GetBoolean(12), reader.GetBoolean(13)), cancellationToken);
        return rows.FirstOrDefault();
    }

    public async Task<PiecePointsResultDto> EvaluatePiecePointsAsync(int customerId, int orderId, string pieceCode, decimal quantity, string source, string? notes, CancellationToken cancellationToken)
    {
        var setting = await GetActivePieceSettingAsync(pieceCode, cancellationToken);
        var program = await GetProgramSettingsAsync(cancellationToken);
        var fallback = program?.PointsPerPiece ?? 0m;
        var basePoints = PiecePointsEngine.CalculateForItem(
            setting,
            quantity,
            fallback,
            program?.IsEnabled == true);
        var buyerEarn = PiecePointsEngine.CalculateBuyerEarn(basePoints, pieceCode, quantity, fallback);
        var rewardLevels = ReferralRewardEngine.CalculateLevels(basePoints, 1, 2, 3, 4);
        var fingerprint = ReferralRewardEngine.CreateFingerprint(customerId, customerId, orderId, 0, source, "RL3", $"{pieceCode}|{quantity}|{basePoints}");

        return new PiecePointsResultDto(basePoints, buyerEarn, rewardLevels, fingerprint, $"{customerId}:{orderId}:{pieceCode}:{quantity}");
    }

    private async Task<List<T>> QueryAsync<T>(string sql, Func<SqlDataReader, T> map, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var results = new List<T>();
        while (await reader.ReadAsync(cancellationToken)) results.Add(map(reader));
        return results;
    }

    private async Task<List<T>> QueryAsync<T>(string sql, string pieceCode, Func<SqlDataReader, T> map, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@pieceCode", pieceCode);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var results = new List<T>();
        while (await reader.ReadAsync(cancellationToken)) results.Add(map(reader));
        return results;
    }
}
