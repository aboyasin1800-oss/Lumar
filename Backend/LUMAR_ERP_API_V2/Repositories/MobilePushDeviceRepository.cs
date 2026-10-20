using System.Security.Cryptography;
using System.Text;
using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Push;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace LUMAR_ERP_API_V2.Repositories;

public sealed class MobilePushDeviceRepository(
    OperationalSqlConnectionFactory connections,
    IDataProtectionProvider dataProtectionProvider,
    ILogger<MobilePushDeviceRepository>? logger = null) : IMobilePushDeviceRepository
{
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("LUMAR_ERP_API_V2:MobilePushDevices");

    public async Task<PushDeviceSummaryDto?> RegisterAsync(RegisterPushDeviceRequest request, int mobileAccountId, CancellationToken cancellationToken)
    {
        ValidateRequest(request, mobileAccountId);

        var deviceKey = string.IsNullOrWhiteSpace(request.DeviceKey) ? Guid.NewGuid().ToString("N") : request.DeviceKey.Trim();
        var token = request.PushToken.Trim();
        var tokenHash = ComputeTokenHash(token);
        var encryptedToken = Convert.ToBase64String(_protector.Protect(Encoding.UTF8.GetBytes(token)));

        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);

        await EnsureSchemaAsync(connection, cancellationToken);

        await using var ownerCheck = new SqlCommand("SELECT TOP (1) MobileAccountId FROM dbo.MobilePushDevices WITH (NOLOCK) WHERE TokenHash = @tokenHash AND IsActive = 1 AND MobileAccountId <> @mobileAccountId;", connection);
        ownerCheck.Parameters.AddWithValue("@tokenHash", tokenHash);
        ownerCheck.Parameters.AddWithValue("@mobileAccountId", mobileAccountId);
        var otherOwnerId = await ownerCheck.ExecuteScalarAsync(cancellationToken);
        if (otherOwnerId is not null)
            throw new InvalidOperationException("This push token is already registered to a different MobileAccount.");

        await using var upsert = new SqlCommand(
            @"IF EXISTS (SELECT 1 FROM dbo.MobilePushDevices WHERE MobileAccountId = @mobileAccountId AND DeviceKey = @deviceKey)
                  BEGIN
                      UPDATE dbo.MobilePushDevices
                      SET Provider = @provider,
                          Platform = @platform,
                          PushTokenCiphertext = @tokenCiphertext,
                          TokenHash = @tokenHash,
                          IsActive = 1,
                          IsDisabled = 0,
                          UpdatedAtUtc = SYSUTCDATETIME(),
                          LastRegisteredAtUtc = SYSUTCDATETIME(),
                          DisabledAtUtc = NULL,
                          DisabledReason = NULL,
                          DeviceName = @deviceName,
                          AppVersion = @appVersion
                      OUTPUT INSERTED.MobilePushDeviceId, INSERTED.MobileAccountId, INSERTED.Provider, INSERTED.Platform, INSERTED.DeviceKey, INSERTED.IsActive, INSERTED.IsDisabled, INSERTED.CreatedAtUtc, INSERTED.UpdatedAtUtc, INSERTED.LastRegisteredAtUtc, INSERTED.DisabledAtUtc, INSERTED.DisabledReason, INSERTED.PushTokenCiphertext, INSERTED.TokenHash
                      WHERE MobileAccountId = @mobileAccountId AND DeviceKey = @deviceKey;
                  END
                  ELSE
                  BEGIN
                      INSERT INTO dbo.MobilePushDevices (MobileAccountId, Provider, Platform, DeviceKey, PushTokenCiphertext, TokenHash, IsActive, IsDisabled, CreatedAtUtc, UpdatedAtUtc, LastRegisteredAtUtc, DeviceName, AppVersion)
                      OUTPUT INSERTED.MobilePushDeviceId, INSERTED.MobileAccountId, INSERTED.Provider, INSERTED.Platform, INSERTED.DeviceKey, INSERTED.IsActive, INSERTED.IsDisabled, INSERTED.CreatedAtUtc, INSERTED.UpdatedAtUtc, INSERTED.LastRegisteredAtUtc, INSERTED.DisabledAtUtc, INSERTED.DisabledReason, INSERTED.PushTokenCiphertext, INSERTED.TokenHash
                      VALUES (@mobileAccountId, @provider, @platform, @deviceKey, @tokenCiphertext, @tokenHash, 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), SYSUTCDATETIME(), @deviceName, @appVersion);
                  END;",
            connection);

        upsert.Parameters.AddWithValue("@mobileAccountId", mobileAccountId);
        upsert.Parameters.AddWithValue("@provider", request.Provider.Trim());
        upsert.Parameters.AddWithValue("@platform", request.Platform.Trim());
        upsert.Parameters.AddWithValue("@deviceKey", deviceKey);
        upsert.Parameters.AddWithValue("@tokenCiphertext", encryptedToken);
        upsert.Parameters.AddWithValue("@tokenHash", tokenHash);
        upsert.Parameters.AddWithValue("@deviceName", string.IsNullOrWhiteSpace(request.DeviceName) ? (object)DBNull.Value : request.DeviceName.Trim());
        upsert.Parameters.AddWithValue("@appVersion", string.IsNullOrWhiteSpace(request.AppVersion) ? (object)DBNull.Value : request.AppVersion.Trim());

        await using var reader = await upsert.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return MapSummary(reader, mobileAccountId);
    }

    public async Task<PushDeviceSummaryDto?> RefreshAsync(int deviceId, RegisterPushDeviceRequest request, CancellationToken cancellationToken)
    {
        ValidateRequest(request, 0);
        var deviceKey = string.IsNullOrWhiteSpace(request.DeviceKey) ? Guid.NewGuid().ToString("N") : request.DeviceKey.Trim();
        var token = request.PushToken.Trim();
        var tokenHash = ComputeTokenHash(token);
        var encryptedToken = Convert.ToBase64String(_protector.Protect(Encoding.UTF8.GetBytes(token)));

        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection, cancellationToken);

        await using var check = new SqlCommand("SELECT TOP (1) MobileAccountId FROM dbo.MobilePushDevices WHERE MobilePushDeviceId = @deviceId;", connection);
        check.Parameters.AddWithValue("@deviceId", deviceId);
        var currentOwner = await check.ExecuteScalarAsync(cancellationToken);
        if (currentOwner is null)
            return null;

        var currentOwnerId = Convert.ToInt32(currentOwner);
        await using var ownerCheck = new SqlCommand("SELECT TOP (1) MobileAccountId FROM dbo.MobilePushDevices WITH (NOLOCK) WHERE TokenHash = @tokenHash AND IsActive = 1 AND MobileAccountId <> @mobileAccountId AND MobilePushDeviceId <> @deviceId;", connection);
        ownerCheck.Parameters.AddWithValue("@tokenHash", tokenHash);
        ownerCheck.Parameters.AddWithValue("@mobileAccountId", currentOwnerId);
        ownerCheck.Parameters.AddWithValue("@deviceId", deviceId);
        var conflictOwner = await ownerCheck.ExecuteScalarAsync(cancellationToken);
        if (conflictOwner is not null)
            throw new InvalidOperationException("This push token is already registered to a different MobileAccount.");

        await using var update = new SqlCommand(
            @"UPDATE dbo.MobilePushDevices
              SET Provider = @provider,
                  Platform = @platform,
                  DeviceKey = @deviceKey,
                  PushTokenCiphertext = @tokenCiphertext,
                  TokenHash = @tokenHash,
                  IsActive = 1,
                  IsDisabled = 0,
                  UpdatedAtUtc = SYSUTCDATETIME(),
                  LastRegisteredAtUtc = SYSUTCDATETIME(),
                  DisabledAtUtc = NULL,
                  DisabledReason = NULL,
                  DeviceName = @deviceName,
                  AppVersion = @appVersion
              OUTPUT INSERTED.MobilePushDeviceId, INSERTED.MobileAccountId, INSERTED.Provider, INSERTED.Platform, INSERTED.DeviceKey, INSERTED.IsActive, INSERTED.IsDisabled, INSERTED.CreatedAtUtc, INSERTED.UpdatedAtUtc, INSERTED.LastRegisteredAtUtc, INSERTED.DisabledAtUtc, INSERTED.DisabledReason, INSERTED.PushTokenCiphertext, INSERTED.TokenHash
              WHERE MobilePushDeviceId = @deviceId;", connection);

        update.Parameters.AddWithValue("@deviceId", deviceId);
        update.Parameters.AddWithValue("@provider", request.Provider.Trim());
        update.Parameters.AddWithValue("@platform", request.Platform.Trim());
        update.Parameters.AddWithValue("@deviceKey", deviceKey);
        update.Parameters.AddWithValue("@tokenCiphertext", encryptedToken);
        update.Parameters.AddWithValue("@tokenHash", tokenHash);
        update.Parameters.AddWithValue("@deviceName", string.IsNullOrWhiteSpace(request.DeviceName) ? (object)DBNull.Value : request.DeviceName.Trim());
        update.Parameters.AddWithValue("@appVersion", string.IsNullOrWhiteSpace(request.AppVersion) ? (object)DBNull.Value : request.AppVersion.Trim());

        await using var reader = await update.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return MapSummary(reader, currentOwnerId);
    }

    public async Task<PushDeviceSummaryDto?> DisableAsync(int deviceId, string reason, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection, cancellationToken);

        var disabledReason = string.IsNullOrWhiteSpace(reason) ? "Disabled by user." : reason.Trim();
        await using var update = new SqlCommand(
            @"UPDATE dbo.MobilePushDevices
              SET IsActive = 0,
                  IsDisabled = 1,
                  UpdatedAtUtc = SYSUTCDATETIME(),
                  DisabledAtUtc = SYSUTCDATETIME(),
                  DisabledReason = @disabledReason
              OUTPUT INSERTED.MobilePushDeviceId, INSERTED.MobileAccountId, INSERTED.Provider, INSERTED.Platform, INSERTED.DeviceKey, INSERTED.IsActive, INSERTED.IsDisabled, INSERTED.CreatedAtUtc, INSERTED.UpdatedAtUtc, INSERTED.LastRegisteredAtUtc, INSERTED.DisabledAtUtc, INSERTED.DisabledReason, INSERTED.PushTokenCiphertext, INSERTED.TokenHash
              WHERE MobilePushDeviceId = @deviceId;", connection);

        update.Parameters.AddWithValue("@deviceId", deviceId);
        update.Parameters.AddWithValue("@disabledReason", disabledReason);

        await using var reader = await update.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return MapSummary(reader, reader.GetInt32(1));
    }

    public async Task<IReadOnlyList<PushDeviceSummaryDto>> GetActiveAsync(int mobileAccountId, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection, cancellationToken);

        const string sql = @"SELECT MobilePushDeviceId, MobileAccountId, Provider, Platform, DeviceKey, IsActive, IsDisabled, CreatedAtUtc, UpdatedAtUtc, LastRegisteredAtUtc, DisabledAtUtc, DisabledReason, PushTokenCiphertext, TokenHash FROM dbo.MobilePushDevices WHERE MobileAccountId = @mobileAccountId AND IsActive = 1 ORDER BY UpdatedAtUtc DESC;";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@mobileAccountId", mobileAccountId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var result = new List<PushDeviceSummaryDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(MapSummary(reader, mobileAccountId));
        }

        return result;
    }

    public async Task<IReadOnlyList<PushDeviceTokenRecord>> GetActiveTokensAsync(int mobileAccountId, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection, cancellationToken);

        const string sql = @"SELECT MobilePushDeviceId, MobileAccountId, Provider, Platform, DeviceKey, PushTokenCiphertext, TokenHash, IsActive, IsDisabled, UpdatedAtUtc, DisabledAtUtc, DisabledReason FROM dbo.MobilePushDevices WHERE MobileAccountId = @mobileAccountId AND IsActive = 1 ORDER BY UpdatedAtUtc DESC;";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@mobileAccountId", mobileAccountId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var result = new List<PushDeviceTokenRecord>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var tokenCiphertext = reader.GetString(5);
            var tokenValue = DecryptToken(tokenCiphertext);
            result.Add(new PushDeviceTokenRecord(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                tokenCiphertext,
                reader.GetString(6),
                tokenValue,
                reader.GetBoolean(7),
                reader.GetBoolean(8),
                reader.GetDateTime(9),
                reader.IsDBNull(10) ? null : reader.GetDateTime(10),
                reader.IsDBNull(11) ? null : reader.GetString(11)));
        }

        return result;
    }

    public async Task<PushDeviceSummaryDto?> GetByDeviceIdAsync(int deviceId, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection, cancellationToken);

        const string sql = @"SELECT MobilePushDeviceId, MobileAccountId, Provider, Platform, DeviceKey, IsActive, IsDisabled, CreatedAtUtc, UpdatedAtUtc, LastRegisteredAtUtc, DisabledAtUtc, DisabledReason, PushTokenCiphertext, TokenHash FROM dbo.MobilePushDevices WHERE MobilePushDeviceId = @deviceId;";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@deviceId", deviceId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return MapSummary(reader, reader.GetInt32(1));
    }

    public async Task<bool> IsTokenRegisteredToAnotherAccountAsync(string tokenHash, int mobileAccountId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
            return false;

        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection, cancellationToken);

        await using var command = new SqlCommand("SELECT TOP (1) 1 FROM dbo.MobilePushDevices WITH (NOLOCK) WHERE TokenHash = @tokenHash AND MobileAccountId <> @mobileAccountId AND IsActive = 1;", connection);
        command.Parameters.AddWithValue("@tokenHash", tokenHash);
        command.Parameters.AddWithValue("@mobileAccountId", mobileAccountId);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private async Task EnsureSchemaAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            @"IF OBJECT_ID(N'dbo.MobilePushDevices', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.MobilePushDevices (
                        MobilePushDeviceId int IDENTITY(1,1) NOT NULL,
                        MobileAccountId int NOT NULL,
                        Provider nvarchar(32) NOT NULL,
                        Platform nvarchar(32) NOT NULL,
                        DeviceKey nvarchar(128) NOT NULL,
                        PushTokenCiphertext nvarchar(max) NOT NULL,
                        TokenHash nvarchar(128) NOT NULL,
                        IsActive bit NOT NULL CONSTRAINT DF_MobilePushDevices_IsActive DEFAULT ((1)),
                        IsDisabled bit NOT NULL CONSTRAINT DF_MobilePushDevices_IsDisabled DEFAULT ((0)),
                        CreatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_MobilePushDevices_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
                        UpdatedAtUtc datetime2(3) NOT NULL CONSTRAINT DF_MobilePushDevices_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
                        LastRegisteredAtUtc datetime2(3) NULL,
                        DisabledAtUtc datetime2(3) NULL,
                        DisabledReason nvarchar(256) NULL,
                        DeviceName nvarchar(128) NULL,
                        AppVersion nvarchar(64) NULL,
                        CONSTRAINT PK_MobilePushDevices PRIMARY KEY (MobilePushDeviceId),
                        CONSTRAINT FK_MobilePushDevices_MobileAccounts FOREIGN KEY (MobileAccountId) REFERENCES dbo.MobileAccounts(MobileAccountId),
                        CONSTRAINT CK_MobilePushDevices_Provider CHECK (Provider IN (N'FCM')),
                        CONSTRAINT CK_MobilePushDevices_Platform CHECK (Platform IN (N'Android')),
                        CONSTRAINT UQ_MobilePushDevices_TokenHash UNIQUE (TokenHash)
                    );

                    CREATE INDEX IX_MobilePushDevices_MobileAccountId_IsActive
                        ON dbo.MobilePushDevices(MobileAccountId, IsActive, UpdatedAtUtc DESC);

                    CREATE INDEX IX_MobilePushDevices_DeviceKey
                        ON dbo.MobilePushDevices(DeviceKey);
                END;

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.MobilePushDevices') AND name = N'IX_MobilePushDevices_MobileAccountId_IsActive')
                BEGIN
                    CREATE INDEX IX_MobilePushDevices_MobileAccountId_IsActive
                        ON dbo.MobilePushDevices(MobileAccountId, IsActive, UpdatedAtUtc DESC);
                END;

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.MobilePushDevices') AND name = N'IX_MobilePushDevices_DeviceKey')
                BEGIN
                    CREATE INDEX IX_MobilePushDevices_DeviceKey
                        ON dbo.MobilePushDevices(DeviceKey);
                END;",
            connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void ValidateRequest(RegisterPushDeviceRequest request, int mobileAccountId)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (mobileAccountId > 0 && string.IsNullOrWhiteSpace(request.Provider))
        {
            throw new InvalidOperationException("A valid MobileAccount is required for device registration.");
        }

        if (string.IsNullOrWhiteSpace(request.Provider) || !string.Equals(request.Provider.Trim(), "FCM", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Provider must be FCM.");

        if (string.IsNullOrWhiteSpace(request.Platform) || !string.Equals(request.Platform.Trim(), "Android", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Platform must be Android.");

        if (string.IsNullOrWhiteSpace(request.PushToken))
            throw new ArgumentException("Push token is required.");
    }

    private static string ComputeTokenHash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token.Trim()));
        return Convert.ToHexString(bytes);
    }

    private string DecryptToken(string ciphertext)
    {
        if (string.IsNullOrWhiteSpace(ciphertext))
            return string.Empty;

        try
        {
            var protectedBytes = Convert.FromBase64String(ciphertext);
            var decrypted = _protector.Unprotect(protectedBytes);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch
        {
            logger?.LogWarning("Unable to decrypt push token for device. Token was stored in an unreadable format.");
            return string.Empty;
        }
    }

    private static PushDeviceSummaryDto MapSummary(SqlDataReader reader, int mobileAccountId)
        => new(
            reader.GetInt32(0),
            mobileAccountId,
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetBoolean(5),
            reader.GetBoolean(6),
            reader.GetDateTime(7),
            reader.GetDateTime(8),
            reader.IsDBNull(9) ? null : reader.GetDateTime(9),
            reader.IsDBNull(10) ? null : reader.GetDateTime(10),
            reader.IsDBNull(11) ? null : reader.GetString(11),
            reader.IsDBNull(12) ? null : reader.GetString(12),
            reader.IsDBNull(13) ? null : reader.GetString(13));
}
