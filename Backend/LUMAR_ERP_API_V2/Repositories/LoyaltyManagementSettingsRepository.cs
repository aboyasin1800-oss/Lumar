using System.Data;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Loyalty;
using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.Repositories;

public sealed class LoyaltyManagementSettingsRepository(ReadOnlySqlConnectionFactory connections, OperationalSqlConnectionFactory operationalConnections) : ILoyaltyManagementSettingsRepository
{
    // Program settings
    public async Task<IReadOnlyList<LoyaltyProgramSettingsDto>> GetProgramSettingsAsync(CancellationToken cancellationToken)
    {
        const string sql = "SELECT LoyaltyProgramSettingId, IsEnabled, PointsPerPiece, PointMonetaryValue, EffectiveFromUtc, UpdatedAtUtc, AllowRedemption, MinimumRedemptionPoints, MaximumRedemptionPoints, LoyaltyFreezeEnabled, GracePeriodDays, WarningPeriodDays, ManualReactivationEnabled, PurchaseReactivationEnabled FROM dbo.LoyaltyProgramSettings ORDER BY EffectiveFromUtc DESC, LoyaltyProgramSettingId DESC;";
        return await QueryAsync<LoyaltyProgramSettingsDto>(sql, null, MapProgramSetting, cancellationToken);
    }

    public async Task<LoyaltyProgramSettingsDto?> GetProgramSettingByIdAsync(int id, CancellationToken cancellationToken)
    {
        const string sql = "SELECT LoyaltyProgramSettingId, IsEnabled, PointsPerPiece, PointMonetaryValue, EffectiveFromUtc, UpdatedAtUtc, AllowRedemption, MinimumRedemptionPoints, MaximumRedemptionPoints, LoyaltyFreezeEnabled, GracePeriodDays, WarningPeriodDays, ManualReactivationEnabled, PurchaseReactivationEnabled FROM dbo.LoyaltyProgramSettings WHERE LoyaltyProgramSettingId = @id;";
        return (await QueryAsync<LoyaltyProgramSettingsDto>(sql, new SqlParameter("@id", id), MapProgramSetting, cancellationToken)).FirstOrDefault();
    }

    public async Task<LoyaltyProgramSettingsDto?> CreateProgramSettingsAsync(CreateLoyaltyProgramSettingsDto request, CancellationToken cancellationToken)
    {
        if (request.PointsPerPiece < 0m) throw new ArgumentException("PointsPerPiece cannot be negative.");
        if (request.PointMonetaryValue <= 0m) throw new ArgumentException("PointMonetaryValue must be greater than zero.");
        ValidateProgramSettings(request.MinimumRedemptionPoints, request.MaximumRedemptionPoints, request.GracePeriodDays, request.WarningPeriodDays);

        const string sql = @"INSERT INTO dbo.LoyaltyProgramSettings (IsEnabled, PointsPerPiece, PointMonetaryValue, EffectiveFromUtc, UpdatedAtUtc, AllowRedemption, MinimumRedemptionPoints, MaximumRedemptionPoints, LoyaltyFreezeEnabled, GracePeriodDays, WarningPeriodDays, ManualReactivationEnabled, PurchaseReactivationEnabled)
    OUTPUT INSERTED.LoyaltyProgramSettingId, INSERTED.IsEnabled, INSERTED.PointsPerPiece, INSERTED.PointMonetaryValue, INSERTED.EffectiveFromUtc, INSERTED.UpdatedAtUtc, INSERTED.AllowRedemption, INSERTED.MinimumRedemptionPoints, INSERTED.MaximumRedemptionPoints, INSERTED.LoyaltyFreezeEnabled, INSERTED.GracePeriodDays, INSERTED.WarningPeriodDays, INSERTED.ManualReactivationEnabled, INSERTED.PurchaseReactivationEnabled
    VALUES (@isEnabled, @pointsPerPiece, @pointMonetaryValue, @effectiveFromUtc, SYSDATETIME(), @allowRedemption, @minimumRedemptionPoints, @maximumRedemptionPoints, @loyaltyFreezeEnabled, @gracePeriodDays, @warningPeriodDays, @manualReactivationEnabled, @purchaseReactivationEnabled);";

        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@isEnabled", request.IsEnabled);
        command.Parameters.AddWithValue("@pointsPerPiece", request.PointsPerPiece);
        command.Parameters.AddWithValue("@pointMonetaryValue", request.PointMonetaryValue);
        command.Parameters.AddWithValue("@effectiveFromUtc", request.EffectiveFromUtc);
        command.Parameters.AddWithValue("@allowRedemption", request.AllowRedemption);
        command.Parameters.AddWithValue("@minimumRedemptionPoints", request.MinimumRedemptionPoints);
        command.Parameters.AddWithValue("@maximumRedemptionPoints", request.MaximumRedemptionPoints);
        command.Parameters.AddWithValue("@loyaltyFreezeEnabled", request.LoyaltyFreezeEnabled);
        command.Parameters.AddWithValue("@gracePeriodDays", request.GracePeriodDays);
        command.Parameters.AddWithValue("@warningPeriodDays", request.WarningPeriodDays);
        command.Parameters.AddWithValue("@manualReactivationEnabled", request.ManualReactivationEnabled);
        command.Parameters.AddWithValue("@purchaseReactivationEnabled", request.PurchaseReactivationEnabled);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return MapProgramSetting(reader);
    }

    public async Task<LoyaltyProgramSettingsDto?> UpdateProgramSettingsAsync(int id, UpdateLoyaltyProgramSettingsDto request, CancellationToken cancellationToken)
    {
        if (id <= 0) throw new ArgumentException("Program setting id must be positive.");
        if (request.PointsPerPiece < 0m) throw new ArgumentException("PointsPerPiece cannot be negative.");
        if (request.PointMonetaryValue <= 0m) throw new ArgumentException("PointMonetaryValue must be greater than zero.");
        ValidateProgramSettings(request.MinimumRedemptionPoints, request.MaximumRedemptionPoints, request.GracePeriodDays, request.WarningPeriodDays);

        const string sql = @"UPDATE dbo.LoyaltyProgramSettings
SET IsEnabled = @isEnabled,
    PointsPerPiece = @pointsPerPiece,
    PointMonetaryValue = @pointMonetaryValue,
    AllowRedemption = @allowRedemption,
    MinimumRedemptionPoints = @minimumRedemptionPoints,
    MaximumRedemptionPoints = @maximumRedemptionPoints,
    LoyaltyFreezeEnabled = @loyaltyFreezeEnabled,
    GracePeriodDays = @gracePeriodDays,
    WarningPeriodDays = @warningPeriodDays,
    ManualReactivationEnabled = @manualReactivationEnabled,
    PurchaseReactivationEnabled = @purchaseReactivationEnabled,
    EffectiveFromUtc = @effectiveFromUtc,
    UpdatedAtUtc = SYSDATETIME()
WHERE LoyaltyProgramSettingId = @id;";

        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@isEnabled", request.IsEnabled);
        command.Parameters.AddWithValue("@pointsPerPiece", request.PointsPerPiece);
        command.Parameters.AddWithValue("@pointMonetaryValue", request.PointMonetaryValue);
        command.Parameters.AddWithValue("@allowRedemption", request.AllowRedemption);
        command.Parameters.AddWithValue("@minimumRedemptionPoints", request.MinimumRedemptionPoints);
        command.Parameters.AddWithValue("@maximumRedemptionPoints", request.MaximumRedemptionPoints);
        command.Parameters.AddWithValue("@loyaltyFreezeEnabled", request.LoyaltyFreezeEnabled);
        command.Parameters.AddWithValue("@gracePeriodDays", request.GracePeriodDays);
        command.Parameters.AddWithValue("@warningPeriodDays", request.WarningPeriodDays);
        command.Parameters.AddWithValue("@manualReactivationEnabled", request.ManualReactivationEnabled);
        command.Parameters.AddWithValue("@purchaseReactivationEnabled", request.PurchaseReactivationEnabled);
        command.Parameters.AddWithValue("@effectiveFromUtc", request.EffectiveFromUtc);
        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0 ? await GetProgramSettingByIdAsync(id, cancellationToken) : null;
    }

    public async Task<LoyaltyProgramSettingsDto?> ActivateProgramSettingsAsync(int id, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE dbo.LoyaltyProgramSettings SET IsEnabled = 1, UpdatedAtUtc = SYSDATETIME() WHERE LoyaltyProgramSettingId = @id;";
        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0 ? await GetProgramSettingByIdAsync(id, cancellationToken) : null;
    }

    public async Task<LoyaltyProgramSettingsDto?> DeactivateProgramSettingsAsync(int id, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE dbo.LoyaltyProgramSettings SET IsEnabled = 0, UpdatedAtUtc = SYSDATETIME() WHERE LoyaltyProgramSettingId = @id;";
        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0 ? await GetProgramSettingByIdAsync(id, cancellationToken) : null;
    }

    private static LoyaltyProgramSettingsDto MapProgramSetting(SqlDataReader reader) => new(
        reader.GetInt32(0),
        reader.GetBoolean(1),
        reader.GetDecimal(2),
        reader.GetDecimal(3),
        reader.GetDateTime(4),
        reader.GetDateTime(5),
        reader.GetBoolean(6),
        reader.GetDecimal(7),
        reader.GetDecimal(8),
        reader.GetBoolean(9),
        reader.GetInt32(10),
        reader.GetInt32(11),
        reader.GetBoolean(12),
        reader.GetBoolean(13));

    private static void ValidateProgramSettings(
        decimal minimumRedemptionPoints,
        decimal maximumRedemptionPoints,
        int gracePeriodDays,
        int warningPeriodDays)
    {
        if (minimumRedemptionPoints < 0m) throw new ArgumentException("MinimumRedemptionPoints cannot be negative.");
        if (maximumRedemptionPoints < 0m) throw new ArgumentException("MaximumRedemptionPoints cannot be negative.");
        if (maximumRedemptionPoints != 0m && maximumRedemptionPoints < minimumRedemptionPoints)
            throw new ArgumentException("MaximumRedemptionPoints must be zero or greater than or equal to MinimumRedemptionPoints.");
        if (gracePeriodDays <= 0) throw new ArgumentException("GracePeriodDays must be greater than zero.");
        if (warningPeriodDays < 0 || warningPeriodDays >= gracePeriodDays)
            throw new ArgumentException("WarningPeriodDays must be between zero and less than GracePeriodDays.");
    }

    // Piece point settings
    public async Task<IReadOnlyList<LoyaltyPiecePointSettingDto>> GetPiecePointSettingsAsync(CancellationToken cancellationToken)
    {
        const string sql = "SELECT LoyaltyPiecePointSettingId, PieceCode, PieceName, Points, IsActive, CreatedAtUtc, UpdatedAtUtc FROM dbo.LoyaltyPiecePointSettings ORDER BY PieceName, LoyaltyPiecePointSettingId;";
        return await QueryAsync<LoyaltyPiecePointSettingDto>(sql, null, reader => new LoyaltyPiecePointSettingDto(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetDecimal(3),
            reader.GetBoolean(4),
            reader.GetDateTime(5),
            reader.GetDateTime(6)), cancellationToken);
    }

    public async Task<LoyaltyPiecePointSettingDto?> GetPiecePointSettingByIdAsync(int id, CancellationToken cancellationToken)
    {
        const string sql = "SELECT LoyaltyPiecePointSettingId, PieceCode, PieceName, Points, IsActive, CreatedAtUtc, UpdatedAtUtc FROM dbo.LoyaltyPiecePointSettings WHERE LoyaltyPiecePointSettingId = @id;";
        return (await QueryAsync<LoyaltyPiecePointSettingDto>(sql, new SqlParameter("@id", id), reader => new LoyaltyPiecePointSettingDto(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetDecimal(3),
            reader.GetBoolean(4),
            reader.GetDateTime(5),
            reader.GetDateTime(6)), cancellationToken)).FirstOrDefault();
    }

    public async Task<LoyaltyPiecePointSettingDto?> CreatePiecePointSettingAsync(CreateLoyaltyPiecePointSettingDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PieceCode)) throw new ArgumentException("PieceCode is required.");
        if (string.IsNullOrWhiteSpace(request.PieceName)) throw new ArgumentException("PieceName is required.");
        if (request.Points < 0m) throw new ArgumentException("Points cannot be negative.");

        const string sql = @"INSERT INTO dbo.LoyaltyPiecePointSettings (PieceCode, PieceName, Points, IsActive, CreatedAtUtc, UpdatedAtUtc)
OUTPUT INSERTED.LoyaltyPiecePointSettingId, INSERTED.PieceCode, INSERTED.PieceName, INSERTED.Points, INSERTED.IsActive, INSERTED.CreatedAtUtc, INSERTED.UpdatedAtUtc
VALUES (@pieceCode, @pieceName, @points, @isActive, SYSDATETIME(), SYSDATETIME());";

        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@pieceCode", request.PieceCode.Trim());
        command.Parameters.AddWithValue("@pieceName", request.PieceName.Trim());
        command.Parameters.AddWithValue("@points", request.Points);
        command.Parameters.AddWithValue("@isActive", request.IsActive);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new LoyaltyPiecePointSettingDto(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetDecimal(3), reader.GetBoolean(4), reader.GetDateTime(5), reader.GetDateTime(6));
    }

    public async Task<LoyaltyPiecePointSettingDto?> UpdatePiecePointSettingAsync(int id, UpdateLoyaltyPiecePointSettingDto request, CancellationToken cancellationToken)
    {
        if (id <= 0) throw new ArgumentException("Piece setting id must be positive.");
        if (string.IsNullOrWhiteSpace(request.PieceCode)) throw new ArgumentException("PieceCode is required.");
        if (string.IsNullOrWhiteSpace(request.PieceName)) throw new ArgumentException("PieceName is required.");
        if (request.Points < 0m) throw new ArgumentException("Points cannot be negative.");

        const string sql = @"UPDATE dbo.LoyaltyPiecePointSettings
SET PieceCode = @pieceCode,
    PieceName = @pieceName,
    Points = @points,
    IsActive = @isActive,
    UpdatedAtUtc = SYSDATETIME()
WHERE LoyaltyPiecePointSettingId = @id;";

        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@pieceCode", request.PieceCode.Trim());
        command.Parameters.AddWithValue("@pieceName", request.PieceName.Trim());
        command.Parameters.AddWithValue("@points", request.Points);
        command.Parameters.AddWithValue("@isActive", request.IsActive);
        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0 ? await GetPiecePointSettingByIdAsync(id, cancellationToken) : null;
    }

    public async Task<LoyaltyPiecePointSettingDto?> ActivatePiecePointSettingAsync(int id, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE dbo.LoyaltyPiecePointSettings SET IsActive = 1, UpdatedAtUtc = SYSDATETIME() WHERE LoyaltyPiecePointSettingId = @id;";
        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0 ? await GetPiecePointSettingByIdAsync(id, cancellationToken) : null;
    }

    public async Task<LoyaltyPiecePointSettingDto?> DeactivatePiecePointSettingAsync(int id, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE dbo.LoyaltyPiecePointSettings SET IsActive = 0, UpdatedAtUtc = SYSDATETIME() WHERE LoyaltyPiecePointSettingId = @id;";
        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0 ? await GetPiecePointSettingByIdAsync(id, cancellationToken) : null;
    }

    // Vip levels
    public async Task<IReadOnlyList<VipLevelDto>> GetVipLevelsAsync(CancellationToken cancellationToken)
    {
        const string sql = "SELECT VipLevelId, Code, DisplayName, MinimumPoints, Multiplier, Priority, IsActive, CreatedAt, UpdatedAt FROM dbo.VipLevels ORDER BY Priority, MinimumPoints;";
        return await QueryAsync<VipLevelDto>(sql, null, reader => new VipLevelDto(
            reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetDecimal(3), reader.GetDecimal(4), reader.GetInt32(5), reader.GetBoolean(6), reader.GetDateTime(7), reader.GetDateTime(8)), cancellationToken);
    }

    public async Task<VipLevelDto?> GetVipLevelByIdAsync(int id, CancellationToken cancellationToken)
    {
        const string sql = "SELECT VipLevelId, Code, DisplayName, MinimumPoints, Multiplier, Priority, IsActive, CreatedAt, UpdatedAt FROM dbo.VipLevels WHERE VipLevelId = @id;";
        return (await QueryAsync<VipLevelDto>(sql, new SqlParameter("@id", id), reader => new VipLevelDto(
            reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetDecimal(3), reader.GetDecimal(4), reader.GetInt32(5), reader.GetBoolean(6), reader.GetDateTime(7), reader.GetDateTime(8)), cancellationToken)).FirstOrDefault();
    }

    public async Task<VipLevelDto?> CreateVipLevelAsync(CreateVipLevelDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code)) throw new ArgumentException("Code is required.");
        if (string.IsNullOrWhiteSpace(request.DisplayName)) throw new ArgumentException("DisplayName is required.");
        if (request.MinimumPoints < 0m) throw new ArgumentException("MinimumPoints cannot be negative.");
        if (request.Multiplier < 0m) throw new ArgumentException("Multiplier cannot be negative.");

        const string sql = @"INSERT INTO dbo.VipLevels (Code, DisplayName, MinimumPoints, Multiplier, Priority, IsActive, CreatedAt, UpdatedAt)
OUTPUT INSERTED.VipLevelId, INSERTED.Code, INSERTED.DisplayName, INSERTED.MinimumPoints, INSERTED.Multiplier, INSERTED.Priority, INSERTED.IsActive, INSERTED.CreatedAt, INSERTED.UpdatedAt
VALUES (@code, @displayName, @minimumPoints, @multiplier, @priority, @isActive, SYSDATETIME(), SYSDATETIME());";

        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@code", request.Code.Trim());
        command.Parameters.AddWithValue("@displayName", request.DisplayName.Trim());
        command.Parameters.AddWithValue("@minimumPoints", request.MinimumPoints);
        command.Parameters.AddWithValue("@multiplier", request.Multiplier);
        command.Parameters.AddWithValue("@priority", request.Priority);
        command.Parameters.AddWithValue("@isActive", request.IsActive);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new VipLevelDto(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetDecimal(3), reader.GetDecimal(4), reader.GetInt32(5), reader.GetBoolean(6), reader.GetDateTime(7), reader.GetDateTime(8));
    }

    public async Task<VipLevelDto?> UpdateVipLevelAsync(int id, UpdateVipLevelDto request, CancellationToken cancellationToken)
    {
        if (id <= 0) throw new ArgumentException("Vip level id must be positive.");
        if (string.IsNullOrWhiteSpace(request.Code)) throw new ArgumentException("Code is required.");
        if (string.IsNullOrWhiteSpace(request.DisplayName)) throw new ArgumentException("DisplayName is required.");
        if (request.MinimumPoints < 0m) throw new ArgumentException("MinimumPoints cannot be negative.");
        if (request.Multiplier < 0m) throw new ArgumentException("Multiplier cannot be negative.");

        const string sql = @"UPDATE dbo.VipLevels
SET Code = @code,
    DisplayName = @displayName,
    MinimumPoints = @minimumPoints,
    Multiplier = @multiplier,
    Priority = @priority,
    IsActive = @isActive,
    UpdatedAt = SYSDATETIME()
WHERE VipLevelId = @id;";

        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@code", request.Code.Trim());
        command.Parameters.AddWithValue("@displayName", request.DisplayName.Trim());
        command.Parameters.AddWithValue("@minimumPoints", request.MinimumPoints);
        command.Parameters.AddWithValue("@multiplier", request.Multiplier);
        command.Parameters.AddWithValue("@priority", request.Priority);
        command.Parameters.AddWithValue("@isActive", request.IsActive);
        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0 ? await GetVipLevelByIdAsync(id, cancellationToken) : null;
    }

    public async Task<VipLevelDto?> ActivateVipLevelAsync(int id, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE dbo.VipLevels SET IsActive = 1, UpdatedAt = SYSDATETIME() WHERE VipLevelId = @id;";
        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0 ? await GetVipLevelByIdAsync(id, cancellationToken) : null;
    }

    public async Task<VipLevelDto?> DeactivateVipLevelAsync(int id, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE dbo.VipLevels SET IsActive = 0, UpdatedAt = SYSDATETIME() WHERE VipLevelId = @id;";
        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0 ? await GetVipLevelByIdAsync(id, cancellationToken) : null;
    }

    // Loyalty rules
    public async Task<IReadOnlyList<LoyaltyRuleDto>> GetLoyaltyRulesAsync(CancellationToken cancellationToken)
    {
        const string sql = "SELECT LoyaltyRuleId, RuleName, RuleType, IsActive, PointsValue, SpendingAmount, Multiplier, BonusPoints, Priority, VipLevelId, StartDate, EndDate, CreatedAt, UpdatedAt FROM dbo.LoyaltyRules ORDER BY Priority, LoyaltyRuleId;";
        return await QueryAsync<LoyaltyRuleDto>(sql, null, reader => new LoyaltyRuleDto(
            reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetBoolean(3), reader.GetDecimal(4), reader.GetDecimal(5), reader.GetDecimal(6), reader.GetDecimal(7), reader.GetInt32(8), reader.NullableInt32("VipLevelId"), reader.NullableDateTime("StartDate"), reader.NullableDateTime("EndDate"), reader.GetDateTime(12), reader.GetDateTime(13)), cancellationToken);
    }

    public async Task<LoyaltyRuleDto?> GetLoyaltyRuleByIdAsync(int id, CancellationToken cancellationToken)
    {
        const string sql = "SELECT LoyaltyRuleId, RuleName, RuleType, IsActive, PointsValue, SpendingAmount, Multiplier, BonusPoints, Priority, VipLevelId, StartDate, EndDate, CreatedAt, UpdatedAt FROM dbo.LoyaltyRules WHERE LoyaltyRuleId = @id;";
        return (await QueryAsync<LoyaltyRuleDto>(sql, new SqlParameter("@id", id), reader => new LoyaltyRuleDto(
            reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetBoolean(3), reader.GetDecimal(4), reader.GetDecimal(5), reader.GetDecimal(6), reader.GetDecimal(7), reader.GetInt32(8), reader.NullableInt32("VipLevelId"), reader.NullableDateTime("StartDate"), reader.NullableDateTime("EndDate"), reader.GetDateTime(12), reader.GetDateTime(13)), cancellationToken)).FirstOrDefault();
    }

    public async Task<LoyaltyRuleDto?> CreateLoyaltyRuleAsync(CreateLoyaltyRuleDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RuleName)) throw new ArgumentException("RuleName is required.");
        if (string.IsNullOrWhiteSpace(request.RuleType)) throw new ArgumentException("RuleType is required.");
        if (request.Priority < 0) throw new ArgumentException("Priority cannot be negative.");
        if (request.VipLevelId.HasValue && request.VipLevelId <= 0) throw new ArgumentException("VipLevelId must be positive when supplied.");

        const string sql = @"INSERT INTO dbo.LoyaltyRules (RuleName, RuleType, IsActive, PointsValue, SpendingAmount, Multiplier, BonusPoints, Priority, VipLevelId, StartDate, EndDate, CreatedAt, UpdatedAt)
OUTPUT INSERTED.LoyaltyRuleId, INSERTED.RuleName, INSERTED.RuleType, INSERTED.IsActive, INSERTED.PointsValue, INSERTED.SpendingAmount, INSERTED.Multiplier, INSERTED.BonusPoints, INSERTED.Priority, INSERTED.VipLevelId, INSERTED.StartDate, INSERTED.EndDate, INSERTED.CreatedAt, INSERTED.UpdatedAt
VALUES (@ruleName, @ruleType, @isActive, @pointsValue, @spendingAmount, @multiplier, @bonusPoints, @priority, @vipLevelId, @startDate, @endDate, SYSDATETIME(), SYSDATETIME());";

        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ruleName", request.RuleName.Trim());
        command.Parameters.AddWithValue("@ruleType", request.RuleType.Trim());
        command.Parameters.AddWithValue("@isActive", request.IsActive);
        command.Parameters.AddWithValue("@pointsValue", request.PointsValue);
        command.Parameters.AddWithValue("@spendingAmount", request.SpendingAmount);
        command.Parameters.AddWithValue("@multiplier", request.Multiplier);
        command.Parameters.AddWithValue("@bonusPoints", request.BonusPoints);
        command.Parameters.AddWithValue("@priority", request.Priority);
        command.Parameters.AddWithValue("@vipLevelId", request.VipLevelId.HasValue ? request.VipLevelId.Value : (object)DBNull.Value);
        command.Parameters.AddWithValue("@startDate", request.StartDate ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@endDate", request.EndDate ?? (object)DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new LoyaltyRuleDto(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetBoolean(3), reader.GetDecimal(4), reader.GetDecimal(5), reader.GetDecimal(6), reader.GetDecimal(7), reader.GetInt32(8), reader.IsDBNull(9) ? null : reader.GetInt32(9), reader.IsDBNull(10) ? null : reader.GetDateTime(10), reader.IsDBNull(11) ? null : reader.GetDateTime(11), reader.GetDateTime(12), reader.GetDateTime(13));
    }

    public async Task<LoyaltyRuleDto?> UpdateLoyaltyRuleAsync(int id, UpdateLoyaltyRuleDto request, CancellationToken cancellationToken)
    {
        if (id <= 0) throw new ArgumentException("Rule id must be positive.");
        if (string.IsNullOrWhiteSpace(request.RuleName)) throw new ArgumentException("RuleName is required.");
        if (string.IsNullOrWhiteSpace(request.RuleType)) throw new ArgumentException("RuleType is required.");
        if (request.Priority < 0) throw new ArgumentException("Priority cannot be negative.");
        if (request.VipLevelId.HasValue && request.VipLevelId <= 0) throw new ArgumentException("VipLevelId must be positive when supplied.");

        const string sql = @"UPDATE dbo.LoyaltyRules
SET RuleName = @ruleName,
    RuleType = @ruleType,
    IsActive = @isActive,
    PointsValue = @pointsValue,
    SpendingAmount = @spendingAmount,
    Multiplier = @multiplier,
    BonusPoints = @bonusPoints,
    Priority = @priority,
    VipLevelId = @vipLevelId,
    StartDate = @startDate,
    EndDate = @endDate,
    UpdatedAt = SYSDATETIME()
WHERE LoyaltyRuleId = @id;";

        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@ruleName", request.RuleName.Trim());
        command.Parameters.AddWithValue("@ruleType", request.RuleType.Trim());
        command.Parameters.AddWithValue("@isActive", request.IsActive);
        command.Parameters.AddWithValue("@pointsValue", request.PointsValue);
        command.Parameters.AddWithValue("@spendingAmount", request.SpendingAmount);
        command.Parameters.AddWithValue("@multiplier", request.Multiplier);
        command.Parameters.AddWithValue("@bonusPoints", request.BonusPoints);
        command.Parameters.AddWithValue("@priority", request.Priority);
        command.Parameters.AddWithValue("@vipLevelId", request.VipLevelId.HasValue ? request.VipLevelId.Value : (object)DBNull.Value);
        command.Parameters.AddWithValue("@startDate", request.StartDate ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@endDate", request.EndDate ?? (object)DBNull.Value);
        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0 ? await GetLoyaltyRuleByIdAsync(id, cancellationToken) : null;
    }

    public async Task<LoyaltyRuleDto?> ActivateLoyaltyRuleAsync(int id, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE dbo.LoyaltyRules SET IsActive = 1, UpdatedAt = SYSDATETIME() WHERE LoyaltyRuleId = @id;";
        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0 ? await GetLoyaltyRuleByIdAsync(id, cancellationToken) : null;
    }

    public async Task<LoyaltyRuleDto?> DeactivateLoyaltyRuleAsync(int id, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE dbo.LoyaltyRules SET IsActive = 0, UpdatedAt = SYSDATETIME() WHERE LoyaltyRuleId = @id;";
        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0 ? await GetLoyaltyRuleByIdAsync(id, cancellationToken) : null;
    }

    // Referral rewards
    public async Task<IReadOnlyList<ReferralRewardDto>> GetReferralRewardsAsync(CancellationToken cancellationToken)
    {
        const string sql = "SELECT ReferralRewardId, RewardCode, RewardName, RewardType, FixedAmount, LoyaltyPoints, BonusMultiplier, MinSuccessfulReferrals, IsActive, CreatedAt, UpdatedAt FROM dbo.ReferralRewards ORDER BY RewardName, ReferralRewardId;";
        return await QueryAsync<ReferralRewardDto>(sql, null, reader => new ReferralRewardDto(
            reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetDecimal(4), reader.GetDecimal(5), reader.GetDecimal(6), reader.GetInt32(7), reader.GetBoolean(8), reader.GetDateTime(9), reader.GetDateTime(10)), cancellationToken);
    }

    public async Task<ReferralRewardDto?> GetReferralRewardByIdAsync(int id, CancellationToken cancellationToken)
    {
        const string sql = "SELECT ReferralRewardId, RewardCode, RewardName, RewardType, FixedAmount, LoyaltyPoints, BonusMultiplier, MinSuccessfulReferrals, IsActive, CreatedAt, UpdatedAt FROM dbo.ReferralRewards WHERE ReferralRewardId = @id;";
        return (await QueryAsync<ReferralRewardDto>(sql, new SqlParameter("@id", id), reader => new ReferralRewardDto(
            reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetDecimal(4), reader.GetDecimal(5), reader.GetDecimal(6), reader.GetInt32(7), reader.GetBoolean(8), reader.GetDateTime(9), reader.GetDateTime(10)), cancellationToken)).FirstOrDefault();
    }

    public async Task<ReferralRewardDto?> CreateReferralRewardAsync(CreateReferralRewardDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RewardCode)) throw new ArgumentException("RewardCode is required.");
        if (string.IsNullOrWhiteSpace(request.RewardName)) throw new ArgumentException("RewardName is required.");
        if (string.IsNullOrWhiteSpace(request.RewardType)) throw new ArgumentException("RewardType is required.");
        if (request.FixedAmount < 0m) throw new ArgumentException("FixedAmount cannot be negative.");
        if (request.LoyaltyPoints < 0m) throw new ArgumentException("LoyaltyPoints cannot be negative.");
        if (request.MinSuccessfulReferrals < 0) throw new ArgumentException("MinSuccessfulReferrals cannot be negative.");

        const string sql = @"INSERT INTO dbo.ReferralRewards (RewardCode, RewardName, RewardType, FixedAmount, LoyaltyPoints, BonusMultiplier, MinSuccessfulReferrals, IsActive, CreatedAt, UpdatedAt)
OUTPUT INSERTED.ReferralRewardId, INSERTED.RewardCode, INSERTED.RewardName, INSERTED.RewardType, INSERTED.FixedAmount, INSERTED.LoyaltyPoints, INSERTED.BonusMultiplier, INSERTED.MinSuccessfulReferrals, INSERTED.IsActive, INSERTED.CreatedAt, INSERTED.UpdatedAt
VALUES (@rewardCode, @rewardName, @rewardType, @fixedAmount, @loyaltyPoints, @bonusMultiplier, @minSuccessfulReferrals, @isActive, SYSDATETIME(), SYSDATETIME());";

        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@rewardCode", request.RewardCode.Trim());
        command.Parameters.AddWithValue("@rewardName", request.RewardName.Trim());
        command.Parameters.AddWithValue("@rewardType", request.RewardType.Trim());
        command.Parameters.AddWithValue("@fixedAmount", request.FixedAmount);
        command.Parameters.AddWithValue("@loyaltyPoints", request.LoyaltyPoints);
        command.Parameters.AddWithValue("@bonusMultiplier", request.BonusMultiplier);
        command.Parameters.AddWithValue("@minSuccessfulReferrals", request.MinSuccessfulReferrals);
        command.Parameters.AddWithValue("@isActive", request.IsActive);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new ReferralRewardDto(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetDecimal(4), reader.GetDecimal(5), reader.GetDecimal(6), reader.GetInt32(7), reader.GetBoolean(8), reader.GetDateTime(9), reader.GetDateTime(10));
    }

    public async Task<ReferralRewardDto?> UpdateReferralRewardAsync(int id, UpdateReferralRewardDto request, CancellationToken cancellationToken)
    {
        if (id <= 0) throw new ArgumentException("Reward id must be positive.");
        if (string.IsNullOrWhiteSpace(request.RewardCode)) throw new ArgumentException("RewardCode is required.");
        if (string.IsNullOrWhiteSpace(request.RewardName)) throw new ArgumentException("RewardName is required.");
        if (string.IsNullOrWhiteSpace(request.RewardType)) throw new ArgumentException("RewardType is required.");
        if (request.FixedAmount < 0m) throw new ArgumentException("FixedAmount cannot be negative.");
        if (request.LoyaltyPoints < 0m) throw new ArgumentException("LoyaltyPoints cannot be negative.");
        if (request.MinSuccessfulReferrals < 0) throw new ArgumentException("MinSuccessfulReferrals cannot be negative.");

        const string sql = @"UPDATE dbo.ReferralRewards
SET RewardCode = @rewardCode,
    RewardName = @rewardName,
    RewardType = @rewardType,
    FixedAmount = @fixedAmount,
    LoyaltyPoints = @loyaltyPoints,
    BonusMultiplier = @bonusMultiplier,
    MinSuccessfulReferrals = @minSuccessfulReferrals,
    IsActive = @isActive,
    UpdatedAt = SYSDATETIME()
WHERE ReferralRewardId = @id;";

        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@rewardCode", request.RewardCode.Trim());
        command.Parameters.AddWithValue("@rewardName", request.RewardName.Trim());
        command.Parameters.AddWithValue("@rewardType", request.RewardType.Trim());
        command.Parameters.AddWithValue("@fixedAmount", request.FixedAmount);
        command.Parameters.AddWithValue("@loyaltyPoints", request.LoyaltyPoints);
        command.Parameters.AddWithValue("@bonusMultiplier", request.BonusMultiplier);
        command.Parameters.AddWithValue("@minSuccessfulReferrals", request.MinSuccessfulReferrals);
        command.Parameters.AddWithValue("@isActive", request.IsActive);
        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0 ? await GetReferralRewardByIdAsync(id, cancellationToken) : null;
    }

    public async Task<ReferralRewardDto?> ActivateReferralRewardAsync(int id, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE dbo.ReferralRewards SET IsActive = 1, UpdatedAt = SYSDATETIME() WHERE ReferralRewardId = @id;";
        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0 ? await GetReferralRewardByIdAsync(id, cancellationToken) : null;
    }

    public async Task<ReferralRewardDto?> DeactivateReferralRewardAsync(int id, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE dbo.ReferralRewards SET IsActive = 0, UpdatedAt = SYSDATETIME() WHERE ReferralRewardId = @id;";
        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0 ? await GetReferralRewardByIdAsync(id, cancellationToken) : null;
    }

    private async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, SqlParameter? parameter, Func<SqlDataReader, T> map, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        if (parameter is not null)
        {
            command.Parameters.Add(parameter);
        }
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var items = new List<T>();
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(map(reader));
        }
        return items;
    }
}
