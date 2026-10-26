using System.Text.Json;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Finance;
using LUMAR_ERP_API_V2.DTOs.Settings;
using LUMAR_ERP_API_V2.DTOs.WhatsApp;
using LUMAR_ERP_API_V2.Services;
using LUMAR_ERP_API_V2.Utilities;
using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.Repositories;

public sealed class SettingsRepository(
    ReadOnlySqlConnectionFactory connections,
    OperationalSqlConnectionFactory operationalConnections,
    IProductionProductTypeIdentityResolver identities) : ISettingsRepository, IWhatsAppProviderStore
{
    private const string Safe = "LOWER(COALESCE(SettingName,'')) NOT LIKE '%password%' AND LOWER(COALESCE(SettingName,'')) NOT LIKE '%secret%' AND LOWER(COALESCE(SettingName,'')) NOT LIKE '%token%' AND LOWER(COALESCE(SettingName,'')) NOT LIKE '%api%key%' AND LOWER(COALESCE(SettingName,'')) NOT LIKE '%connection%string%' AND LOWER(COALESCE(SettingName,'')) NOT LIKE '%jwt%' AND LOWER(COALESCE(SettingName,'')) NOT LIKE '%scanner%key%' AND LOWER(COALESCE(SettingName,'')) NOT LIKE '%webhook%' AND LOWER(COALESCE(SettingValue,'')) NOT LIKE '%password=%' AND LOWER(COALESCE(SettingValue,'')) NOT LIKE '%server=%'";
    private const string ProductionRoutesSettingName = "ProductionRoutesConfig";
    private static readonly IReadOnlySet<string> CanonicalStages =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Printing",
            "FabricPrep",
            "Cutting",
            "Sewing",
            "Buttons",
            "Ironing",
            "Quality",
            "Assembly"
        };

    public async Task<IWhatsAppProviderLease> AcquireAsync(bool exclusive, CancellationToken cancellationToken)
    {
        var connection = operationalConnections.Create();
        SqlTransaction? transaction = null;
        try
        {
            await connection.OpenAsync(cancellationToken);
            transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
            await using var guard = new SqlCommand("""
                DECLARE @result int;
                EXEC @result = sys.sp_getapplock @Resource=N'LUMAR.ActiveWhatsAppProvider',
                    @LockMode=@mode, @LockOwner='Transaction', @LockTimeout=5000;
                SELECT @result;
                """, connection, transaction);
            guard.Parameters.AddWithValue("@mode", exclusive ? "Exclusive" : "Shared");
            if (Convert.ToInt32(await guard.ExecuteScalarAsync(cancellationToken)) < 0)
                throw new WhatsAppProviderConflictException("هناك عملية إرسال أو تبديل مزود قيد التنفيذ. حاول مجددًا بعد انتهائها.");
            await using var read = new SqlCommand("SELECT SettingValue FROM dbo.System_Settings WHERE SettingName=N'ActiveWhatsAppProvider';", connection, transaction);
            await using var reader = await read.ExecuteReaderAsync(cancellationToken);
            string? stored = null;
            if (await reader.ReadAsync(cancellationToken))
                stored = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
            if (await reader.ReadAsync(cancellationToken))
                throw new WhatsAppProviderConflictException("إعداد مزود واتساب مكرر. يلزم تدقيق الإعداد قبل التفعيل.");
            return new ProviderLease(connection, transaction, exclusive, WhatsAppProviderChannels.FromStoredValue(stored));
        }
        catch
        {
            if (transaction is not null) await transaction.DisposeAsync();
            await connection.DisposeAsync();
            throw;
        }
    }

    private sealed class ProviderLease(SqlConnection connection, SqlTransaction transaction, bool exclusive,
        ActiveWhatsAppProvider provider) : IWhatsAppProviderLease
    {
        public ActiveWhatsAppProvider Provider { get; private set; } = provider;

        public async Task SetAsync(ActiveWhatsAppProvider value, CancellationToken cancellationToken)
        {
            if (!exclusive || !Enum.IsDefined(value)) throw new InvalidOperationException("Invalid provider update.");
            await using var command = new SqlCommand("""
                UPDATE dbo.System_Settings SET SettingValue=@value
                WHERE SettingName=N'ActiveWhatsAppProvider';
                IF @@ROWCOUNT=0
                    INSERT INTO dbo.System_Settings (SettingName,SettingValue,Description)
                    VALUES (N'ActiveWhatsAppProvider',@value,N'Manually selected WhatsApp provider: Meta, Local or None');
                """, connection, transaction);
            command.Parameters.AddWithValue("@value", value.ToString());
            await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            Provider = value;
        }

        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    public Task<IReadOnlyList<SystemSettingListDto>> GetAllAsync(CancellationToken cancellationToken) =>
        QueryAsync("SELECT SettingID,SettingName,Description FROM dbo.System_Settings WHERE " + Safe + " ORDER BY SettingName,SettingID", null, MapList, cancellationToken);

    public async Task<SystemSettingDetailsDto?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        (await QueryAsync("SELECT SettingID,SettingName,SettingValue,Description FROM dbo.System_Settings WHERE SettingID=@id AND " + Safe, id, MapDetails, cancellationToken)).FirstOrDefault();

    public async Task<SystemSettingDetailsDto?> GetByKeyAsync(string key, CancellationToken cancellationToken) =>
        (await QueryAsync("SELECT SettingID,SettingName,SettingValue,Description FROM dbo.System_Settings WHERE SettingName=@key AND " + Safe, key, MapDetails, cancellationToken)).FirstOrDefault();

    public async Task<IReadOnlyList<SettingCategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken)
    {
        var settings = await GetAllAsync(cancellationToken);
        return settings
            .GroupBy(setting => Category(setting.SettingName))
            .OrderBy(group => group.Key)
            .Select(group => new SettingCategoryDto(group.Key, group.Count()))
            .ToList();
    }

    public async Task<IReadOnlyList<SystemSettingListDto>> GetByCategoryAsync(string category, CancellationToken cancellationToken) =>
        (await GetAllAsync(cancellationToken))
            .Where(setting => string.Equals(Category(setting.SettingName), category, StringComparison.OrdinalIgnoreCase))
            .ToList();

    public async Task<ProductionRoutesConfigDto> GetProductionRoutesConfigAsync(CancellationToken cancellationToken)
    {
        var value = await ReadSettingValueAsync(ProductionRoutesSettingName, cancellationToken);
        if (string.IsNullOrWhiteSpace(value))
        {
            return new ProductionRoutesConfigDto(Array.Empty<ProductionRouteEntryDto>());
        }

        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        return await ResolveRouteEntriesAsync(value, connection, null, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetProductionRoutePieceTypesAsync(CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        var types = await identities.GetActiveAsync(connection, null, cancellationToken);
        return types.Select(type => type.NameAr).ToList();
    }

    public async Task<ProductionRoutesConfigDto> UpsertProductionRoutesConfigAsync(ProductionRoutesConfigDto config, CancellationToken cancellationToken)
    {
        if (config is null || config.Routes is null || config.Routes.Count == 0)
        {
            throw new ArgumentException("يجب إرسال مسارات الإنتاج المطلوبة.");
        }

        var routes = new List<ProductionRouteEntryDto>();
        var seenProductTypeIds = new HashSet<int>();
        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        foreach (var entry in config.Routes)
        {
            var identity = await identities.ResolveAsync(
                connection,
                transaction,
                entry.ProductTypeId > 0 ? entry.ProductTypeId : null,
                entry.ProductTypeCode ?? entry.PieceType,
                entry.ProductTypeNameAr ?? entry.PieceType,
                null,
                cancellationToken);
            if (identity is null)
            {
                throw new ArgumentException("لا يوجد نوع منتج رسمي مطابق لمسار الإنتاج المرسل.");
            }

            if (!seenProductTypeIds.Add(identity.ProductTypeId))
            {
                throw new ArgumentException($"يوجد أكثر من مسار محفوظ لنوع المنتج رقم {identity.ProductTypeId}.");
            }

            var orderedStages = entry.IsEnabled
                ? NormalizeStages(entry.Stages, identity.NameAr, rejectUnknown: true)
                : Array.Empty<string>();
            routes.Add(new ProductionRouteEntryDto
            {
                ProductTypeId = identity.ProductTypeId,
                ProductTypeCode = identity.Code,
                ProductTypeNameAr = identity.NameAr,
                Stages = orderedStages,
                IsEnabled = entry.IsEnabled,
                PieceType = identity.Code,
            });
        }

        if (routes.Count == 0)
        {
            throw new ArgumentException("يجب إرسال مسارات إنتاج صالحة لنوع أو أكثر من أنواع القطع.");
        }

        var payload = new
        {
            routes = routes.Select(route => new
            {
                productTypeId = route.ProductTypeId,
                productTypeCode = route.ProductTypeCode,
                productTypeNameAr = route.ProductTypeNameAr,
                stages = route.Stages,
                isEnabled = route.IsEnabled,
            }).ToList()
        };
        var json = JsonSerializer.Serialize(payload);

        try
        {
            var exists = await ExistsSettingAsync(connection, transaction, ProductionRoutesSettingName, cancellationToken);
            if (exists)
            {
                await using var update = new SqlCommand(
                    "UPDATE dbo.System_Settings SET SettingValue=@value, Description=@description WHERE SettingName=@key;",
                    connection,
                    transaction);
                update.Parameters.AddWithValue("@key", ProductionRoutesSettingName);
                update.Parameters.AddWithValue("@value", json);
                update.Parameters.AddWithValue("@description", "Production route configuration keyed by PricingProductTypes.ProductTypeId");
                await update.ExecuteNonQueryAsync(cancellationToken);
            }
            else
            {
                await using var insert = new SqlCommand(
                    "INSERT INTO dbo.System_Settings (SettingName, SettingValue, Description) VALUES (@key, @value, @description);",
                    connection,
                    transaction);
                insert.Parameters.AddWithValue("@key", ProductionRoutesSettingName);
                insert.Parameters.AddWithValue("@value", json);
                insert.Parameters.AddWithValue("@description", "Production route configuration keyed by PricingProductTypes.ProductTypeId");
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }

        return await GetProductionRoutesConfigAsync(cancellationToken);
    }

    public async Task<MessageSettingsDto> GetMessageSettingsAsync(CancellationToken cancellationToken)
    {
        var defaults = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
        {
            ["MessageMasterEnabled"] = true,
            ["DirectReferralDefaultOn"] = true,
            ["OtherReferralDefaultOff"] = false,
            ["FridayGreetingEnabled"] = true,
            ["CampaignEnabled"] = true,
            ["WhatsAppEnabled"] = true,
        };

        foreach (var key in defaults.Keys.ToArray())
        {
            var value = await ReadSettingValueAsync(key, cancellationToken);
            defaults[key] = ParseBoolValue(value, defaults[key]);
        }

        return new MessageSettingsDto(
            defaults["MessageMasterEnabled"],
            defaults["DirectReferralDefaultOn"],
            defaults["OtherReferralDefaultOff"],
            defaults["FridayGreetingEnabled"],
            defaults["CampaignEnabled"],
            defaults["WhatsAppEnabled"]);
    }

    public async Task<MessageSettingsDto> UpdateMessageSettingsAsync(MessageSettingsDto config, CancellationToken cancellationToken)
    {
        if (config is null)
        {
            throw new ArgumentException("يجب إرسال إعدادات الرسائل المطلوبة.");
        }

        var entries = new[]
        {
            (Key: "MessageMasterEnabled", Value: config.MessageMasterEnabled),
            (Key: "DirectReferralDefaultOn", Value: config.DirectReferralDefaultOn),
            (Key: "OtherReferralDefaultOff", Value: config.OtherReferralDefaultOff),
            (Key: "FridayGreetingEnabled", Value: config.FridayGreetingEnabled),
            (Key: "CampaignEnabled", Value: config.CampaignEnabled),
            (Key: "WhatsAppEnabled", Value: config.WhatsAppEnabled),
        };

        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            foreach (var entry in entries)
            {
                var exists = await ExistsSettingAsync(connection, transaction, entry.Key, cancellationToken);
                if (exists)
                {
                    await using var update = new SqlCommand(
                        "UPDATE dbo.System_Settings SET SettingValue=@value, Description=@description WHERE SettingName=@key;",
                        connection,
                        transaction);
                    update.Parameters.AddWithValue("@key", entry.Key);
                    update.Parameters.AddWithValue("@value", entry.Value ? "true" : "false");
                    update.Parameters.AddWithValue("@description", $"Message settings: {entry.Key}");
                    await update.ExecuteNonQueryAsync(cancellationToken);
                    continue;
                }

                await using var insert = new SqlCommand(
                    "INSERT INTO dbo.System_Settings (SettingName, SettingValue, Description) VALUES (@key, @value, @description);",
                    connection,
                    transaction);
                insert.Parameters.AddWithValue("@key", entry.Key);
                insert.Parameters.AddWithValue("@value", entry.Value ? "true" : "false");
                insert.Parameters.AddWithValue("@description", $"Message settings: {entry.Key}");
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }

        return await GetMessageSettingsAsync(cancellationToken);
    }

    public async Task<bool> UpdateCurrencySettingsAsync(
        string currencyName,
        string preferredCurrency,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE dbo.System_Settings SET SettingValue = CASE SettingName
                WHEN N'Currency' THEN @currencyName
                WHEN N'PreferredCurrency' THEN @preferredCurrency
            END
            WHERE SettingName IN (N'Currency', N'PreferredCurrency');
            SELECT @@ROWCOUNT;
            """;

        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        command.Transaction = transaction;
        command.Parameters.AddWithValue("@currencyName", currencyName);
        command.Parameters.AddWithValue("@preferredCurrency", preferredCurrency);
        var affected = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        if (affected != 2)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> UpdatePrintSettingsAsync(
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken)
    {
        if (values is null || values.Count == 0)
        {
            return false;
        }

        if (values.Keys.Any(key => string.Equals(key.Trim(), "ActiveWhatsAppProvider", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("تغيير مزود واتساب متاح من إدارة مزودي واتساب فقط.");

        const string insertSql = """
            INSERT INTO dbo.System_Settings (SettingName, SettingValue, Description)
            VALUES (@key, @value, @description);
            """;

        const string updateSql = """
            UPDATE dbo.System_Settings
            SET SettingValue = @value,
                Description = @description
            WHERE SettingName = @key;
            """;

        await using var connection = operationalConnections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            foreach (var item in values)
            {
                var key = item.Key.Trim();
                await using var reservedKeyGuard = new SqlCommand("SELECT CASE WHEN @key=N'ActiveWhatsAppProvider' THEN 1 ELSE 0 END;", connection, transaction);
                reservedKeyGuard.Parameters.AddWithValue("@key", key);
                if (Convert.ToInt32(await reservedKeyGuard.ExecuteScalarAsync(cancellationToken)) == 1)
                    throw new ArgumentException("تغيير مزود واتساب متاح من إدارة مزودي واتساب فقط.");
                var value = item.Value ?? string.Empty;
                var exists = await ExistsAsync(connection, transaction, key, cancellationToken);

                if (exists)
                {
                    await using var update = new SqlCommand(updateSql, connection, transaction);
                    update.Parameters.AddWithValue("@key", key);
                    update.Parameters.AddWithValue("@value", value);
                    update.Parameters.AddWithValue("@description", $"Print settings for {key}");
                    await update.ExecuteNonQueryAsync(cancellationToken);
                    continue;
                }

                await using var insert = new SqlCommand(insertSql, connection, transaction);
                insert.Parameters.AddWithValue("@key", key);
                insert.Parameters.AddWithValue("@value", value);
                insert.Parameters.AddWithValue("@description", $"Print settings for {key}");
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<GeneralCashAccountSettingDto> GetGeneralCashAccountSettingAsync(CancellationToken cancellationToken)
    {
        var rawValue = await ReadSettingValueAsync(GeneralCashAccountSettingKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return new GeneralCashAccountSettingDto(null, null, false, "لم يتم تحديد صندوق نقدي عام افتراضي بعد.");
        }

        if (!int.TryParse(rawValue.Trim(), out var cashAccountId) || cashAccountId <= 0)
        {
            return new GeneralCashAccountSettingDto(null, null, false, "قيمة إعداد الصندوق النقدي العام غير صالحة.");
        }

        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        var account = await GetCashAccountAsync(connection, null, cashAccountId, cancellationToken);

        if (account is null)
        {
            return new GeneralCashAccountSettingDto(cashAccountId, null, false, "الحساب النقدي المحدد غير موجود في النظام.");
        }

        if (!GeneralCashAccountSettingValidator.IsEligible(account))
        {
            return new GeneralCashAccountSettingDto(
                account.CashAccountId,
                account.AccountName,
                false,
                "الحساب المحدد غير مؤهل ليكون صندوقاً نقدياً عاماً افتراضياً. يجب أن يكون نشطاً، بالعملة YER، وقابلاً للإيراد والمصروفات.");
        }

        return new GeneralCashAccountSettingDto(
            account.CashAccountId,
            account.AccountName,
            true,
            "تم تحديد الصندوق النقدي العام الافتراضي بنجاح.");
    }

    public async Task<GeneralCashAccountSettingDto> UpsertGeneralCashAccountSettingAsync(int? cashAccountId, CancellationToken cancellationToken)
    {
        if (cashAccountId is null)
        {
            await using var connection = operationalConnections.Create();
            await connection.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(
                "DELETE FROM dbo.System_Settings WHERE SettingName=@key;",
                connection);
            command.Parameters.AddWithValue("@key", GeneralCashAccountSettingKey);
            await command.ExecuteNonQueryAsync(cancellationToken);
            return new GeneralCashAccountSettingDto(null, null, false, "تم إلغاء تحديد الصندوق النقدي العام الافتراضي.");
        }

        if (cashAccountId <= 0)
        {
            throw new ArgumentException("معرف الحساب النقدي العام يجب أن يكون أكبر من صفر.");
        }

        await using var connectionForCheck = connections.Create();
        await connectionForCheck.OpenAsync(cancellationToken);
        var account = await GetCashAccountAsync(connectionForCheck, null, cashAccountId.Value, cancellationToken);
        if (account is null)
        {
            throw new ArgumentException("الحساب النقدي المحدد غير موجود في النظام.");
        }

        if (!GeneralCashAccountSettingValidator.IsEligible(account))
        {
            throw new ArgumentException("لا يمكن تعيين هذا الحساب كصندوق نقدي عام افتراضي إلا إذا كان نشطاً، بالعملة YER، وقابلاً للإيراد والمصروفات.");
        }

        await using var writeConnection = operationalConnections.Create();
        await writeConnection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await writeConnection.BeginTransactionAsync(cancellationToken);

        try
        {
            var exists = await ExistsSettingAsync(writeConnection, transaction, GeneralCashAccountSettingKey, cancellationToken);
            if (exists)
            {
                await using var update = new SqlCommand(
                    "UPDATE dbo.System_Settings SET SettingValue=@value, Description=@description WHERE SettingName=@key;",
                    writeConnection,
                    transaction);
                update.Parameters.AddWithValue("@key", GeneralCashAccountSettingKey);
                update.Parameters.AddWithValue("@value", cashAccountId.Value.ToString());
                update.Parameters.AddWithValue("@description", "Default general cash account selection managed through settings.");
                await update.ExecuteNonQueryAsync(cancellationToken);
            }
            else
            {
                await using var insert = new SqlCommand(
                    "INSERT INTO dbo.System_Settings (SettingName, SettingValue, Description) VALUES (@key, @value, @description);",
                    writeConnection,
                    transaction);
                insert.Parameters.AddWithValue("@key", GeneralCashAccountSettingKey);
                insert.Parameters.AddWithValue("@value", cashAccountId.Value.ToString());
                insert.Parameters.AddWithValue("@description", "Default general cash account selection managed through settings.");
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }

        return await GetGeneralCashAccountSettingAsync(cancellationToken);
    }

    private const string GeneralCashAccountSettingKey = "GeneralCashAccountId";

    private async Task<string?> ReadSettingValueAsync(string settingName, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(
            "SELECT TOP (1) SettingValue FROM dbo.System_Settings WITH (NOLOCK) WHERE SettingName=@key",
            connection);
        command.Parameters.AddWithValue("@key", settingName);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null ? null : Convert.ToString(value);
    }

    private async Task<CashAccountDto?> GetCashAccountAsync(
        SqlConnection connection,
        SqlTransaction? transaction,
        int cashAccountId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT ca.CashAccountId, ca.AccountName,
                   COALESCE(SUM(CASE cm.CashDirection WHEN 1 THEN cm.Amount WHEN 2 THEN -cm.Amount ELSE 0 END), 0),
                   ca.CurrentBalance, ca.IsActive,
                   CASE WHEN ca.IsActive = 1 AND ca.CashAccountType = 1 AND ca.AllowsReceipts = 1 AND ca.CurrencyCode IS NOT NULL AND ca.LedgerControlAccountId IS NOT NULL THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END,
                   CASE WHEN ca.IsActive = 1 AND ca.CashAccountType = 1 AND ca.AllowsDisbursements = 1 AND ca.CurrencyCode IS NOT NULL AND ca.LedgerControlAccountId IS NOT NULL THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END,
                   ca.CurrencyCode, ca.CreatedAt
            FROM dbo.CashAccounts ca
            LEFT JOIN dbo.CashMovements cm ON cm.CashAccountId = ca.CashAccountId
            WHERE ca.CashAccountId = @id
            GROUP BY ca.CashAccountId, ca.AccountName, ca.CurrentBalance, ca.IsActive, ca.CashAccountType, ca.AllowsReceipts, ca.AllowsDisbursements, ca.CurrencyCode, ca.LedgerControlAccountId, ca.CreatedAt;
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@id", cashAccountId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new CashAccountDto(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetDecimal(2),
            reader.GetDecimal(3),
            reader.GetBoolean(4),
            reader.GetBoolean(5),
            reader.GetBoolean(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.GetDateTime(8));
    }

    private static bool ParseBoolValue(string? value, bool fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var normalized = value.Trim();
        if (bool.TryParse(normalized, out var parsed))
        {
            return parsed;
        }

        if (int.TryParse(normalized, out var numeric))
        {
            return numeric != 0;
        }

        var lowered = normalized.ToLowerInvariant();
        if (lowered is "1" or "yes" or "y" or "on" or "true")
        {
            return true;
        }

        if (lowered is "0" or "no" or "n" or "off" or "false")
        {
            return false;
        }

        return fallback;
    }

    private static async Task<bool> ExistsSettingAsync(SqlConnection connection, SqlTransaction transaction, string key, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            "SELECT TOP (1) 1 FROM dbo.System_Settings WITH (UPDLOCK,HOLDLOCK) WHERE SettingName=@key",
            connection,
            transaction);
        command.Parameters.AddWithValue("@key", key);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private async Task<ProductionRoutesConfigDto> ResolveRouteEntriesAsync(
        string value,
        SqlConnection connection,
        SqlTransaction? transaction,
        CancellationToken cancellationToken)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            var entries = new List<ProductionRouteEntryDto>();
            var seenProductTypeIds = new HashSet<int>();
            foreach (var rawEntry in ParseRouteEntries(document.RootElement))
            {
                var identity = await identities.ResolveAsync(
                    connection,
                    transaction,
                    rawEntry.ProductTypeId > 0 ? rawEntry.ProductTypeId : null,
                    rawEntry.ProductTypeCode ?? rawEntry.PieceType,
                    rawEntry.ProductTypeNameAr ?? rawEntry.PieceType,
                    null,
                    cancellationToken);
                if (identity is null || !seenProductTypeIds.Add(identity.ProductTypeId))
                {
                    continue;
                }

                entries.Add(new ProductionRouteEntryDto
                {
                    ProductTypeId = identity.ProductTypeId,
                    ProductTypeCode = identity.Code,
                    ProductTypeNameAr = identity.NameAr,
                    Stages = rawEntry.IsEnabled
                        ? NormalizeStages(rawEntry.Stages, identity.NameAr, rejectUnknown: false)
                        : Array.Empty<string>(),
                    IsEnabled = rawEntry.IsEnabled,
                    PieceType = identity.Code,
                });
            }

            return new ProductionRoutesConfigDto(entries);
        }
        catch (JsonException)
        {
            return new ProductionRoutesConfigDto(Array.Empty<ProductionRouteEntryDto>());
        }
    }

    private static IEnumerable<RawProductionRouteEntry> ParseRouteEntries(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
            {
                var parsed = ParseRouteEntry(item);
                yield return parsed;
            }
            yield break;
        }

        if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("routes", out var routesElement) && routesElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in routesElement.EnumerateArray())
                {
                    var parsed = ParseRouteEntry(item);
                    yield return parsed;
                }
                yield break;
            }

            var single = ParseRouteEntry(root);
            yield return single;
        }
    }

    private static RawProductionRouteEntry ParseRouteEntry(JsonElement entry)
    {
        var pieceType = ReadString(entry, "pieceType", "PieceType", "type");
        var productTypeId = ReadInt(entry, "productTypeId", "ProductTypeId");
        var productTypeCode = ReadString(entry, "productTypeCode", "ProductTypeCode", "code", "Code");
        var productTypeNameAr = ReadString(entry, "productTypeNameAr", "ProductTypeNameAr", "nameAr", "NameAr", "name", "Name");

        var stages = new List<string>();
        if (TryReadArray(entry, out var array, "stages", "Stages", "route", "Route", "routes", "Routes"))
        {
            foreach (var stage in array.EnumerateArray())
            {
                if (stage.ValueKind == JsonValueKind.String)
                {
                    var value = stage.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        stages.Add(value.Trim());
                    }
                }
            }
        }
        else if (entry.ValueKind == JsonValueKind.Object && entry.TryGetProperty("value", out var valueElement) && valueElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var stage in valueElement.EnumerateArray())
            {
                if (stage.ValueKind == JsonValueKind.String)
                {
                    var value = stage.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        stages.Add(value.Trim());
                    }
                }
            }
        }

        var isEnabled = !entry.TryGetProperty("isEnabled", out var enabledElement)
            || enabledElement.ValueKind != JsonValueKind.False;
        return new RawProductionRouteEntry(productTypeId, productTypeCode, productTypeNameAr, pieceType, stages, isEnabled);
    }

    private static IReadOnlyList<string> NormalizeStages(IReadOnlyList<string> stages, string typeName, bool rejectUnknown)
    {
        var requested = stages
            .Where(stage => !string.IsNullOrWhiteSpace(stage))
            .Select(stage => stage.Trim())
            .ToList();
        var unknown = requested
            .Where(stage => !CanonicalStages.Contains(stage, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (rejectUnknown && unknown.Count > 0)
        {
            throw new ArgumentException($"تحتوي مراحل مسار {typeName} على قيمة غير رسمية: {string.Join(", ", unknown)}.");
        }

        return CanonicalStages
            .Where(stage => requested.Contains(stage, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    private static bool TryReadArray(JsonElement element, out JsonElement values, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array)
            {
                values = value;
                return true;
            }
        }

        values = default;
        return false;
    }

    private static string? ReadString(JsonElement element, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }

        return null;
    }

    private static int ReadInt(JsonElement element, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var result))
            {
                return result;
            }
        }

        return 0;
    }

    private sealed record RawProductionRouteEntry(
        int ProductTypeId,
        string? ProductTypeCode,
        string? ProductTypeNameAr,
        string? PieceType,
        IReadOnlyList<string> Stages,
        bool IsEnabled);

    private static async Task<bool> ExistsAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string key,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            "SELECT TOP (1) 1 FROM dbo.System_Settings WITH (UPDLOCK,HOLDLOCK) WHERE SettingName=@key",
            connection,
            transaction);
        command.Parameters.AddWithValue("@key", key);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static string Category(string? key)
    {
        var separator = key?.IndexOf('.') ?? -1;
        return separator > 0 ? key![..separator] : "General";
    }

    private static SystemSettingListDto MapList(SqlDataReader reader) =>
        new(reader.GetInt32(0), reader.NullableString("SettingName"), Category(reader.NullableString("SettingName")), reader.NullableString("Description"));

    private static SystemSettingDetailsDto MapDetails(SqlDataReader reader) =>
        new(reader.GetInt32(0), reader.NullableString("SettingName"), reader.NullableString("SettingValue"), Category(reader.NullableString("SettingName")), reader.NullableString("Description"));

    private async Task<IReadOnlyList<T>> QueryAsync<T>(
        string sql,
        object? parameter,
        Func<SqlDataReader, T> map,
        CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection);
        if (parameter is int id) command.Parameters.AddWithValue("@id", id);
        else if (parameter is string key) command.Parameters.AddWithValue("@key", key);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<T>();
        while (await reader.ReadAsync(cancellationToken)) result.Add(map(reader));
        return result;
    }
}
