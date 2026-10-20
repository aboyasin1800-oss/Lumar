using LUMAR_ERP_API_V2.Authorization;
using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Auth;
using LUMAR_ERP_API_V2.DTOs.Push;
using LUMAR_ERP_API_V2.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace LUMAR_ERP_API_V2.Services;

public sealed class PushDeviceService(
    IMobilePushDeviceRepository repository,
    IAuthenticatedUserContext userContext,
    IPushSender sender,
    OperationalSqlConnectionFactory connections,
    ILogger<PushDeviceService> logger) : IPushDeviceService
{
    public async Task<PushDeviceSummaryDto?> RegisterAsync(RegisterPushDeviceRequest request, CancellationToken cancellationToken)
    {
        var currentUser = await userContext.GetCurrentUserAsync(cancellationToken);
        if (currentUser is null)
            throw new UnauthorizedAccessException("Authentication is required to register a device.");

        var mobileAccountId = await ResolveMobileAccountIdAsync(currentUser, cancellationToken);
        if (mobileAccountId <= 0)
            throw new InvalidOperationException("No official MobileAccount could be resolved for the authenticated user.");

        return await repository.RegisterAsync(request, mobileAccountId, cancellationToken);
    }

    public async Task<PushDeviceSummaryDto?> RefreshAsync(int deviceId, RegisterPushDeviceRequest request, CancellationToken cancellationToken)
    {
        var currentUser = await userContext.GetCurrentUserAsync(cancellationToken);
        if (currentUser is null)
            throw new UnauthorizedAccessException("Authentication is required to refresh a device.");

        var existing = await repository.GetByDeviceIdAsync(deviceId, cancellationToken);
        if (existing is null)
            return null;

        var currentMobileAccountId = await ResolveMobileAccountIdAsync(currentUser, cancellationToken);
        var deviceOwnerId = await GetDeviceOwnerAccountIdAsync(deviceId, cancellationToken);
        if (currentMobileAccountId != deviceOwnerId)
            throw new UnauthorizedAccessException("Device ownership does not match the authenticated account.");

        return await repository.RefreshAsync(deviceId, request, cancellationToken);
    }

    public async Task<PushDeviceSummaryDto?> DisableAsync(int deviceId, string reason, CancellationToken cancellationToken)
    {
        var currentUser = await userContext.GetCurrentUserAsync(cancellationToken);
        if (currentUser is null)
            throw new UnauthorizedAccessException("Authentication is required to disable a device.");

        var existing = await repository.GetByDeviceIdAsync(deviceId, cancellationToken);
        if (existing is null)
            return null;

        var currentMobileAccountId = await ResolveMobileAccountIdAsync(currentUser, cancellationToken);
        var deviceOwnerId = await GetDeviceOwnerAccountIdAsync(deviceId, cancellationToken);
        if (currentMobileAccountId != deviceOwnerId)
            throw new UnauthorizedAccessException("Device ownership does not match the authenticated account.");

        return await repository.DisableAsync(deviceId, reason, cancellationToken);
    }

    public async Task<IReadOnlyList<PushDeviceSummaryDto>> GetActiveAsync(CancellationToken cancellationToken)
    {
        var currentUser = await userContext.GetCurrentUserAsync(cancellationToken);
        if (currentUser is null)
            throw new UnauthorizedAccessException("Authentication is required to list devices.");

        var mobileAccountId = await ResolveMobileAccountIdAsync(currentUser, cancellationToken);
        return mobileAccountId > 0 ? await repository.GetActiveAsync(mobileAccountId, cancellationToken) : Array.Empty<PushDeviceSummaryDto>();
    }

    public async Task<PushDeliveryResult> DispatchAsync(PushDeliveryRequest request, CancellationToken cancellationToken)
    {
        var currentUser = await userContext.GetCurrentUserAsync(cancellationToken);
        if (currentUser is null)
            return new PushDeliveryResult(false, null, "Unauthorized", "Authentication is required for push dispatch.", false, 0, 0, Array.Empty<string>());

        var targetMobileAccountId = await ResolveMobileAccountIdAsync(currentUser, cancellationToken);
        if (targetMobileAccountId <= 0)
            return new PushDeliveryResult(false, null, "NoAccount", "No official MobileAccount could be resolved for the authenticated user.", false, 0, 0, Array.Empty<string>());

        var devices = await repository.GetActiveTokensAsync(targetMobileAccountId, cancellationToken);
        if (devices.Count == 0)
            return new PushDeliveryResult(false, null, "NoDevices", "No active devices are registered for this account.", false, 0, 0, Array.Empty<string>());

        var sentCount = 0;
        var failedCount = 0;
        var deviceKeys = new List<string>();

        foreach (var device in devices)
        {
            deviceKeys.Add(device.DeviceKey);
            var outcome = await sender.SendAsync(device, request.Title, request.Body, request.ReferenceType, request.ReferenceId, cancellationToken);
            if (outcome.Success)
            {
                sentCount++;
                logger.LogInformation("Push sent successfully for account {MobileAccountId} device {DeviceId} provider {Provider}.", targetMobileAccountId, device.DeviceId, device.Provider);
                continue;
            }

            failedCount++;
            logger.LogWarning("Push delivery failed for account {MobileAccountId} device {DeviceId} provider {Provider}. Error={Error}", targetMobileAccountId, device.DeviceId, device.Provider, outcome.Error);
        }

        return new PushDeliveryResult(sentCount > 0, null, failedCount == 0 ? "Completed" : "Partial", failedCount == 0 ? null : "One or more device deliveries failed.", failedCount > 0, sentCount, failedCount, deviceKeys);
    }

    public async Task<PushDeliveryResult> DispatchForOfficialNotificationAsync(
        int? customerId,
        int? employeeId,
        int? supplierId,
        string title,
        string body,
        string referenceType,
        string referenceId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required for official push delivery.", nameof(title));
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Body is required for official push delivery.", nameof(body));
        if (string.IsNullOrWhiteSpace(referenceType))
            throw new ArgumentException("ReferenceType is required for official push delivery.", nameof(referenceType));
        if (string.IsNullOrWhiteSpace(referenceId))
            throw new ArgumentException("ReferenceId is required for official push delivery.", nameof(referenceId));

        var currentUser = await userContext.GetCurrentUserAsync(cancellationToken);
        if (currentUser is null)
            return new PushDeliveryResult(false, null, "Unauthorized", "Authentication is required for official push delivery.", false, 0, 0, Array.Empty<string>());

        var resolvedMobileAccountId = await ResolveMobileAccountIdAsync(currentUser, cancellationToken);
        if (resolvedMobileAccountId <= 0)
            return new PushDeliveryResult(false, null, "NoAccount", "No official MobileAccount could be resolved for the authenticated user.", false, 0, 0, Array.Empty<string>());

        _ = customerId;
        _ = employeeId;
        _ = supplierId;

        var request = new PushDeliveryRequest(title, body, referenceType, referenceId, "Push");
        return await DispatchAsync(request, cancellationToken);
    }

    private async Task<int> ResolveMobileAccountIdAsync(CurrentUserDto currentUser, CancellationToken cancellationToken)
    {
        if (connections is null)
        {
            var normalizedAccountType = (currentUser.AccountType ?? string.Empty).Trim();
            if (string.Equals(normalizedAccountType, "Customer", StringComparison.OrdinalIgnoreCase) && currentUser.CustomerId is > 0)
                return currentUser.CustomerId.Value;
            if (string.Equals(normalizedAccountType, "Employee", StringComparison.OrdinalIgnoreCase) && currentUser.EmployeeId is > 0)
                return currentUser.EmployeeId.Value;
            if (string.Equals(normalizedAccountType, "Supplier", StringComparison.OrdinalIgnoreCase) && currentUser.SupplierId is > 0)
                return currentUser.SupplierId.Value;
            return 0;
        }

        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);

        var accountType = currentUser.AccountType ?? string.Empty;
        var customerId = currentUser.CustomerId ?? 0;
        var employeeId = currentUser.EmployeeId ?? 0;
        var supplierId = currentUser.SupplierId ?? 0;

        const string sql = @"SELECT TOP (1) MobileAccountId FROM dbo.MobileAccounts WHERE IsActive = 1 AND ((@accountType = 'Customer' AND CustomerId = @customerId) OR (@accountType = 'Employee' AND EmployeeId = @employeeId) OR (@accountType = 'Supplier' AND SupplierId = @supplierId)) ORDER BY MobileAccountId;";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@accountType", accountType);
        command.Parameters.AddWithValue("@customerId", customerId);
        command.Parameters.AddWithValue("@employeeId", employeeId);
        command.Parameters.AddWithValue("@supplierId", supplierId);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null ? 0 : Convert.ToInt32(result);
    }

    private async Task<int> GetDeviceOwnerAccountIdAsync(int deviceId, CancellationToken cancellationToken)
    {
        if (connections is null)
            return 0;

        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);

        const string sql = @"SELECT TOP (1) MobileAccountId FROM dbo.MobilePushDevices WHERE MobilePushDeviceId = @deviceId;";
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@deviceId", deviceId);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null ? 0 : Convert.ToInt32(result);
    }
}
