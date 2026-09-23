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
            IsActive = NormalizeIsActive(request.IsActive)
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
            IsActive = NormalizeIsActive(request.IsActive)
        };
    }

    public static bool NormalizeIsActive(bool? isActive)
    {
        if (isActive is null) return true;
        return isActive.Value;
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
