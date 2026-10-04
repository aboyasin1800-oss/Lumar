using LUMAR_ERP_API_V2.Authorization;
using LUMAR_ERP_API_V2.DTOs.Auth;

namespace LUMAR_ERP_API_V2.Services;

public interface ISupplierOwnershipResolver
{
    Task<int?> ResolveCurrentSupplierAsync(CancellationToken cancellationToken = default);
    Task<int?> ResolveCurrentSupplier(CancellationToken cancellationToken = default);
    Task<int> RequireCurrentSupplierAsync(CancellationToken cancellationToken = default);
    bool CanAccessSupplier(int? currentSupplierId, int? requestedSupplierId);
}

public static class SupplierOwnershipGuard
{
    public static bool IsAllowed(int? currentSupplierId, int? requestedSupplierId)
        => !currentSupplierId.HasValue || !requestedSupplierId.HasValue || currentSupplierId.Value == requestedSupplierId.Value;

    public static bool IsCrossSupplierViolation(int? currentSupplierId, int? requestedSupplierId)
        => currentSupplierId.HasValue && requestedSupplierId.HasValue && currentSupplierId.Value != requestedSupplierId.Value;
}

public sealed class SupplierOwnershipResolver(IAuthenticatedUserContext userContext) : ISupplierOwnershipResolver
{
    public async Task<int?> ResolveCurrentSupplierAsync(CancellationToken cancellationToken = default)
    {
        var currentUser = await userContext.GetCurrentUserAsync(cancellationToken);
        if (currentUser is null || !string.Equals(currentUser.AccountType, "Supplier", StringComparison.OrdinalIgnoreCase))
            return null;

        return currentUser.SupplierId;
    }

    public Task<int?> ResolveCurrentSupplier(CancellationToken cancellationToken = default)
        => ResolveCurrentSupplierAsync(cancellationToken);

    public async Task<int> RequireCurrentSupplierAsync(CancellationToken cancellationToken = default)
    {
        var supplierId = await ResolveCurrentSupplierAsync(cancellationToken);
        if (!supplierId.HasValue)
            throw new InvalidOperationException("The authenticated mobile account does not resolve to a Supplier identity.");

        return supplierId.Value;
    }

    public bool CanAccessSupplier(int? currentSupplierId, int? requestedSupplierId)
        => SupplierOwnershipGuard.IsAllowed(currentSupplierId, requestedSupplierId);
}
