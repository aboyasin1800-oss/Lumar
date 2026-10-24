namespace LUMAR_ERP_API_V2.Services;

public sealed record ScannerStagePermission(int ScannerId, string Stage, int ProductTypeId);

public static class ScannerPermissionValidator
{
    public static bool IsAllowed(int productTypeId, string? stage, IEnumerable<ScannerStagePermission>? permissions)
    {
        if (productTypeId <= 0 || string.IsNullOrWhiteSpace(stage))
        {
            return false;
        }

        var normalizedStage = stage.Trim();
        var permissionSet = permissions ?? Enumerable.Empty<ScannerStagePermission>();

        return permissionSet.Any(permission =>
            permission.ProductTypeId == productTypeId &&
            string.Equals(permission.Stage, normalizedStage, StringComparison.OrdinalIgnoreCase));
    }

    public static IReadOnlyList<string> GetAllowedStages(int productTypeId, IEnumerable<ScannerStagePermission>? permissions)
    {
        if (productTypeId <= 0)
        {
            return Array.Empty<string>();
        }

        return (permissions ?? Enumerable.Empty<ScannerStagePermission>())
            .Where(permission => permission.ProductTypeId == productTypeId)
            .Select(permission => permission.Stage)
            .Where(stage => !string.IsNullOrWhiteSpace(stage))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(stage => stage, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
