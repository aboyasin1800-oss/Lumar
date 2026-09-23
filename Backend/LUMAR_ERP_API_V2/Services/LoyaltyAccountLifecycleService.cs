using LUMAR_ERP_API_V2.DTOs.Loyalty;
using LUMAR_ERP_API_V2.Repositories;

namespace LUMAR_ERP_API_V2.Services;

public sealed class LoyaltyAccountLifecycleService(
    ILoyaltyRepository loyaltyRepository,
    ILoyaltyManagementSettingsRepository settingsRepository) : ILoyaltyAccountLifecycleService
{
    public async Task<LoyaltyAccountDto?> EvaluateAsync(int customerId, CancellationToken cancellationToken)
    {
        var account = await loyaltyRepository.GetAccountByCustomerAsync(customerId, cancellationToken);
        return account is null ? null : await EvaluateAccountAsync(account, DateTime.UtcNow, cancellationToken);
    }

    public async Task<LoyaltyAccountDto?> PrepareQualifyingPurchaseAsync(int customerId, CancellationToken cancellationToken)
    {
        var account = await loyaltyRepository.GetAccountByCustomerAsync(customerId, cancellationToken);
        if (account is null) return null;

        account = await EvaluateAccountAsync(account, DateTime.UtcNow, cancellationToken);
        if (!account.LoyaltyAccountStatus.Equals("Frozen", StringComparison.OrdinalIgnoreCase)) return account;

        var settings = await GetSettingsAsync(cancellationToken);
        if (settings?.PurchaseReactivationEnabled != true) return account;

        var now = DateTime.UtcNow;
        return account with
        {
            LoyaltyAccountStatus = "Active",
            WarningStartedAtUtc = null,
            FrozenAtUtc = null,
            ReactivatedAtUtc = now,
            FreezeReason = null,
        };
    }

    public async Task<LoyaltyAccountDto?> MarkQualifyingPurchaseAsync(int customerId, CancellationToken cancellationToken)
    {
        var account = await loyaltyRepository.GetAccountByCustomerAsync(customerId, cancellationToken);
        if (account is null) return null;

        var now = DateTime.UtcNow;
        return await loyaltyRepository.UpdateAccountLifecycleAsync(
            customerId,
            "Active",
            null,
            null,
            account.LoyaltyAccountStatus.Equals("Frozen", StringComparison.OrdinalIgnoreCase) ? now : account.ReactivatedAtUtc,
            null,
            now,
            cancellationToken);
    }

    public async Task<LoyaltyAccountDto?> ReactivateManuallyAsync(int customerId, CancellationToken cancellationToken)
    {
        var account = await loyaltyRepository.GetAccountByCustomerAsync(customerId, cancellationToken);
        if (account is null) return null;
        if (!account.LoyaltyAccountStatus.Equals("Frozen", StringComparison.OrdinalIgnoreCase)) return account;

        var settings = await GetSettingsAsync(cancellationToken);
        if (settings?.ManualReactivationEnabled != true)
            throw new InvalidOperationException("Manual loyalty account reactivation is disabled.");

        var now = DateTime.UtcNow;
        return await loyaltyRepository.UpdateAccountLifecycleAsync(
            customerId,
            "Active",
            null,
            null,
            now,
            null,
            account.LastQualifyingActivityAtUtc,
            cancellationToken);
    }

    public async Task<int> EvaluateAllAsync(CancellationToken cancellationToken)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        if (settings?.LoyaltyFreezeEnabled != true) return 0;

        var now = DateTime.UtcNow;
        var changed = 0;
        foreach (var account in await loyaltyRepository.GetAllAccountsAsync(cancellationToken))
        {
            var updated = await EvaluateAccountAsync(account, now, cancellationToken, settings);
            if (updated.LoyaltyAccountStatus != account.LoyaltyAccountStatus ||
                updated.WarningStartedAtUtc != account.WarningStartedAtUtc ||
                updated.FrozenAtUtc != account.FrozenAtUtc)
                changed++;
        }

        return changed;
    }

    private async Task<LoyaltyAccountDto> EvaluateAccountAsync(
        LoyaltyAccountDto account,
        DateTime now,
        CancellationToken cancellationToken,
        LoyaltyProgramSettingsDto? loadedSettings = null)
    {
        if (account.LoyaltyAccountStatus.Equals("Frozen", StringComparison.OrdinalIgnoreCase)) return account;

        var settings = loadedSettings ?? await GetSettingsAsync(cancellationToken);
        if (settings?.LoyaltyFreezeEnabled != true) return account;

        var baseline = account.LastQualifyingActivityAtUtc;
        if (account.ReactivatedAtUtc is DateTime reactivated && (baseline is null || reactivated > baseline))
            baseline = reactivated;
        baseline ??= account.CreatedAt;

        var freezeDueAt = baseline.Value.ToUniversalTime().AddDays(settings.GracePeriodDays);
        var warningStartsAt = freezeDueAt.AddDays(-settings.WarningPeriodDays);
        if (now >= freezeDueAt)
        {
            return await loyaltyRepository.UpdateAccountLifecycleAsync(
                account.CustomerId,
                "Frozen",
                account.WarningStartedAtUtc ?? warningStartsAt,
                now,
                account.ReactivatedAtUtc,
                "Grace period expired without a qualifying purchase.",
                account.LastQualifyingActivityAtUtc,
                cancellationToken) ?? account;
        }

        if (settings.WarningPeriodDays > 0 && now >= warningStartsAt)
        {
            return await loyaltyRepository.UpdateAccountLifecycleAsync(
                account.CustomerId,
                "Warning",
                account.WarningStartedAtUtc ?? now,
                null,
                account.ReactivatedAtUtc,
                null,
                account.LastQualifyingActivityAtUtc,
                cancellationToken) ?? account;
        }

        if (!account.LoyaltyAccountStatus.Equals("Active", StringComparison.OrdinalIgnoreCase) || account.WarningStartedAtUtc is not null)
        {
            return await loyaltyRepository.UpdateAccountLifecycleAsync(
                account.CustomerId,
                "Active",
                null,
                null,
                account.ReactivatedAtUtc,
                null,
                account.LastQualifyingActivityAtUtc,
                cancellationToken) ?? account;
        }

        return account;
    }

    private async Task<LoyaltyProgramSettingsDto?> GetSettingsAsync(CancellationToken cancellationToken) =>
        (await settingsRepository.GetProgramSettingsAsync(cancellationToken)).FirstOrDefault();
}
