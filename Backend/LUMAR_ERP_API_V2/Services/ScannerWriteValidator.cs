namespace LUMAR_ERP_API_V2.Services;

using LUMAR_ERP_API_V2.DTOs.Production;

public static class ScannerWriteValidator
{
    public static CreateScannerDto ValidateForCreate(CreateScannerDto request)
    {
        if (request is null) throw new ArgumentException("Scanner payload is required.");

        if (string.IsNullOrWhiteSpace(request.ScannerCode)) throw new ArgumentException("ScannerCode is required.");
        if (string.IsNullOrWhiteSpace(request.ScannerName)) throw new ArgumentException("ScannerName is required.");

        return request with
        {
            ScannerCode = request.ScannerCode.Trim(),
            ScannerName = request.ScannerName.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            IsActive = NormalizeIsActive(request.IsActive),
            EmployeeId = NormalizeEmployeeId(request.EmployeeId),
            Permissions = NormalizePermissions(request.Permissions)
        };
    }

    public static UpdateScannerDto ValidateForUpdate(UpdateScannerDto request)
    {
        if (request is null) throw new ArgumentException("Scanner payload is required.");

        if (string.IsNullOrWhiteSpace(request.ScannerCode)) throw new ArgumentException("ScannerCode is required.");
        if (string.IsNullOrWhiteSpace(request.ScannerName)) throw new ArgumentException("ScannerName is required.");

        return request with
        {
            ScannerCode = request.ScannerCode.Trim(),
            ScannerName = request.ScannerName.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            IsActive = NormalizeIsActive(request.IsActive),
            EmployeeId = NormalizeEmployeeId(request.EmployeeId),
            Permissions = NormalizePermissions(request.Permissions)
        };
    }

    public static IReadOnlyList<ScannerPermissionWriteDto> NormalizePermissions(IReadOnlyList<ScannerPermissionWriteDto>? permissions)
    {
        var list = permissions ?? Array.Empty<ScannerPermissionWriteDto>();
        var normalized = list
            .Where(permission => permission is not null)
            .Select(permission => new ScannerPermissionWriteDto(
                permission.ProductTypeId,
                string.IsNullOrWhiteSpace(permission.Stage) ? string.Empty : permission.Stage.Trim(),
                permission.IsActive))
            .Where(permission => permission.ProductTypeId > 0 && !string.IsNullOrWhiteSpace(permission.Stage))
            .GroupBy(permission => new PermissionKey(permission.ProductTypeId, permission.Stage.Trim()), new PermissionKeyComparer())
            .Select(group => group.First())
            .OrderBy(permission => permission.ProductTypeId)
            .ThenBy(permission => permission.Stage, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (!normalized.Any())
        {
            return Array.Empty<ScannerPermissionWriteDto>();
        }

        return normalized;
    }

    private readonly record struct PermissionKey(int ProductTypeId, string Stage);

    private sealed class PermissionKeyComparer : IEqualityComparer<PermissionKey>
    {
        public bool Equals(PermissionKey x, PermissionKey y) =>
            x.ProductTypeId == y.ProductTypeId &&
            string.Equals(x.Stage, y.Stage, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(PermissionKey obj) =>
            HashCode.Combine(obj.ProductTypeId, StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Stage));
    }

    public static bool NormalizeIsActive(bool? isActive)
    {
        if (isActive is null) return true;
        return isActive.Value;
    }

    public static int? NormalizeEmployeeId(int? employeeId)
    {
        if (employeeId is null) return null;
        return employeeId.Value > 0 ? employeeId.Value : null;
    }

    public static void EnsureUniqueCode(string? scannerCode, IEnumerable<string>? existingCodes, string? currentScannerCode = null)
    {
        if (string.IsNullOrWhiteSpace(scannerCode)) throw new ArgumentException("ScannerCode is required.");

        var normalized = scannerCode.Trim();
        var codes = existingCodes ?? [];
        var duplicate = codes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim())
            .Any(code => string.Equals(code, normalized, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(code, currentScannerCode?.Trim(), StringComparison.OrdinalIgnoreCase));

        if (duplicate) throw new ArgumentException("ScannerCode already exists.");
    }
}
